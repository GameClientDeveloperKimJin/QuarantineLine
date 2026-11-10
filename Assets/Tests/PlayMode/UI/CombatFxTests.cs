using System.Collections;
using System.Reflection;
using NUnit.Framework;
using QL.Core;
using QL.FX;
using QL.FX.Combat;
using QL.FX.Motion;
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
            yield return null; // LateUpdate 에서 처치와 짝지어 발사

            Assert.AreEqual(1, _coins.FlyingCount, "처치 1 → 코인 1개");
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
        public IEnumerator WaveBonus_ScattersCoins_FromCenter()
        {
            yield return WaitCoinsSettled();
            int bonus = 50;
            _bootstrap.Wallet.Add(bonus);
            Raise("RaiseCoinChanged", Wallet, bonus);
            yield return null;
            Assert.AreEqual(Mathf.Min(Fx.coinBonusCount, Fx.coinFlyMax), _coins.FlyingCount, "보너스 = 코인 여러 개");
            Assert.AreEqual(bonus, _coins.Pending, "몫 합 = 보너스");

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

        [UnityTest]
        public IEnumerator TwoHundredPlays_NoObjectGrowth()
        {
            int numbers = _numbers.CreatedCount;
            int doubles = _death.CreatedCount;
            int coins = _coins.CreatedCount;
            int flames = _burn.CreatedCount;
            int fxItems = _pool.CreatedCount;
            int childCount = _numbers.transform.GetComponentsInChildren<Transform>(true).Length;
            int poolChildren = _pool.transform.GetComponentsInChildren<Transform>(true).Length;

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
            Assert.AreEqual(childCount, _numbers.transform.GetComponentsInChildren<Transform>(true).Length, "CombatFx 아래 오브젝트 수 그대로");
            Assert.AreEqual(poolChildren, _pool.transform.GetComponentsInChildren<Transform>(true).Length, "FxPool 아래 오브젝트 수 그대로");
            Assert.GreaterOrEqual(_death.PlayCount, 200);
            Assert.LessOrEqual(_numbers.ActiveCount(), _numbers.PoolSize);
            Assert.LessOrEqual(_coins.FlyingCount, Fx.coinFlyMax);

            yield return WaitCoinsSettled();
            Assert.AreEqual(Wallet, _hud.Shown, "몰아쳐도 결국 표시 = 지갑");
        }
    }
}
