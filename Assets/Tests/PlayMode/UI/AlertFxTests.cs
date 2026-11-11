using System.Collections;
using System.Reflection;
using NUnit.Framework;
using QL.Core;
using QL.FX;
using QL.FX.Alert;
using QL.FX.Combat;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Drones;
using QL.Game.Flow;
using QL.Game.Input;
using QL.Game.Slimes;
using QL.Game.Stage;
using QL.Game.Summon;
using QL.UI.Hud;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace QL.Tests.PlayMode.UI
{
    /// <summary>
    /// U06 웨이브 · 피격 연출 — Game.unity 를 열고 GameEvents 리플렉션 발행 · SlimeRegistry.Spawn / Despawn 으로
    /// (U15: 경고 배너 지움) 히트 플래시 · 카메라 흔들림 · 위기 비네트 · HUD 해치 점멸 · 경고등을 본다. DroneSeeder · DragController 는 끈다.
    /// </summary>
    public sealed class AlertFxTests
    {
        private const string SceneName = "Game";
        private const int MaxWaitFrames = 300;
        private const float Slack = 0.3f;

        private GameBootstrap _bootstrap;
        private SlimeRegistry _slimes;
        private HitFlashFx _flash;
        private CameraShakeFx _shake;
        private HatchAlertFx _posts;
        private CriticalVignetteFx _vignette;
        private HudHatch _hatch;
        private Transform _camera;

        private FxConfig Fx => _flash.Config;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameEvents.ClearAll();
            SceneManager.sceneLoaded += DisableSeeder;
            SceneManager.LoadScene(SceneName, LoadSceneMode.Single);

            GameLoop loop = null;
            for (int i = 0; i < MaxWaitFrames; i++)
            {
                yield return null;
                loop = Object.FindAnyObjectByType<GameLoop>();
                StageBuilder stage = Object.FindAnyObjectByType<StageBuilder>();
                SummonController summon = Object.FindAnyObjectByType<SummonController>();
                if (loop != null && loop.IsReady && stage != null && stage.IsBuilt && summon != null && summon.IsReady) break;
            }
            SceneManager.sceneLoaded -= DisableSeeder;
            Assert.IsTrue(loop != null && loop.IsReady, "GameLoop 가 준비되지 않았다");
            yield return null; // HatchAlertFx 가 Posts 를 찾는 LateUpdate

            _bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            _slimes = Object.FindAnyObjectByType<SlimeRegistry>();
            _flash = Object.FindAnyObjectByType<HitFlashFx>();
            _shake = Object.FindAnyObjectByType<CameraShakeFx>();
            _posts = Object.FindAnyObjectByType<HatchAlertFx>();
            _vignette = Object.FindAnyObjectByType<CriticalVignetteFx>();
            _hatch = Object.FindAnyObjectByType<HudHatch>();
            Assert.IsNotNull(_flash, "[FX] 아래 AlertFx(HitFlashFx) 가 없다");
            Assert.IsNotNull(_shake, "[FX] 아래 AlertFx(CameraShakeFx) 가 없다");
            Assert.IsNotNull(_posts, "[FX] 아래 AlertFx(HatchAlertFx) 가 없다");
            Assert.IsNotNull(_vignette, "UIRoot 에 CriticalVignetteFx 가 없다");
            Assert.IsNotNull(_hatch);
            Assert.IsNotNull(_shake.Rig, "CameraShakeFx 가 CameraRig 를 찾는다");
            _camera = _shake.Rig.transform;
            Object.FindAnyObjectByType<DragController>().enabled = false;
            Time.timeScale = 1f;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SceneManager.sceneLoaded -= DisableSeeder;
            Time.timeScale = 1f;
            GameEvents.ClearAll();
            yield return null;
        }

        // ── 도우미 ───────────────────────────────────────────

        private static void DisableSeeder(Scene scene, LoadSceneMode mode)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                DroneSeeder[] seeders = roots[i].GetComponentsInChildren<DroneSeeder>(true);
                for (int j = 0; j < seeders.Length; j++) seeders[j].enabled = false;
            }
        }

        private static void Raise(string name, params object[] args)
        {
            MethodInfo m = typeof(GameEvents).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(m, "GameEvents." + name + " 이 없다");
            m.Invoke(null, args);
        }

        private static IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }

        private static IEnumerator WaitRealtime(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        private static Color BlockColor(Renderer r)
        {
            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            return block.GetColor("_BaseColor");
        }

        // ── 1. 웨이브 배너 — U15 에서 지움 (웨이브 시작 알림 = 작업 지시서 WaveOrderCard, WaveOrderTests) ──

        [UnityTest]
        public IEnumerator WaveBanner_RemovedInU15_NoBannerComponent()
        {
            Assert.IsNull(System.Type.GetType("QL.FX.Alert.WaveBannerFx, QL.FX"), "WaveBannerFx 는 U15 에서 지웠다");
            yield return null;
        }

        // ── 2. 히트 플래시 ───────────────────────────────────

        [UnityTest]
        public IEnumerator Hit_SetsBlock_ClearsAfterDuration_OtherSlimeUntouched()
        {
            SlimeView a = _slimes.Spawn(SlimeType.Blob, 1);
            SlimeView b = _slimes.Spawn(SlimeType.Blob, 3);
            yield return null;
            Renderer ra = _flash.RendererOf(a);
            Renderer rb = _flash.RendererOf(b);
            Assert.IsNotNull(ra);
            Assert.IsFalse(ra.HasPropertyBlock(), "아트 점액은 평소 블록 없음");

            Raise("RaiseSlimeDamaged", a, 5, DroneType.Suction);
            Assert.IsTrue(_flash.IsFlashing(a));
            Assert.IsTrue(ra.HasPropertyBlock(), "맞은 점액 MPB 설정");
            Color c = BlockColor(ra);
            Assert.Greater(c.r + c.g + c.b, 2.5f, "밝게 번쩍 (흰색 ↔ 종류색 × hitFlashIntensity)");
            var probe = new MaterialPropertyBlock();
            ra.GetPropertyBlock(probe);
            Assert.AreSame(Texture2D.whiteTexture, probe.GetTexture("_BaseMap"), "텍스처를 흰색으로 (초록 텍스처 곱셈은 노랗게만 보임)");
            Assert.IsFalse(rb.HasPropertyBlock(), "안 맞은 점액은 그대로");
            Vector3 scale = ra.transform.localScale;

            yield return Wait(Fx.hitFlashDuration * 0.5f);
            Raise("RaiseSlimeDamaged", a, 5, DroneType.Suction); // 연속 타격 = 시간만 다시
            Assert.AreEqual(1, _flash.ActiveCount, "트윈 · 항목을 쌓지 않는다");

            yield return Wait(Fx.hitFlashDuration + 0.1f);
            Assert.IsFalse(_flash.IsFlashing(a));
            Assert.IsFalse(ra.HasPropertyBlock(), "hitFlashDuration 뒤 해제 (원래 = 블록 없음)");
            Assert.AreEqual(scale, ra.transform.localScale, "트랜스폼은 건드리지 않는다");
        }

        [UnityTest]
        public IEnumerator Hit_ThenKilled_ClearedImmediately_ReusedSlimeClean()
        {
            SlimeView a = _slimes.Spawn(SlimeType.Mutant, 2);
            yield return null;
            Renderer ra = _flash.RendererOf(a);
            Raise("RaiseSlimeDamaged", a, 5, DroneType.Suction);
            Assert.IsTrue(ra.HasPropertyBlock());

            _slimes.Despawn(a, DespawnReason.Killed); // SlimeKilled → 반납 직전 해제
            Assert.IsFalse(_flash.IsFlashing(a));
            Assert.IsFalse(ra.HasPropertyBlock(), "SlimeKilled 즉시 해제");
            Assert.AreEqual(0, _flash.ActiveCount);

            // 풀에서 다시 나온 점액들 — 흰색으로 남은 것 없음
            for (int i = 0; i < 6; i++)
            {
                SlimeView s = _slimes.Spawn(SlimeType.Mutant, i % 5);
                Renderer r = _flash.RendererOf(s);
                Assert.IsFalse(r.HasPropertyBlock(), "재사용 점액 MPB 비어 있음");
            }

            SlimeView h = _slimes.Spawn(SlimeType.Blob, 0);
            yield return null;
            Renderer rh = _flash.RendererOf(h);
            Raise("RaiseSlimeDamaged", h, 5, DroneType.Suction);
            _slimes.Despawn(h, DespawnReason.ReachedLine); // SlimeReachedHatch 도 즉시 해제
            Assert.IsFalse(rh.HasPropertyBlock(), "SlimeReachedHatch 즉시 해제");
        }

        // ── F01 — 실제 공격 경로 (DroneCombat → SlimeDamaged, 이벤트 직접 발행 없음) ──

        [UnityTest]
        public IEnumerator F01_RealAttack_HitSlimeFlashesWhite_ForConfiguredDuration()
        {
            Assert.GreaterOrEqual(Fx.hitFlashDuration, 0.12f, "F01: 60fps 에서 7프레임 이상 (0.08 은 실제 플레이에서 안 보였음)");
            int cell = _bootstrap.Grid.IndexOf(2, 0);
            _bootstrap.Grid.Place(cell, DroneType.Suction, 1);
            Object.FindAnyObjectByType<DroneBoard>().SyncCell(cell);
            SlimeView target = _slimes.Spawn(SlimeType.Blob, 2);

            SlimeView hit = null;
            bool flashingAtEvent = false;
            System.Action<SlimeView, int, DroneType> onHit = (v, d, t) =>
            {
                if (hit != null) return;
                hit = v;
                flashingAtEvent = _flash.IsFlashing(v); // HitFlashFx 는 씬 시작 때 먼저 구독
            };
            GameEvents.SlimeDamaged += onHit;
            float end = Time.realtimeSinceStartup + 10f;
            while (hit == null && Time.realtimeSinceStartup < end) yield return null; // GameLoop 가 실제로 돈다 (준비 중에도 전투)
            GameEvents.SlimeDamaged -= onHit;

            Assert.AreSame(target, hit, "레인 2 흡입 드론이 실제로 맞힘");
            Assert.IsTrue(flashingAtEvent, "맞은 그 프레임에 번쩍 시작");
            Renderer r = _flash.RendererOf(hit);
            var probe = new MaterialPropertyBlock();
            r.GetPropertyBlock(probe);
            Assert.AreSame(Texture2D.whiteTexture, probe.GetTexture("_BaseMap"), "그려지는 프레임에 흰 텍스처");
            Color c = probe.GetColor("_BaseColor");
            Assert.Greater(Mathf.Min(c.r, Mathf.Min(c.g, c.b)), 1f, "흰색 (모든 채널 1 이상)");

            yield return Wait(Fx.hitFlashDuration * 0.6f);
            Assert.IsTrue(_flash.IsFlashing(hit) || hit.Combat.Hp <= 0, "번쩍 유지 중");
            yield return Wait(Fx.hitFlashDuration);
            // 흡입 1등급 간격 0.5초 > 번쩍 시간 — 다음 타격 전에 꺼진다
            Assert.IsFalse(_flash.IsFlashing(hit), "시간이 지나면 원래대로");
        }

        [UnityTest]
        public IEnumerator F01_KillHit_DoubleStartsWhite_ThenOriginalTexture()
        {
            var death = Object.FindAnyObjectByType<SlimeDeathFx>();
            Assert.IsNotNull(death, "[FX] 아래 CombatFx(SlimeDeathFx) 가 없다");
            Assert.Greater(Fx.deathFlashDuration, 0f);
            SlimeView a = _slimes.Spawn(SlimeType.Blob, 1);
            yield return null;
            Raise("RaiseSlimeDamaged", a, 40, DroneType.Suction);
            _slimes.Despawn(a, DespawnReason.Killed); // 처치 타격 — 같은 프레임에 반납 → 점액 플래시는 즉시 해제
            Assert.IsFalse(_flash.IsFlashing(a));
            SlimeDouble d = death.Last;
            Assert.IsNotNull(d);
            Renderer dr = d.GetComponent<MeshRenderer>();
            var probe = new MaterialPropertyBlock();
            dr.GetPropertyBlock(probe);
            Assert.AreSame(Texture2D.whiteTexture, probe.GetTexture("_BaseMap"), "대역 첫 프레임 흰색 — 처치 타격도 번쩍");

            yield return Wait(Fx.deathFlashDuration + 0.05f);
            if (d.Active)
            {
                dr.GetPropertyBlock(probe);
                Assert.AreNotSame(Texture2D.whiteTexture, probe.GetTexture("_BaseMap"), "그 뒤 점액 원래 텍스처");
            }
        }

        // ── 3. 카메라 흔들림 ─────────────────────────────────

        [UnityTest]
        public IEnumerator ThreeHatchHits_StrengthCapped_ReturnsToBase()
        {
            Vector3 basePos = _camera.position;
            SlimeView s = _slimes.Spawn(SlimeType.Blob, 2);
            yield return null;

            float maxOffset = 0f;
            for (int i = 0; i < 3; i++)
            {
                Raise("RaiseSlimeReachedHatch", s);
                Assert.LessOrEqual(_shake.Strength, Fx.shakeStrengthCap + 1e-5f, "흔들림 세기 ≤ 상한");
                yield return null;
                maxOffset = Mathf.Max(maxOffset, (_camera.position - basePos).magnitude);
            }
            Assert.IsTrue(_shake.IsShaking);
            Assert.AreEqual(3, _shake.HitCount);
            Assert.LessOrEqual(_shake.PeakStrength, Fx.shakeStrengthCap + 1e-5f);
            Assert.Greater(maxOffset, 0f, "카메라가 움직였다");

            float end = Time.time + Fx.shakeDuration + Slack;
            while (Time.time < end)
            {
                maxOffset = Mathf.Max(maxOffset, (_camera.position - basePos).magnitude);
                yield return null;
            }
            Assert.LessOrEqual(maxOffset, Fx.shakeStrengthCap * 1.4143f + 1e-4f, "오프셋 ≤ 상한 (x · y 각각)");
            Assert.IsFalse(_shake.IsShaking, "끝남");
            Assert.AreEqual(0f, (_camera.position - basePos).magnitude, 0.001f, "끝나면 카메라 = Refit 기본 위치");
        }

        [UnityTest]
        public IEnumerator Pause_StopsShake_CameraAtBase()
        {
            Vector3 basePos = _camera.position;
            SlimeView s = _slimes.Spawn(SlimeType.Blob, 2);
            yield return null;
            SlimeView hit = _slimes.Spawn(SlimeType.Blob, 4);
            yield return null;
            Raise("RaiseSlimeReachedHatch", s);
            Raise("RaiseSlimeDamaged", hit, 5, DroneType.Suction);
            yield return null;
            Assert.IsTrue(_shake.IsShaking);
            Assert.IsTrue(_flash.IsFlashing(hit));

            Time.timeScale = 0f;
            Raise("RaiseStateChanged", GameState.Paused);
            Assert.IsFalse(_shake.IsShaking, "일시정지 = 흔들림 멈춤");
            Assert.AreEqual(0f, (_camera.position - basePos).magnitude, 0.001f, "일시정지 즉시 원위치");
            Assert.IsFalse(_hatch.IsShaking, "HUD 흔들림도 멈춤");
            Assert.IsFalse(_flash.RendererOf(hit).HasPropertyBlock(), "일시정지 때 히트 플래시도 원래대로 (timeScale 0 에 흰색으로 멈추지 않게)");
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(0f, (_camera.position - basePos).magnitude, 0.001f, "일시정지 중 그대로");
            Time.timeScale = 1f;
        }

        // ── 4. HUD 해치 패널 ─────────────────────────────────

        [UnityTest]
        public IEnumerator HatchDamage_HudShakes_LostSegmentFlashesRed()
        {
            int max = _hatch.SegmentCount;
            Assert.Greater(max, 1);
            Raise("RaiseHatchHpChanged", max - 1, max);
            Assert.IsTrue(_hatch.IsShaking, "피해 → HUD 해치 패널 흔들림");
            yield return null;
            Color lost = _hatch.GetSegment(max - 1).color;
            Color hit = _hatch.HitFlashColor;
            Assert.Greater(lost.r, 0.8f * hit.r, "깎인 칸 빨강 번쩍");
            Assert.Greater(lost.a, _hatch.OffAlpha + 0.2f, "아직 꺼지기 전");
            yield return Wait(Mathf.Max(_hatch.FadeDuration, Fx.hudShakeDuration) + Slack);
            Assert.AreEqual(_hatch.OffAlpha, _hatch.GetSegment(max - 1).color.a, 0.01f, "번쩍 뒤 꺼진 칸");
            Assert.IsFalse(_hatch.IsShaking);
        }

        // ── 5. 위기 ─────────────────────────────────────────

        [UnityTest]
        public IEnumerator Critical_VignetteAndBlinks_ClearedOnResult()
        {
            int max = _bootstrap.Data.Config.HatchMaxHp;
            int crit = _bootstrap.Data.Config.HatchCriticalHp;
            Assert.IsFalse(_vignette.IsVisible, "평소 비네트 없음");

            Raise("RaiseHatchHpChanged", crit + 1, max);
            yield return null;
            Assert.IsFalse(_vignette.IsVisible, "위기 전");
            Assert.IsFalse(_hatch.IsCriticalBlinking);

            Raise("RaiseHatchHpChanged", crit, max);
            yield return Wait(Fx.vignettePeriod * 0.5f);
            Assert.IsTrue(_vignette.IsVisible, "위기 진입 → 비네트 켜짐");
            Assert.LessOrEqual(_vignette.Alpha, Fx.vignetteAlphaMax + 1e-4f, "알파 상한");
            Assert.Greater(_vignette.Alpha, Fx.vignetteAlphaMin, "점멸 중 (반 주기 = 최고)");
            Assert.IsFalse(_vignette.Image.raycastTarget, "입력 막지 않음");
            Assert.IsTrue(_hatch.IsCriticalBlinking, "HUD 해치 패널 붉게 점멸");
            Assert.IsTrue(_posts.IsCritical, "경고등 위기 점멸");
            if (_posts.PostCount > 0)
            {
                Assert.Greater(_posts.Level, 0f);
                Assert.IsTrue(_posts.PostAt(0).HasPropertyBlock(), "경고등 MPB");
            }

            Raise("RaiseGameEnded", default(ResultSummary));
            yield return null;
            Assert.IsFalse(_vignette.IsVisible, "결과 화면 때 꺼짐");
            Assert.IsFalse(_hatch.IsCriticalBlinking);
            Assert.IsFalse(_posts.IsCritical);
            if (_posts.PostCount > 0) Assert.IsFalse(_posts.PostAt(0).HasPropertyBlock(), "경고등 원래대로");
        }

        [UnityTest]
        public IEnumerator WaveStarted_PostsBlinkFast_ThenRestore()
        {
            Assert.Greater(_posts.PostCount, 0, "무대 Posts 기둥을 찾는다");
            Raise("RaiseWaveStarted", 0, 3, "배너");
            yield return Wait(Fx.postWaveBlinkPeriod * 0.5f);
            Assert.IsTrue(_posts.IsWaveBlinking);
            Assert.Greater(_posts.Level, 0.5f, "빠른 점멸 (반 주기 = 최고)");
            Assert.IsTrue(_posts.GlowAt(0).enabled, "꼭대기 빛");
            yield return Wait(Fx.postWaveBlinkDuration + Slack);
            Assert.IsFalse(_posts.IsWaveBlinking);
            Assert.IsFalse(_posts.PostAt(0).HasPropertyBlock(), "평소 값 (블록 없음)");
            Assert.IsFalse(_posts.GlowAt(0).enabled);
        }

        // ── U17 게이트 표기판 (보류함 33) ─────────────────────

        [UnityTest]
        public IEnumerator GateSign_ShowsWaveCode_AtR3Sign_OnScreen_SwapsOnPrep()
        {
            GateSignFx sign = Object.FindAnyObjectByType<GateSignFx>();
            Assert.IsNotNull(sign, "[FX] AlertFx 에 GateSignFx 가 없다");
            for (int i = 0; i < 10 && sign.Text == null; i++) yield return null;
            Assert.IsNotNull(sign.Sign, "R3 Stage_SpawnGate/GateSign 을 찾는다");
            Assert.IsNotNull(sign.Text, "글자를 만든다");

            string banner0 = _bootstrap.Data.Waves.Waves[0].BannerText;
            Assert.AreEqual("C-1", GateSignFx.CodeOf(banner0), "Waves.asset 실측 — 「C-1 화물칸, …」 첫 낱말");
            Assert.AreEqual("C-1", sign.Text.text, "시작 = 웨이브 1");
            Assert.AreEqual("QL_BagelFatOne SDF", sign.Text.font.name, "글꼴 하나");
            Assert.AreEqual(0f, Vector3.Distance(sign.Sign.position + Fx.gateSignOffset * _bootstrap.Data.Config.CellSize, sign.Text.transform.position), 1e-3f, "R3 표기판 자리");
            Assert.AreNotSame(sign.Sign, sign.Text.transform.parent, "R3 계층은 건드리지 않음 (글자는 AlertFx 아래)");

            // 화면 안 · 해치 HUD 패널 아래 (G09 p1 — 패널 아래 끝 바로 밑 왼쪽 기둥 앞)
            Camera cam = Camera.main;
            sign.Text.ForceMeshUpdate();
            Vector3 sp = cam.WorldToScreenPoint(sign.Text.transform.TransformPoint(sign.Text.bounds.center)); // TMP bounds = 로컬
            Assert.Greater(sp.z, 0f);
            Assert.That(sp.x, Is.InRange(0f, Screen.width), "화면 안 (x)");
            Assert.That(sp.y, Is.InRange(0f, Screen.height), "화면 안 (y)");
            var hatch = (RectTransform)GameObject.Find("HatchPanel").transform;
            var c = new Vector3[4];
            hatch.GetWorldCorners(c);
            Assert.Less(sp.y, c[0].y, "해치 패널 아래 (가려지지 않음)");

            Raise("RaiseWavePrepStarted", 1, default(QL.Core.Waves.WaveForecast));
            Assert.AreEqual(GateSignFx.CodeOf(_bootstrap.Data.Waves.Waves[1].BannerText), sign.Text.text, "WavePrepStarted 마다 교체");
            Assert.AreEqual("C-2", sign.Text.text);
            Assert.AreEqual(1, sign.ShownIndex);
            Assert.AreEqual(string.Empty, sign.CodeAt(99), "범위 밖 = 빈 글자 (예외 없음)");
        }
    }
}
