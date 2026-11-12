using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using QL.Core;
using QL.Core.Flow;
using QL.FX.Feel;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Drones;
using QL.Game.Flow;
using QL.Game.Input;
using QL.Game.Slimes;
using QL.Game.Stage;
using QL.Game.Summon;
using QL.Tests.PlayMode.UI;
using QL.UI.Hud;
using QL.UI.Screens;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace QL.Tests.PlayMode.FX
{
    /// <summary>
    /// U18b Feel 피드백 재구성 — 전투 순간 6개 (MotionFx/Feel 의 FeelMomentsFx) + UI 3개 (해치 패널 · 영수증 합계 · 결과 도장) 의 MMF_Player.
    /// 이벤트 → 그 플레이어 재생 1번 · 한 프레임 상한 · 일시정지 멈춤 (전투 = 게임 시간, 결과 = Unscaled) · 200회 반복해도 오브젝트 · 힙 증가 없음 ·
    /// 금지 피드백 (timescale · FreezeFrame · 카메라 shaker) 0. Game.unity 를 열고 GameLoop 를 끈 채 이벤트를 직접 올린다 (internal — 같은 테스트 어셈블리).
    /// 가이드(U09)는 GuidePrefsGuard 가 끝남으로 둔다 — 끝나면 원복.
    /// </summary>
    public sealed class FeelMomentsTests
    {
        private const string SceneName = "Game";
        private const int MaxWaitFrames = 300;

        private GuidePrefsGuard _guidePrefs;
        private GameLoop _loop;
        private GameBootstrap _bootstrap;
        private SlimeRegistry _slimes;
        private FeelMomentsFx _feel;

        private GridSpace Space => _bootstrap.Space;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _guidePrefs = GuidePrefsGuard.Save(); // F03 — QL.GuideDone 원복
            GameEvents.ClearAll();
            SceneManager.sceneLoaded += DisableSeeder;
            SceneManager.LoadScene(SceneName, LoadSceneMode.Single);
            GameLoop found = null;
            for (int i = 0; i < MaxWaitFrames; i++)
            {
                yield return null;
                found = Object.FindAnyObjectByType<GameLoop>();
                StageBuilder stage = Object.FindAnyObjectByType<StageBuilder>();
                SummonController summon = Object.FindAnyObjectByType<SummonController>();
                FeelMomentsFx feel = Object.FindAnyObjectByType<FeelMomentsFx>();
                if (found != null && found.IsReady && stage != null && stage.IsBuilt && summon != null && summon.IsReady && feel != null && feel.IsReady) break;
            }
            SceneManager.sceneLoaded -= DisableSeeder;
            Assert.IsTrue(found != null && found.IsReady, "GameLoop 가 준비되지 않았다");
            _loop = found;
            _loop.enabled = false; // 준비 시간이 흘러 웨이브가 시작되지 않게
            _bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            _slimes = Object.FindAnyObjectByType<SlimeRegistry>();
            Object.FindAnyObjectByType<DragController>().enabled = false;
            _feel = Object.FindAnyObjectByType<FeelMomentsFx>();
            Assert.IsNotNull(_feel, "[FX]/MotionFx/Feel 에 FeelMomentsFx 가 없다");
            Assert.IsTrue(_feel.IsReady, "시작 때 초기화 · 데우기");
            Time.timeScale = 1f;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _guidePrefs.Restore();
            SceneManager.sceneLoaded -= DisableSeeder;
            Time.timeScale = 1f;
            GameEvents.ClearAll();
            yield return null;
        }

        private static void DisableSeeder(Scene scene, LoadSceneMode mode)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                DroneSeeder[] seeders = roots[i].GetComponentsInChildren<DroneSeeder>(true);
                for (int j = 0; j < seeders.Length; j++) seeders[j].enabled = false;
            }
        }

        private static IEnumerator WaitReal(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        private SlimeView Slime(int lane)
        {
            SlimeView s = _slimes.Spawn(SlimeType.Blob, lane);
            Assert.IsNotNull(s);
            return s;
        }

        /// <summary>순간마다 이벤트 1번 올림 (게임 상태는 바꾸지 않는 표시 이벤트 — 점액은 살려 둔 채).</summary>
        private void RaiseAll(SlimeView slime, int cell)
        {
            GameEvents.RaiseDroneMerged(cell + 1, cell, DroneType.Suction, 2);
            GameEvents.RaiseSlimeKilled(slime);
            GameEvents.RaiseSlimeDamaged(slime, 1, DroneType.Plasma);
            GameEvents.RaiseSlimeReachedHatch(slime);
            GameEvents.RaiseCellCleaned(cell, true);
            GameEvents.RaiseDroneSpawned(cell, DroneType.Cryo, 1);
        }

        // ── 순간 → 재생 ──────────────────────────────────────

        [UnityTest]
        public IEnumerator EachMoment_EventPlaysItsPlayerOnce()
        {
            SlimeView s = Slime(1);
            int cell = 2;
            var before = new int[FeelMomentsFx.MomentCount];
            for (int m = 0; m < before.Length; m++)
            {
                Assert.Greater(_feel.FeedbackCountOf((FeelMoment)m), 0, (FeelMoment)m + " 플레이어에 피드백이 없다");
                before[m] = _feel.PlayCountOf((FeelMoment)m);
            }
            RaiseAll(s, cell);
            for (int m = 0; m < before.Length; m++)
            {
                FeelMoment fm = (FeelMoment)m;
                Assert.AreEqual(before[m] + 1, _feel.PlayCountOf(fm), fm + " 재생 1번");
                Assert.IsTrue(_feel.IsPlaying(fm), fm + " MMF_Player.IsPlaying");
            }
            Assert.AreEqual(before[(int)FeelMoment.Clean] + 1, _feel.PlayCountOf(FeelMoment.Clean));
            GameEvents.RaiseCellCleaned(cell, false); // 자연 청소 — Feel 없음 (O03 그대로)
            Assert.AreEqual(before[(int)FeelMoment.Clean] + 1, _feel.PlayCountOf(FeelMoment.Clean), "자연 청소는 재생 안 함");
            yield return null;
            Assert.Greater(_feel.ParticleCountOf(FeelMoment.Merge), 0, "머지 입자 터짐");
            Assert.IsTrue(_feel.UsesCameraKick(FeelMoment.Merge), "머지 흔들림 = CameraShakeFx.Kick 경로");
            Assert.IsFalse(_feel.UsesCameraKick(FeelMoment.Hatch), "해치 피격 흔들림은 CameraShakeFx 피격 흔들림 그대로 (중복 Kick 없음)");
            _slimes.Despawn(s, DespawnReason.Killed);
        }

        [UnityTest]
        public IEnumerator SameFrame_CappedPerMoment()
        {
            SlimeView s = Slime(2);
            yield return null;
            int cap = _feel.MaxPerFrameOf(FeelMoment.Hit);
            Assert.Greater(cap, 0);
            int plays = _feel.PlayCountOf(FeelMoment.Hit);
            int skipped = _feel.SkippedOf(FeelMoment.Hit);
            for (int i = 0; i < 10; i++) GameEvents.RaiseSlimeDamaged(s, 1, DroneType.Suction);
            Assert.AreEqual(plays + cap, _feel.PlayCountOf(FeelMoment.Hit), "한 프레임 상한");
            Assert.AreEqual(skipped + 10 - cap, _feel.SkippedOf(FeelMoment.Hit));
            yield return null;
            GameEvents.RaiseSlimeDamaged(s, 1, DroneType.Suction);
            Assert.AreEqual(plays + cap + 1, _feel.PlayCountOf(FeelMoment.Hit), "다음 프레임은 다시 재생");
            _slimes.Despawn(s, DespawnReason.Killed);
        }

        // ── 일시정지 ────────────────────────────────────────

        [UnityTest]
        public IEnumerator Pause_CombatFeedbackFreezes()
        {
            SlimeView s = Slime(0);
            Vector3 p = s.transform.position;
            _slimes.Despawn(s, DespawnReason.Killed); // 이벤트 순서 그대로 — 처치 Feel 은 SlimeKilled 로
            yield return null;
            int alive = _feel.ParticleCountOf(FeelMoment.Kill);
            Assert.Greater(alive, 0, "처치 입자");
            Time.timeScale = 0f;
            yield return WaitReal(0.9f);
            Assert.AreEqual(alive, _feel.ParticleCountOf(FeelMoment.Kill), "timeScale 0 — 입자 멈춤 (수명이 지나도 남음)");
            Time.timeScale = 1f;
            yield return WaitReal(0.9f);
            Assert.AreEqual(0, _feel.ParticleCountOf(FeelMoment.Kill), "재개하면 이어서 사라짐");
        }

        [UnityTest]
        public IEnumerator Result_StampFeelUnscaled_PanelThumpsWhileTimeStopped()
        {
            ResultScreen result = Object.FindAnyObjectByType<ResultScreen>(FindObjectsInactive.Include);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.HasStampFeel, "ResultScreen/StampFeel 연결");
            RectTransform panel = (RectTransform)result.transform.Find("Panel");
            GameEvents.RaiseGameEnded(new ResultSummary(GameOutcome.Defeat, 0, 20, 7, 0, 2, 3, 1, 0, 0, 0));
            Time.timeScale = 0f; // GameLoop 와 같음 — 결과 화면은 실제 시간
            yield return null;
            float baseY = panel.localPosition.y;
            float minY = baseY;
            float end = Time.realtimeSinceStartup + 2.2f;
            while (Time.realtimeSinceStartup < end && result.StampFeelPlays == 0) yield return null;
            Assert.AreEqual(1, result.StampFeelPlays, "도장이 닿는 순간 Feel 1번");
            end = Time.realtimeSinceStartup + 0.4f;
            while (Time.realtimeSinceStartup < end)
            {
                minY = Mathf.Min(minY, panel.localPosition.y);
                yield return null;
            }
            Assert.Less(minY, baseY - 3f, "timeScale 0 에서도 패널이 툭 내려감 (Unscaled)");
            Assert.AreEqual(baseY, panel.localPosition.y, 0.5f, "끝나면 제자리");
        }

        [UnityTest]
        public IEnumerator Hud_HatchHitAndPayslipTotal_PlayFeel()
        {
            HudHatch hatch = Object.FindAnyObjectByType<HudHatch>();
            Assert.IsNotNull(hatch);
            Assert.IsTrue(hatch.HasHitFeel, "HatchPanel/HitFeel 연결");
            yield return null;
            int max = _loop.Flow.Hatch.MaxHp;
            int plays = hatch.HitFeelPlays;
            float baseScale = hatch.HpText.transform.localScale.x;
            GameEvents.RaiseHatchHpChanged(max - 1, max);
            Assert.AreEqual(plays + 1, hatch.HitFeelPlays, "해치 피해 → HP 숫자 펀치");
            yield return null;
            yield return null;
            Assert.AreNotEqual(baseScale, hatch.HpText.transform.localScale.x, "스프링 펀치가 숫자 크기를 흔든다");
            yield return WaitReal(1.2f);
            Assert.AreEqual(baseScale, hatch.HpText.transform.localScale.x, 1e-3f, "끝나면 원래 크기");

            PayslipReceipt pay = Object.FindAnyObjectByType<PayslipReceipt>(FindObjectsInactive.Include);
            Assert.IsTrue(pay.HasTotalFeel, "Footer/TotalFeel 연결");
            int totalPlays = pay.TotalFeelPlays;
            var p = new PayBreakdown(0, 50, 2, _bootstrap.Config.CleanReward);
            GameEvents.RaiseWaveCleared(0, p);
            Assert.IsTrue(pay.IsPlaying, "영수증 재생");
            float limit = Time.realtimeSinceStartup + 4f;
            while (Time.realtimeSinceStartup < limit && pay.TotalFeelPlays == totalPlays) yield return null;
            Assert.AreEqual(totalPlays + 1, pay.TotalFeelPlays, "합계 굴림 끝 → 숫자 펀치 1번");
        }

        // ── 할당 · Instantiate · 금지 ─────────────────────────

        [UnityTest]
        public IEnumerator Repeat200_NoObjectOrHeapGrowth_NoForbiddenFeedback()
        {
            int bad = FeelMomentsFx.CountForbiddenInScene(out int players);
            Assert.GreaterOrEqual(players, FeelMomentsFx.MomentCount + 3, "전투 6 + UI 3 MMF_Player");
            Assert.AreEqual(0, bad, "timescale · FreezeFrame · 카메라 shaker 0 (S3 규칙 9)");

            SlimeView s = Slime(3);
            HudHatch hatch = Object.FindAnyObjectByType<HudHatch>();
            int max = _loop.Flow.Hatch.MaxHp;
            // 데우기 — 한 바퀴씩 (첫 재생의 Feel 내부 준비)
            for (int i = 0; i < 20; i++)
            {
                RaiseAll(s, 7);
                GameEvents.RaiseHatchHpChanged(max - 1 - (i & 1), max);
                yield return null;
            }
            yield return WaitReal(0.8f);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            int objects = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            long heap = GC.GetTotalMemory(true);

            long fx = 0;
            for (int i = 0; i < 200; i++)
            {
                long a = GC.GetAllocatedBytesForCurrentThread();
                RaiseAll(s, 7);
                fx += GC.GetAllocatedBytesForCurrentThread() - a;
                GameEvents.RaiseHatchHpChanged(max - 1 - (i & 1), max);
                yield return null;
            }
            yield return WaitReal(1.2f); // 입자 · 스프링 끝
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            int objectsAfter = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            long heapAfter = GC.GetTotalMemory(true);
            Debug.Log("[U18b] 200회 × 전투 순간 6 — 재생 경로 할당 " + fx + " B (회당 " + (fx / 200) + " B) · 힙 " + heap + " → " + heapAfter + " · 오브젝트 " + objects + " → " + objectsAfter);
            Assert.AreEqual(objects, objectsAfter, "Instantiate 0 — 오브젝트 수 그대로");
            Assert.LessOrEqual(heapAfter - heap, 64 * 1024, "200회 반복해도 GC 힙이 늘지 않음 (잡음 64KB 이내)");
            Assert.AreEqual(0L, fx, "전투 순간 재생 (우리 코드 + MMF_Particles · Kick) 할당 0");
            _slimes.Despawn(s, DespawnReason.Killed);
        }
    }
}
