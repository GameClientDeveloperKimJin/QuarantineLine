using System;
using System.Collections;
using NUnit.Framework;
using QL.Core;
using QL.Core.Combat;
using QL.Core.Flow;
using QL.Core.Support;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Drones;
using QL.Game.Flow;
using QL.Game.Input;
using QL.Game.Slimes;
using QL.Game.Stage;
using QL.Game.Summon;
using QL.UI.Hud;
using QL.UI.Support;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace QL.Tests.PlayMode.UI
{
    /// <summary>
    /// U19 업무 지원 카드 — 선택 화면 · 승인 도장 · HUD 칩 · C14 종류 선택 창 · C13 보험 처리.
    /// Game.unity 를 열고 (DroneSeeder 끔) timeScale 0 에서 GameLoop.Step 으로 웨이브 1 을 막는다 (점액은 바로 처치). 그 뒤 영수증(게임 시간 2초)을 보려고 timeScale 1 —
    /// 선택 중엔 준비 시간이 멈춰 있어 웨이브가 시작되지 않는다. 선택 화면 연출은 실제 시간.
    /// 원하는 카드는 SupportCardRunner.OverrideOffer (테스트 전용) 로 제시 자리에 넣는다 — 화면은 영수증이 끝난 뒤 여므로 그 전에 바꾸면 화면에 그대로 뜬다.
    /// 1 · 2 · 3 키는 <see cref="SupportSelectScreen.Choose"/> 와 같은 길이라 Choose 로 누른다 (클릭도 같은 길).
    /// </summary>
    public sealed class SupportScreenTests
    {
        private const string SceneName = "Game";
        private const int MaxWaitFrames = 300;
        private const int MaxSteps = 20000;
        private const float Dt = 0.05f;

        private GameLoop _loop;
        private GameBootstrap _bootstrap;
        private SlimeRegistry _registry;
        private SummonController _summon;
        private SupportSelectScreen _screen;
        private SupportChipBar _bar;
        private SummonTypePicker _picker;
        private InsuranceFlash _insurance;
        private PayslipReceipt _payslip;
        private WaveOrderCard _order;
        private HudStartButton _start;
        private HudCountdown _countdown;
        private GuidePrefsGuard _guidePrefs;
        private int _picked;
        private int _offered;

        private GameFlow Flow => _loop.Flow;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _guidePrefs = GuidePrefsGuard.Save(); // F03 — QL.GuideDone 원복
            yield return LoadGame();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _guidePrefs.Restore();
            SceneManager.sceneLoaded -= DisableSeeder;
            GameEvents.SupportPicked -= OnPicked;
            GameEvents.SupportOffered -= OnOffered;
            Time.timeScale = 1f;
            GameEvents.ClearAll();
            yield return null;
        }

        // ── 도우미 ───────────────────────────────────────────

        private IEnumerator LoadGame()
        {
            GameLoop old = Object.FindAnyObjectByType<GameLoop>();
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
                if (found != null && found != old && found.IsReady && stage != null && stage.IsBuilt && summon != null && summon.IsReady) break;
            }
            SceneManager.sceneLoaded -= DisableSeeder;
            Assert.IsTrue(found != null && found.IsReady, "GameLoop 가 준비되지 않았다");
            _loop = found;
            _bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            _registry = Object.FindAnyObjectByType<SlimeRegistry>();
            _summon = Object.FindAnyObjectByType<SummonController>();
            _screen = Object.FindAnyObjectByType<SupportSelectScreen>();
            _bar = Object.FindAnyObjectByType<SupportChipBar>();
            _picker = Object.FindAnyObjectByType<SummonTypePicker>();
            _insurance = Object.FindAnyObjectByType<InsuranceFlash>();
            _payslip = Object.FindAnyObjectByType<PayslipReceipt>();
            _order = Object.FindAnyObjectByType<WaveOrderCard>();
            _start = Object.FindAnyObjectByType<HudStartButton>();
            _countdown = Object.FindAnyObjectByType<HudCountdown>();
            Assert.IsNotNull(_screen, "UIRoot/SupportScreen/Select 에 SupportSelectScreen 이 없다");
            Assert.IsNotNull(_bar, "InGameScreen/Hud/SupportChips 에 SupportChipBar 가 없다");
            Assert.IsNotNull(_picker, "UIRoot/SupportScreen/SummonPick 에 SummonTypePicker 가 없다");
            Assert.IsNotNull(_insurance, "HatchPanel/Insurance 에 InsuranceFlash 가 없다");
            Object.FindAnyObjectByType<DragController>().enabled = false; // 실제 마우스가 끼어들지 않게
            Time.timeScale = 0f; // GameLoop.Update 의 Step(0) 은 아무것도 하지 않는다
            _picked = 0;
            _offered = 0;
            GameEvents.SupportPicked += OnPicked;
            GameEvents.SupportOffered += OnOffered;
        }

        private void OnPicked(SupportCardSpec card, int stack) => _picked++;
        private void OnOffered(int wave, SupportCardSpec[] cards) => _offered++;

        private static void DisableSeeder(Scene scene, LoadSceneMode mode)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                DroneSeeder[] seeders = roots[i].GetComponentsInChildren<DroneSeeder>(true);
                for (int j = 0; j < seeders.Length; j++) seeders[j].enabled = false;
            }
        }

        private void KillAllAlive()
        {
            SlimeTarget[] targets = _registry.Targets;
            for (int k = _registry.TargetCount - 1; k >= 0; k--)
                if (_registry.TryGet(targets[k].Id, out SlimeView s)) _registry.Despawn(s, DespawnReason.Killed);
        }

        /// <summary>지금 웨이브를 열고 점액을 바로 처치해 클리어 (timeScale 0 · Step).</summary>
        private void ClearCurrentWave()
        {
            for (int i = 0; i < MaxSteps && Flow.State != GameState.Wave; i++) _loop.Step(Dt);
            Assert.AreEqual(GameState.Wave, Flow.State, "웨이브가 시작되지 않았다");
            for (int i = 0; i < MaxSteps && Flow.State == GameState.Wave; i++)
            {
                _loop.Step(Dt);
                KillAllAlive();
            }
            Assert.AreEqual(GameState.Prep, Flow.State, "웨이브를 막지 못했다");
        }

        private static IEnumerator WaitReal(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        private static IEnumerator WaitUntil(Func<bool> done, float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!done() && Time.realtimeSinceStartup < end) yield return null;
        }

        private int CardIndex(string id)
        {
            int i = _loop.Support.Modifiers.IndexOf(id);
            Assert.GreaterOrEqual(i, 0, "카드 풀에 " + id + " 가 없다");
            return i;
        }

        /// <summary>웨이브 클리어 → (원하면 제시 0번 자리를 id 로) → 영수증 끝 → 선택 화면이 열릴 때까지.</summary>
        private IEnumerator ClearAndOpen(string slot0Id = null)
        {
            ClearCurrentWave();
            Assert.IsTrue(_loop.IsChoosingSupport, "클리어 뒤 카드 선택 대기");
            if (slot0Id != null) Assert.IsTrue(_loop.Support.OverrideOffer(0, CardIndex(slot0Id)));
            Time.timeScale = 1f; // 영수증 (게임 시간) — 선택 중엔 준비 시간이 멈춰 웨이브가 안 온다
            yield return WaitUntil(() => _screen.IsShowing, _payslip.Duration + 3f); // U21 — 영수증 길이 (FxConfig payslipEndAt) 기준
            Assert.IsTrue(_screen.IsShowing, "영수증 뒤 선택 화면이 열리지 않았다");
        }

        /// <summary>선택 화면에서 slot 을 고르고 연출 끝 (PickSupport) 까지.</summary>
        private IEnumerator ChooseAndWait(int slot)
        {
            Assert.IsTrue(_screen.Choose(slot));
            yield return WaitUntil(() => !_screen.IsShowing, _screen.PickDuration + 2f);
            Assert.IsFalse(_screen.IsShowing, "연출 끝에 닫히지 않았다");
        }

        // ── 열림 · 내용 ──────────────────────────────────────

        [UnityTest]
        public IEnumerator Wave1Clear_OpensAfterPayslip_CardsMatchOffer()
        {
            ClearCurrentWave();
            Assert.AreEqual(1, _offered, "SupportOffered 1번");
            Assert.IsTrue(_payslip.IsPlaying, "영수증이 먼저 돈다");
            Assert.IsTrue(_screen.IsWaiting, "영수증이 끝날 때까지 기다림");
            Assert.IsFalse(_screen.IsShowing);
            Assert.IsTrue(_order.IsWaiting, "작업 지시서도 기다림");
            Assert.IsFalse(_countdown.IsHeldShown, "선택 대기는 「연습 중」 이 아니다");
            Assert.IsTrue(_countdown.IsChoosingHold);

            Time.timeScale = 1f;
            yield return WaitUntil(() => _screen.IsShowing, _payslip.Duration + 3f); // U21 — 영수증 길이 (FxConfig payslipEndAt) 기준
            Assert.IsTrue(_screen.IsShowing);
            Assert.IsFalse(_payslip.IsPlaying, "영수증이 끝난 뒤 열림");
            Assert.IsTrue(_order.IsWaiting, "선택 중엔 지시서가 아직");
            yield return null;
            Assert.IsFalse(_start.IsShown, "선택 중 HudStartButton 숨김");

            SupportCardSpec[] offer = _loop.CurrentOffer;
            Assert.AreEqual(offer.Length, _screen.CardCount, "카드 수 = 제시 수");
            for (int i = 0; i < offer.Length; i++)
            {
                SupportCardInfo info = _bar.Table[_loop.Support.GetOfferCardIndex(i)];
                SupportCardView card = _screen.Card(i);
                Assert.AreEqual(offer[i].Id, info.Id);
                Assert.AreEqual(info.Name, card.NameLabel.text, "카드 " + i + " 이름");
                Assert.AreEqual(info.Big, card.BigLabel.text, "카드 " + i + " 큰 수치");
                Assert.AreEqual(info.CardDescription, card.DescriptionLabel.text);
                StringAssert.DoesNotContain("{value}", card.DescriptionLabel.text, "{value} 치환");
                StringAssert.Contains(offer[i].Id, card.NumberLabel.text);
                Assert.IsNotNull(card.Icon.sprite, offer[i].Id + " 아이콘 (A14)");
            }
            yield return WaitReal(0.6f);
            Assert.Greater(_screen.DimAlpha, 0.5f, "어둡게 (0.2초 뒤 끝 알파)");
            for (int i = 0; i < offer.Length; i++) Assert.AreEqual(1f, _screen.Card(i).Alpha, 0.01f, "카드 등장 끝");
        }

        [UnityTest]
        public IEnumerator Choose_FirstInputOnly_PicksOnceAtSequenceEnd_ThenOrderCard()
        {
            yield return ClearAndOpen("C01");
            int orderPlays = _order.PlayCount;

            Assert.IsTrue(_screen.Choose(0), "첫 입력");
            Assert.IsFalse(_screen.Choose(1), "연타 — 다른 카드");
            Assert.IsFalse(_screen.Choose(0), "연타 — 같은 카드");
            Assert.AreEqual(1, _screen.ChooseCount);
            Assert.AreEqual(2, _screen.IgnoredCount);
            Assert.AreEqual(0, _picked, "연출 중엔 아직 안 고름 (기획서 +0.80)");
            Assert.IsTrue(_loop.IsChoosingSupport);

            yield return WaitUntil(() => !_screen.IsShowing, _screen.PickDuration + 2f);
            Assert.AreEqual(1, _picked, "PickSupport 1번");
            Assert.AreEqual(1, _screen.CommitCount);
            Assert.IsFalse(_loop.IsChoosingSupport, "준비 시간 재개");
            Assert.AreEqual(1, _loop.Support.StacksOf(CardIndex("C01")));
            Assert.AreEqual(1, _bar.ShownCount, "칩 1개");
            Assert.AreEqual(CardIndex("C01"), _bar.Chip(0).CardIndex);
            Assert.AreEqual(1, _bar.PunchCount, "칩 도착 펀치");
            Assert.AreEqual(orderPlays + 1, _order.PlayCount, "고른 뒤 작업 지시서");
            Assert.IsFalse(_order.IsWaiting);
        }

        [UnityTest]
        public IEnumerator OtherPath_Pick_ClosesScreenImmediately()
        {
            yield return ClearAndOpen();
            Assert.IsTrue(_loop.PickSupport(1), "테스트 도우미처럼 바로 고름");
            Assert.IsFalse(_screen.IsShowing, "다른 길로 골라지면 즉시 닫힘");
            Assert.AreEqual(0, _screen.CommitCount);
        }

        // ── 칩 · 툴팁 ────────────────────────────────────────

        [UnityTest]
        public IEnumerator Chip_Tooltip_DroneValue_FromResolver()
        {
            yield return ClearAndOpen("C01");
            yield return ChooseAndWait(0);

            _bar.ShowTooltipFor(0);
            Assert.IsTrue(_bar.TooltipShown);
            SupportCardInfo info = _bar.Chip(0).Info;
            Assert.AreEqual(SupportTipKind.Drone, info.TipKind);
            float b = SupportCardInfoTable.BaseStat(_bootstrap.Catalog, info.TipType, info.TipGrade, StatKey.Damage);
            float f = _loop.Support.Resolver.Get(info.TipType, StatKey.Damage, b);
            Assert.Greater(f, b, "C01 — 흡입 공격력이 올랐다");
            string now = _bar.TipNow.text;
            StringAssert.Contains(Mathf.RoundToInt(b).ToString(), now, "기본 값");
            StringAssert.Contains(Mathf.RoundToInt(f).ToString(), now, "최종 값");
            StringAssert.Contains("C01", _bar.TipHeader.text);
            Assert.AreEqual(info.FullDescription, _bar.TipBody.text);
            _bar.ShowTooltipFor(-1);
            Assert.IsFalse(_bar.TooltipShown);
        }

        [UnityTest]
        public IEnumerator Chip_Counted_RemainingUpdates_OnSummon()
        {
            yield return ClearAndOpen("C05");
            yield return ChooseAndWait(0);
            int card = CardIndex("C05");
            int left = _loop.Support.RemainingOf(card);
            Assert.Greater(left, 1);
            Assert.IsTrue(_bar.Chip(0).BadgeShown, "횟수형 배지");
            StringAssert.Contains(left.ToString(), _bar.Chip(0).BadgeText.text);

            Assert.IsTrue(_summon.RequestSummon(), "할인 소환");
            Assert.AreEqual(left - 1, _loop.Support.RemainingOf(card));
            StringAssert.Contains((left - 1).ToString(), _bar.Chip(0).BadgeText.text, "남은 수 갱신 (ModifiersChanged)");
            _bar.ShowTooltipFor(0);
            StringAssert.Contains((left - 1).ToString(), _bar.TipNow.text, "툴팁 남은 n회");
        }

        [UnityTest]
        public IEnumerator Chip_Refresh_DuringWave_NoAlloc()
        {
            yield return ClearAndOpen("C05");
            yield return ChooseAndWait(0);
            Time.timeScale = 0f;
            Assert.IsTrue(_loop.RequestStartWave());
            for (int i = 0; i < 5 && Flow.State != GameState.Wave; i++) _loop.Step(Dt);
            Assert.AreEqual(GameState.Wave, Flow.State);
            _bar.ShowTooltipFor(0);

            var raise = typeof(GameEvents).GetMethod("RaiseModifiersChanged", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            Action fire = (Action)Delegate.CreateDelegate(typeof(Action), raise);
            for (int i = 0; i < 20; i++) fire(); // 데우기
            int refreshes = _bar.RefreshCount;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 200; i++) fire();
            long alloc = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(refreshes + 200, _bar.RefreshCount);
            Assert.AreEqual(0L, alloc, "웨이브 중 칩 · 툴팁 갱신 200회 할당 0");
        }

        // ── 일시정지 · 다시 하기 ─────────────────────────────

        [UnityTest]
        public IEnumerator Esc_Pause_ThenResume_SelectionStays()
        {
            yield return ClearAndOpen();
            _loop.HandleEscape();
            Assert.AreEqual(GameState.Paused, Flow.State, "휴게 시간");
            Assert.IsTrue(_screen.IsShowing, "선택 화면은 그대로 (휴게가 위)");
            Assert.IsFalse(_screen.Choose(0), "휴게 중엔 못 고름");
            yield return null;
            _loop.HandleEscape();
            Assert.AreEqual(GameState.Prep, Flow.State);
            Assert.IsTrue(_screen.IsShowing, "닫으면 선택 화면 그대로");
            Assert.IsTrue(_loop.IsChoosingSupport);
            yield return ChooseAndWait(2);
            Assert.AreEqual(1, _picked);
        }

        [UnityTest]
        public IEnumerator Restart_ClearsScreenAndChips()
        {
            yield return ClearAndOpen("C01");
            yield return ChooseAndWait(0);
            Assert.AreEqual(1, _bar.ShownCount);
            GameEvents.SupportPicked -= OnPicked;
            GameEvents.SupportOffered -= OnOffered;
            _loop.Restart();
            yield return LoadGame();
            yield return null;
            Assert.AreEqual(0, _bar.ShownCount, "새 판 — 칩 없음");
            Assert.IsFalse(_screen.IsShowing);
            Assert.IsFalse(_screen.IsWaiting);
        }

        // ── C14 · C13 ────────────────────────────────────────

        [UnityTest]
        public IEnumerator C14_Picker_CancelThenChoose()
        {
            yield return ClearAndOpen("C14");
            yield return ChooseAndWait(0);
            int coins = _bootstrap.Wallet.Coins;

            Assert.IsFalse(_summon.RequestSummon(), "C14 — 소환 대신 창");
            Assert.IsTrue(_picker.IsOpen, "SummonTypeRequested → 창 열림");
            AssertPickerAboveDeploy();
            _picker.Cancel();
            Assert.IsFalse(_picker.IsOpen, "닫기 = 취소");
            Assert.AreEqual(coins, _bootstrap.Wallet.Coins, "코인 그대로");

            yield return null; // 같은 프레임 연타 가드
            Assert.IsFalse(_summon.RequestSummon());
            Assert.IsTrue(_picker.IsOpen);
            _picker.Choose((int)DroneType.Cryo);
            Assert.IsFalse(_picker.IsOpen, "고르면 닫힘");
            int cryo = 0;
            for (int i = 0; i < _bootstrap.Grid.CellCount; i++)
                if (_bootstrap.Grid.Get(i).HasDrone && _bootstrap.Grid.Get(i).Type == DroneType.Cryo) cryo++;
            Assert.AreEqual(1, cryo, "고른 종류로 소환");
            Assert.AreEqual(2, _picker.OpenCount);
        }

        /// <summary>U18b — C14 창은 드론 투입 버튼 바로 위 (오른쪽 끝 맞춤) · 카운트다운 · 작업 시작 · 칩 · 메신저와 겹침 0 (크기 그대로 — 등장 Scale 제외).</summary>
        private void AssertPickerAboveDeploy()
        {
            Canvas.ForceUpdateCanvases();
            Transform hud = _countdown.transform.parent;
            Rect card = Unscaled(_picker.Card);
            Rect deploy = Unscaled((RectTransform)hud.Find("DeployButton"));
            Assert.GreaterOrEqual(card.yMin, deploy.yMax - 0.5f, "창 아래끝이 투입 버튼 위 " + card + " " + deploy);
            Assert.Less(card.yMin - deploy.yMax, 40f, "창이 버튼 바로 위 (간격 40px 미만)");
            Assert.AreEqual(deploy.xMax, card.xMax, 1f, "오른쪽 끝 맞춤");
            string[] others = { "Countdown", "StartWave/Button", "SupportChips", "OpsMessenger", "CoinPanel", "HatchPanel", "WavePanel" };
            for (int i = 0; i < others.Length; i++)
            {
                Transform t = hud.Find(others[i]);
                Assert.IsNotNull(t, others[i]);
                Rect r = Unscaled((RectTransform)t);
                bool overlap = card.xMin < r.xMax - 0.5f && r.xMin < card.xMax - 0.5f && card.yMin < r.yMax - 0.5f && r.yMin < card.yMax - 0.5f;
                Assert.IsFalse(overlap, "C14 창 ↔ " + others[i] + " 겹침 " + card + " " + r);
            }
        }

        /// <summary>화면 사각형 — 자기 localScale 은 뺀다 (등장 연출 중간값과 무관 · 피벗 기준으로 커지므로 피벗 위치에서 잰다).</summary>
        private static Rect Unscaled(RectTransform rt)
        {
            Vector3 lossy = rt.lossyScale;
            float sx = Mathf.Abs(lossy.x) / Mathf.Max(1e-4f, Mathf.Abs(rt.localScale.x));
            float sy = Mathf.Abs(lossy.y) / Mathf.Max(1e-4f, Mathf.Abs(rt.localScale.y));
            Vector2 size = new Vector2(rt.rect.width * sx, rt.rect.height * sy);
            Vector2 pivotWorld = rt.position;
            Vector2 min = pivotWorld - Vector2.Scale(rt.pivot, size);
            return new Rect(min, size);
        }

        [UnityTest]
        public IEnumerator C13_Blocked_FlashOncePerFrame()
        {
            yield return ClearAndOpen("C13");
            yield return ChooseAndWait(0);
            var raise = typeof(GameEvents).GetMethod("RaiseHatchDamageBlocked", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            raise.Invoke(null, new object[] { null });
            raise.Invoke(null, new object[] { null });
            Assert.AreEqual(1, _insurance.PlayCount, "같은 프레임 두 번 → 한 번");
            Assert.IsTrue(_insurance.Visual.gameObject.activeSelf, "방패 · 보험 처리 보임");
            yield return WaitUntil(() => !_insurance.Visual.gameObject.activeSelf, 3f);
            Assert.IsFalse(_insurance.Visual.gameObject.activeSelf, "끝나면 숨김");
        }
    }
}
