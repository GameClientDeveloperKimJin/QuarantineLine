using System.Collections;
using System.Reflection;
using NUnit.Framework;
using QL.Core;
using QL.FX;
using QL.FX.Combat;
using QL.FX.Motion;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Combat;
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
    /// U05 처치 · 보급 연출 — Game.unity 를 열고 SlimeRegistry.Spawn / Despawn(Killed) (실제 KillRewarder → CoinChanged 흐름) ·
    /// GameEvents 리플렉션 발행으로 데미지 숫자 · 대역 · 코인 비행 · 잔열 불꽃을 본다. DroneSeeder · DragController 는 끈다.
    /// 시간은 흐른다 (timeScale 1, 흐름은 Prep).
    /// </summary>
    public sealed class CombatFxTests
    {
        private const string SceneName = "Game";
        private const int MaxWaitFrames = 300;
        private const float Slack = 0.3f;

        private GameBootstrap _bootstrap;
        private SlimeRegistry _slimes;
        private FxPool _pool;
        private DamageNumberFx _numbers;
        private SlimeDeathFx _death;
        private CoinFlyFx _coins;
        private BurnFx _burn;
        private HudCoin _hud;

        private FxConfig Fx => _pool.Config;
        private int Wallet => _bootstrap.Wallet.Coins;

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
            yield return null; // HudCoin.Start (CoinFlyFx 연결)

            _bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            _slimes = Object.FindAnyObjectByType<SlimeRegistry>();
            _pool = Object.FindAnyObjectByType<FxPool>();
            _numbers = Object.FindAnyObjectByType<DamageNumberFx>();
            _death = Object.FindAnyObjectByType<SlimeDeathFx>();
            _coins = Object.FindAnyObjectByType<CoinFlyFx>();
            _burn = Object.FindAnyObjectByType<BurnFx>();
            _hud = Object.FindAnyObjectByType<HudCoin>();
            Assert.IsNotNull(_numbers, "[FX] 아래 CombatFx(DamageNumberFx) 가 없다");
            Assert.IsNotNull(_death, "[FX] 아래 CombatFx(SlimeDeathFx) 가 없다");
            Assert.IsNotNull(_coins, "[FX] 아래 CombatFx(CoinFlyFx) 가 없다");
            Assert.IsNotNull(_burn, "[FX] 아래 CombatFx(BurnFx) 가 없다");
            Assert.IsNotNull(_hud);
            Assert.AreSame(_hud.CoinIcon, _coins.Target, "HudCoin 이 코인 아이콘을 비행 목표로 넣는다");
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

        private IEnumerator WaitCoinsSettled()
        {
            float end = Time.time + 5f;
            while (Time.time < end && (_coins.FlyingCount > 0 || _coins.Pending > 0 || _hud.IsRolling)) yield return null;
        }

        private static Transform ActiveModel(SlimeView s)
        {
            Transform a = s.transform.Find("Model_A");
            if (a != null && a.gameObject.activeSelf) return a;
            Transform b = s.transform.Find("Model_B");
            return b != null && b.gameObject.activeSelf ? b : null;
        }

        // ── 1. 데미지 숫자 ───────────────────────────────────

        [UnityTest]
        public IEnumerator SameSlime_TwoHitsWithinWindow_OneNumberSummed()
        {
            SlimeView s = _slimes.Spawn(SlimeType.Mutant, 2);
            yield return null;
            int before = _numbers.ActiveCount();

            Raise("RaiseSlimeDamaged", s, 6, DroneType.Plasma);
            Raise("RaiseSlimeDamaged", s, 7, DroneType.Plasma);
            Assert.AreEqual(before + 1, _numbers.ActiveCount(), "0.12초 안 2타 → 숫자 1개");
            DamageNumber n = _numbers.ActiveFor(s);
            Assert.IsNotNull(n);
            Assert.AreEqual(13, n.Value, "합산 값");
            Assert.AreEqual("13", n.Text.text);
            Assert.GreaterOrEqual(n.Text.fontSize, 24f, "UI 글자 최소 24px (S3 규칙 8)");

            yield return Wait(Fx.damageMergeWindow + 0.05f);
            Raise("RaiseSlimeDamaged", s, 5, DroneType.Suction);
            Assert.AreEqual(before + 2, _numbers.ActiveCount(), "합산 창이 지나면 새 숫자");

            SlimeView other = _slimes.Spawn(SlimeType.Mutant, 3);
            Raise("RaiseSlimeDamaged", other, Fx.damageBigThreshold, DroneType.Suction);
            DamageNumber big = _numbers.ActiveFor(other);
            Assert.AreEqual(Fx.damageBigFontSize, big.Text.fontSize, 0.01f, "큰 피해 = 큰 글자");
            Assert.AreNotEqual(Fx.damageColor, big.Text.color, "큰 피해 = 종류색");

            yield return Wait(Fx.damageDuration + Slack);
            Assert.AreEqual(0, _numbers.ActiveCount(), "수명이 끝나면 모두 꺼진다");
        }

        // ── 2. 납작 터짐 — 대역 ──────────────────────────────

        [UnityTest]
        public IEnumerator SlimeKilled_DoubleUsesActiveModelMesh_RootUnchanged()
        {
            SlimeView s = _slimes.Spawn(SlimeType.Blob, 1);
            yield return Wait(0.1f);
            Transform model = ActiveModel(s);
            Assert.IsNotNull(model, "켜진 Model_A/B");
            MeshFilter mf = model.GetComponentInChildren<MeshFilter>();
            MeshRenderer mr = model.GetComponentInChildren<MeshRenderer>();
            Vector3 rootPos = s.transform.localPosition;
            Quaternion rootRot = s.transform.localRotation;
            Vector3 rootScale = s.transform.localScale;
            Vector3 modelPos = model.localPosition;
            Quaternion modelRot = model.localRotation;
            Vector3 world = mf.transform.position;

            _slimes.Despawn(s, DespawnReason.Killed);

            SlimeDouble d = _death.Last;
            Assert.IsNotNull(d, "SlimeKilled → 대역");
            Assert.IsTrue(d.Active);
            Assert.AreSame(mf.sharedMesh, d.Mesh, "켜진 Model 의 mesh");
            Assert.AreSame(mr.sharedMaterial, d.Material, "켜진 Model 의 재질 (공유)");
            Assert.Less(Vector3.Distance(world, d.transform.position), 1e-4f, "그 자리에서");
            Assert.AreEqual(rootPos, s.transform.localPosition, "점액 루트 위치 그대로");
            Assert.AreEqual(rootRot, s.transform.localRotation, "점액 루트 회전 그대로");
            Assert.AreEqual(rootScale, s.transform.localScale, "점액 루트 크기 그대로");
            Assert.AreEqual(modelPos, model.localPosition, "Model 위치 그대로");
            Assert.AreEqual(modelRot, model.localRotation, "Model 회전 그대로");
            Assert.GreaterOrEqual(_pool.ActiveCount(FxKind.Splat), 1, "바닥 Splat");

            yield return Wait(Fx.deathSquashDuration + 0.02f);
            Assert.Less(d.transform.localScale.y, d.BaseScale.y * (Fx.deathSquashY + 0.05f), "세로 납작");
            Assert.Greater(d.transform.localScale.x, d.BaseScale.x * (Fx.deathSquashXZ - 0.05f), "가로 퍼짐");
            Assert.GreaterOrEqual(_pool.ActiveCount(FxKind.Puddle), 1, "Puddle 이어짐");

            yield return Wait(d.TotalDuration + Slack);
            Assert.IsFalse(d.Active, "끝나면 꺼짐");
        }

        // ── 3. 코인 ───────────────────────────────────────────

        [UnityTest]
        public IEnumerator Kill_CoinFlies_ShownCatchesUpOnArrival()
        {
            yield return WaitCoinsSettled();
            int shownBefore = _hud.Shown;
            Assert.AreEqual(Wallet, shownBefore);

            SlimeView s = _slimes.Spawn(SlimeType.Blob, 0);
            int reward = s.Reward;
            _slimes.Despawn(s, DespawnReason.Killed);
            Assert.AreEqual(shownBefore + reward, Wallet, "지갑은 바로 바뀐다 (KillRewarder)");
            Assert.AreEqual(1, _coins.FlyingCount, "처치 1 → KillRewarded 그 자리에서 코인 1개 (U14)");
            yield return null;

            Assert.AreEqual(shownBefore, _hud.Shown, "날아가는 동안 표시는 그대로");
            Assert.AreEqual(Wallet - reward, _hud.Target);

            float end = Time.time + 3f;
            while (Time.time < end && _coins.FlyingCount > 0) yield return null;
            Assert.AreEqual(0, _coins.FlyingCount, "도착");
            Assert.IsTrue(_hud.IsIconPunching, "도착 순간 아이콘 펀치");
            Assert.AreEqual(Wallet, _hud.Target, "도착하면 그 몫만큼 굴림 시작");

            yield return WaitCoinsSettled();
            Assert.AreEqual(Wallet, _hud.Shown, "도착 뒤 표시 = 지갑");
            Assert.AreEqual(Wallet.ToString(), _hud.CoinText.text);
        }

        [UnityTest]
        public IEnumerator ManyKillsSameFrame_OverCap_RestDeliveredImmediately()
        {
            yield return WaitCoinsSettled();
            int cap = Fx.coinFlyMax;
            int kills = cap + 4;
            var list = new SlimeView[kills];
            for (int i = 0; i < kills; i++) list[i] = _slimes.Spawn(SlimeType.Blob, i % 5);
            int reward = list[0].Reward;
            int immediateBefore = _coins.ImmediateTotal;
            for (int i = 0; i < kills; i++) _slimes.Despawn(list[i], DespawnReason.Killed);
            yield return null;

            Assert.AreEqual(cap, _coins.FlyingCount, "동시 코인 상한");
            Assert.AreEqual(4 * reward, _coins.ImmediateTotal - immediateBefore, "넘친 몫은 날리지 않고 바로 반영");
            Assert.AreEqual(cap * reward, _coins.Pending, "날고 있는 몫만 대기");
            Assert.AreEqual(Wallet - cap * reward, _hud.Target);

            yield return WaitCoinsSettled();
            Assert.AreEqual(Wallet, _hud.Shown, "모두 도착하면 표시 = 지갑");
        }

        [UnityTest]
        public IEnumerator WaveBonus_NoScatter_RollsImmediatelyWithoutReceipt()
        {
            // U14: 웨이브 수당 흩뿌림은 없앴다 — 코인 비행은 KillRewarded 만. 영수증이 붙잡지 않은 CoinChanged(+) 는 바로 굴림
            yield return WaitCoinsSettled();
            int bonus = 50;
            int launched = _coins.LaunchCount;
            _bootstrap.Wallet.Add(bonus);
            Raise("RaiseCoinChanged", Wallet, bonus);
            Assert.AreEqual(0, _coins.Pending, "수당은 비행 몫이 아니다");
            Assert.AreEqual(Wallet, _hud.Target, "붙잡은 몫 없음 → 바로 지갑 값으로");
            Assert.IsTrue(_hud.IsRolling);
            yield return null;
            Assert.AreEqual(0, _coins.FlyingCount, "흩뿌림 없음");
            Assert.AreEqual(launched, _coins.LaunchCount);

            yield return WaitCoinsSettled();
            Assert.AreEqual(Wallet, _hud.Shown);
        }

        [UnityTest]
        public IEnumerator KillRewarded_LaunchesFromSlime_NoKillCoinPairing()
        {
            yield return WaitCoinsSettled();
            // ① KillRewarded 하나만 → 그 자리에서 코인 1개 (프레임 끝 짝짓기 없음)
            SlimeView s = _slimes.Spawn(SlimeType.Blob, 2);
            Raise("RaiseKillRewarded", s, 7);
            Assert.AreEqual(1, _coins.FlyingCount, "KillRewarded 즉시 발사");
            Assert.AreEqual(7, _coins.Pending, "몫 = reward 인자");
            yield return WaitCoinsSettled();
            Assert.AreEqual(0, _coins.Pending);

            // ② KillRewarder 를 끄고 SlimeKilled + 같은 값 CoinChanged → 날지 않는다 (보상액 추정 짝짓기 없음 · 보류함 21)
            KillRewarder rewarder = Object.FindAnyObjectByType<KillRewarder>();
            Assert.IsNotNull(rewarder);
            rewarder.enabled = false;
            int reward = s.Reward;
            int launched = _coins.LaunchCount;
            Raise("RaiseSlimeKilled", s);
            _bootstrap.Wallet.Add(reward);
            Raise("RaiseCoinChanged", Wallet, reward);
            yield return null;
            yield return null;
            Assert.AreEqual(launched, _coins.LaunchCount, "SlimeKilled · CoinChanged 만으로는 코인이 날지 않는다");
            Assert.AreEqual(0, _coins.FlyingCount);
            rewarder.enabled = true;
            _slimes.Despawn(s, DespawnReason.Killed);
            yield return WaitCoinsSettled();
            Assert.AreEqual(Wallet, _hud.Shown);
        }

        [UnityTest]
        public IEnumerator GameEnded_FlyingCoinsFlushed_ShownEqualsWallet()
        {
            yield return WaitCoinsSettled();
            for (int i = 0; i < 3; i++) _slimes.Despawn(_slimes.Spawn(SlimeType.Blob, i), DespawnReason.Killed);
            yield return null;
            Assert.Greater(_coins.FlyingCount, 0);
            Assert.AreNotEqual(Wallet, _hud.Shown);

            Raise("RaiseGameEnded", default(ResultSummary));
            Assert.AreEqual(0, _coins.FlyingCount, "판 끝 → 날던 코인 즉시 반영");
            Assert.AreEqual(0, _coins.Pending);
            Assert.AreEqual(Wallet, _hud.Shown, "결과 화면 전에 표시 = 지갑");
            Assert.IsFalse(_hud.IsRolling, "굴림 없이 바로");
            Assert.AreEqual(Wallet.ToString(), _hud.CoinText.text);
        }

        [UnityTest]
        public IEnumerator GameEnded_DamageNumbersOff_PausedKeepsThem_NewRoundShowsAgain()
        {
            var views = new SlimeView[3];
            for (int i = 0; i < views.Length; i++) views[i] = _slimes.Spawn(SlimeType.Blob, i);
            yield return null;
            for (int i = 0; i < views.Length; i++) Raise("RaiseSlimeDamaged", views[i], 10, DroneType.Suction);
            Assert.AreEqual(3, _numbers.ActiveCount(), "숫자 3개 뜸");
            yield return Wait(0.2f);

            // 휴게 (Paused · timeScale 0) — 숫자는 그대로 멈춰 있다
            GameLoop loop = Object.FindAnyObjectByType<GameLoop>();
            loop.HandleEscape();
            Assert.AreEqual(GameState.Paused, loop.Flow.State);
            float p0 = _numbers.ActiveFor(views[0]).Progress;
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(3, _numbers.ActiveCount(), "휴게 중에는 숫자를 끄지 않는다");
            Assert.AreEqual(p0, _numbers.ActiveFor(views[0]).Progress, 1e-5f, "휴게 중 숫자는 멈춤 (게임 시간)");
            loop.HandleEscape();
            Assert.AreEqual(1f, Time.timeScale);

            // 판 끝 — 떠 있는 숫자를 즉시 전부 끈다 (결과 화면 뒤에 얼어붙지 않게, U13)
            Raise("RaiseGameEnded", default(ResultSummary));
            Assert.AreEqual(0, _numbers.ActiveCount(), "판 끝 → 숫자 0");
            for (int i = 0; i < _numbers.PoolSize; i++)
            {
                DamageNumber n = _numbers.ItemAt(i);
                Assert.IsFalse(n.gameObject.activeSelf, "숫자 " + i + " 비활성");
                Assert.IsNull(n.View, "숫자 " + i + " 점액 연결 끊김");
                Assert.AreEqual(Vector3.one, n.Text.transform.localScale, "숫자 " + i + " Punch 원복");
            }

            // 다시 하기 → 새 판에서 숫자가 정상으로 뜬다
            Time.timeScale = 1f;
            loop.Restart();
            DamageNumberFx fresh = null;
            SlimeRegistry slimes = null;
            for (int i = 0; i < MaxWaitFrames; i++)
            {
                yield return null;
                GameLoop l = Object.FindAnyObjectByType<GameLoop>();
                if (l != null && l != loop && l.IsReady)
                {
                    fresh = Object.FindAnyObjectByType<DamageNumberFx>();
                    slimes = Object.FindAnyObjectByType<SlimeRegistry>();
                    break;
                }
            }
            Assert.IsNotNull(fresh, "새 판 DamageNumberFx");
            Assert.AreEqual(0, fresh.ActiveCount());
            SlimeView v = slimes.Spawn(SlimeType.Blob, 1);
            yield return null;
            Raise("RaiseSlimeDamaged", v, 10, DroneType.Suction);
            Assert.AreEqual(1, fresh.ActiveCount(), "새 판 숫자 뜸");
            yield return Wait(0.2f);
            Assert.Greater(fresh.ActiveFor(v).Progress, 0f, "새 판 숫자가 움직인다");
        }

        [UnityTest]
        public IEnumerator Spend_RollsImmediately_EvenWhileCoinsFly()
        {
            yield return WaitCoinsSettled();
            SlimeView s = _slimes.Spawn(SlimeType.Blob, 4);
            int reward = s.Reward;
            _slimes.Despawn(s, DespawnReason.Killed);
            yield return null;
            Assert.AreEqual(1, _coins.FlyingCount);

            Assert.IsTrue(_bootstrap.Wallet.TrySpend(10));
            Raise("RaiseCoinChanged", Wallet, -10);
            Assert.AreEqual(Wallet - reward, _hud.Target, "지출은 바로 굴림 (날고 있는 몫은 빼고)");
            Assert.IsTrue(_hud.IsRolling);

            yield return WaitCoinsSettled();
            Assert.AreEqual(Wallet, _hud.Shown);
        }

        // ── 4. 잔열 불꽃 ─────────────────────────────────────

        [UnityTest]
        public IEnumerator Burning_ShowsFlame_EndsHides()
        {
            SlimeView a = _slimes.Spawn(SlimeType.Mutant, 1);
            SlimeView b = _slimes.Spawn(SlimeType.Mutant, 3);
            yield return null;
            Assert.AreEqual(0, _burn.ActiveCount);

            a.Combat.ApplyBurn(1f, 10f);
            Assert.IsTrue(a.Combat.Burning, "SlimeCombat.Burning (U05 읽기 API)");
            yield return null;
            Assert.AreEqual(1, _burn.ActiveCount, "잔열 걸린 점액 → 불꽃 1");
            BurnFlame f = _burn.FlameAt(0);
            Assert.AreEqual(a.Id, f.TargetId);
            Assert.IsNull(f.transform.parent == a.transform ? f : null, "점액에 붙이지 않는다");
            Vector3 p0 = f.transform.position;
            Assert.Less(Mathf.Abs(p0.x - a.transform.position.x), 0.5f, "점액 위치를 따라간다");
            Transform ring = f.transform.Find("Floor");
            Assert.IsNotNull(ring, "발밑 링");
            Assert.AreEqual(Fx.worldFxHeight, ring.position.y, 1e-4f, "바닥에 눕힌 링");
            Assert.Less(Vector2.Distance(new Vector2(ring.position.x, ring.position.z), new Vector2(a.transform.position.x, a.transform.position.z)), 0.05f, "점액 발밑 (한 프레임 이동 이내 — 링은 LateUpdate, 점액은 다음 Update 에 움직임)");

            b.Combat.ApplyBurn(1f, 10f);
            yield return null;
            Assert.AreEqual(2, _burn.ActiveCount);

            a.Combat.Tick(10f); // 잔열 끝
            Assert.IsFalse(a.Combat.Burning);
            yield return null;
            Assert.AreEqual(1, _burn.ActiveCount, "잔열 끝 → 그 불꽃 사라짐");

            _slimes.Despawn(b, DespawnReason.Killed);
            yield return null;
            Assert.AreEqual(0, _burn.ActiveCount, "처치 → 목록에서 빠짐");
        }

        // ── 5. 풀 재사용 ─────────────────────────────────────

        /// <summary>
        /// 풀이 만든 오브젝트 수 — TMP 하위 메시(TMP_SubMeshUI · TMP_SubMesh)는 뺀다.
        /// Dynamic 폰트 아틀라스가 2장째로 넘어가면 TMP 가 글자마다 「TMP SubMeshUI [… Atlas 1]」 자식을 만든다 (보류함 25 실측) — 풀 누수가 아니라
        /// 앞 테스트들이 채운 아틀라스 상태에 따라 생겨 개수가 191 ↔ 192 로 흔들렸다. 풀 재사용 확인은 그대로 (풀 오브젝트 · CreatedCount).
        /// </summary>
        private static int CountObjects(Transform root)
        {
            int n = 0;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.GetComponent<TMPro.TMP_SubMeshUI>() == null && t.GetComponent<TMPro.TMP_SubMesh>() == null) n++;
            return n;
        }

        /// <summary>실패 메시지용 — before 에 없던 자식 (이름 · 부모 · 컴포넌트 형식).</summary>
        private static string NewObjects(Transform root, System.Collections.Generic.HashSet<Transform> before)
        {
            var sb = new System.Text.StringBuilder();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (before.Contains(t)) continue;
                sb.Append(" | 새 오브젝트 ").Append(t.name).Append(" (부모 ").Append(t.parent != null ? t.parent.name : "-").Append(") :");
                foreach (Component c in t.GetComponents<Component>()) sb.Append(' ').Append(c.GetType().FullName);
            }
            return sb.ToString();
        }

        [UnityTest]
        public IEnumerator TwoHundredPlays_NoObjectGrowth()
        {
            int numbers = _numbers.CreatedCount;
            int doubles = _death.CreatedCount;
            int coins = _coins.CreatedCount;
            int flames = _burn.CreatedCount;
            int fxItems = _pool.CreatedCount;
            int childCount = CountObjects(_numbers.transform);
            var beforeSet = new System.Collections.Generic.HashSet<Transform>(_numbers.transform.GetComponentsInChildren<Transform>(true));
            var poolBefore = new System.Collections.Generic.HashSet<Transform>(_pool.transform.GetComponentsInChildren<Transform>(true));
            int poolChildren = CountObjects(_pool.transform);

            for (int i = 0; i < 200; i++)
            {
                SlimeView s = _slimes.Spawn(SlimeType.Blob, i % 5);
                Raise("RaiseSlimeDamaged", s, 10, DroneType.Suction);
                s.Combat.ApplyBurn(1f, 1f);
                _slimes.Despawn(s, DespawnReason.Killed);
                if (i % 10 == 9) yield return null;
            }
            yield return null;

            Assert.AreEqual(numbers, _numbers.CreatedCount);
            Assert.AreEqual(doubles, _death.CreatedCount);
            Assert.AreEqual(coins, _coins.CreatedCount);
            Assert.AreEqual(flames, _burn.CreatedCount);
            Assert.AreEqual(fxItems, _pool.CreatedCount);
            Assert.AreEqual(childCount, CountObjects(_numbers.transform), "CombatFx 아래 오브젝트 수 그대로" + NewObjects(_numbers.transform, beforeSet));
            Assert.AreEqual(poolChildren, CountObjects(_pool.transform), "FxPool 아래 오브젝트 수 그대로" + NewObjects(_pool.transform, poolBefore));
            Assert.GreaterOrEqual(_death.PlayCount, 200);
            Assert.LessOrEqual(_numbers.ActiveCount(), _numbers.PoolSize);
            Assert.LessOrEqual(_coins.FlyingCount, Fx.coinFlyMax);

            yield return WaitCoinsSettled();
            Assert.AreEqual(Wallet, _hud.Shown, "몰아쳐도 결국 표시 = 지갑");
        }
    }
}
