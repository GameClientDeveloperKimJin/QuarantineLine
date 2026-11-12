using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using QL.Core;
using QL.Core.Economy;
using QL.Core.Flow;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Drones;
using QL.Game.Flow;
using QL.Game.Input;
using QL.Game.Stage;
using QL.Game.Summon;
using QL.UI.Hud;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace QL.Tests.PlayMode.UI
{
    /// <summary>
    /// U02 HUD — Game.unity(UIRoot 포함)를 열고 GameLoop.Step · Wallet.Add · GameEvents 발행으로 HUD 가 값이 바뀔 때만 따라오는지 본다.
    /// GameEvents.Raise… 는 internal 이라 리플렉션으로 부른다 (QL.Game 에 InternalsVisibleTo 없음).
    /// 셋업 뒤 Time.timeScale = 0 (시간은 Step 으로). HUD 트윈은 게임 시간이라 굴림 · Fade 를 볼 때만 잠깐 1 로 둔다.
    /// </summary>
    public sealed class HudTests
    {
        private const string SceneName = "Game";
        private const int MaxWaitFrames = 300;
        private const int MaxSteps = 20000;
        private const float Dt = 0.05f;
        private const float TweenSlack = 0.25f;

        private GameLoop _loop;
        private GameBootstrap _bootstrap;
        private SummonController _summon;
        private HudContext _context;
        private HudWave _wave;
        private HudRemaining _remaining;
        private HudHatch _hatch;
        private HudCoin _coin;
        private HudCountdown _countdown;
        private HudSummonButton _button;
        private int _events;

        private GameFlow Flow => _loop.Flow;

        private GuidePrefsGuard _guidePrefs;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _guidePrefs = GuidePrefsGuard.Save(); // F03 — QL.GuideDone 원복
            GameEvents.ClearAll();
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.LoadScene(SceneName, LoadSceneMode.Single);

            GameLoop found = null;
            StageBuilder stage = null;
            for (int i = 0; i < MaxWaitFrames; i++)
            {
                yield return null;
                found = Object.FindAnyObjectByType<GameLoop>();
                stage = Object.FindAnyObjectByType<StageBuilder>();
                _summon = Object.FindAnyObjectByType<SummonController>();
                if (found != null && found.IsReady && stage != null && stage.IsBuilt && _summon != null && _summon.IsReady) break;
            }
            SceneManager.sceneLoaded -= OnSceneLoaded;

            Assert.IsNotNull(found, "씬에 GameLoop 가 없다");
            Assert.IsTrue(found.IsReady, "GameLoop 가 준비되지 않았다");
            Assert.IsTrue(_summon != null && _summon.IsReady, "SummonController 가 준비되지 않았다");
            _loop = found;
            _bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            _context = Object.FindAnyObjectByType<HudContext>();
            _wave = Object.FindAnyObjectByType<HudWave>();
            _remaining = Object.FindAnyObjectByType<HudRemaining>();
            _hatch = Object.FindAnyObjectByType<HudHatch>();
            _coin = Object.FindAnyObjectByType<HudCoin>();
            _countdown = Object.FindAnyObjectByType<HudCountdown>();
            _button = Object.FindAnyObjectByType<HudSummonButton>();
            Assert.IsNotNull(_context, "UIRoot 에 HudContext 가 없다");
            Assert.IsNotNull(_wave);
            Assert.IsNotNull(_remaining);
            Assert.IsNotNull(_hatch);
            Assert.IsNotNull(_coin);
            Assert.IsNotNull(_countdown);
            Assert.IsNotNull(_button);

            Object.FindAnyObjectByType<DragController>().enabled = false; // 실제 마우스가 끼어들지 않게
            Time.timeScale = 0f; // 시간은 테스트가 Step 으로
            _events = 0;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _guidePrefs.Restore();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Time.timeScale = 1f;
            GameEvents.ClearAll();
            yield return null;
        }

        // ── 도우미 ───────────────────────────────────────────

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
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

        private void StepUntil(Func<bool> done, string what)
        {
            for (int i = 0; i < MaxSteps && !done(); i++) _loop.Step(Dt);
            Assert.IsTrue(done(), what + " 에 이르지 못했다");
        }

        private void StepToWave() => StepUntil(() => Flow.State == GameState.Wave, "웨이브");

        /// <summary>게임 시간으로 도는 HUD 트윈을 seconds 만큼 실제로 돌린다 (그동안 GameLoop 도 실제 시간으로 흐른다).</summary>
        private static IEnumerator RunTweens(float seconds)
        {
            Time.timeScale = 1f;
            yield return new WaitForSecondsRealtime(seconds);
            Time.timeScale = 0f;
        }

        private void CountAllEvents()
        {
            GameEvents.CoinChanged += (a, b) => _events++;
            GameEvents.SummonCostChanged += a => _events++;
            GameEvents.SummonAvailabilityChanged += a => _events++;
            GameEvents.RemainingSlimesChanged += a => _events++;
            GameEvents.HatchHpChanged += (a, b) => _events++;
            GameEvents.PrepTick += a => _events++;
            GameEvents.WaveStarted += (a, b, c) => _events++;
            GameEvents.StateChanged += a => _events++;
        }

        private string Snapshot()
        {
            return string.Join("|",
                _wave.WaveText.text, _wave.BannerText.text, _remaining.RemainingText.text, _remaining.FillRatio.ToString("R"),
                _hatch.HpText.text, _hatch.LitCount.ToString(), _coin.CoinText.text, _countdown.Label.text,
                _countdown.IsVisible.ToString(), _countdown.Scale.ToString("R"), _button.CostText.text,
                _button.Button.interactable.ToString());
        }

        // ── 테스트 ────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Start_ShowsInitialValues()
        {
            Assert.AreEqual(GameState.Prep, Flow.State);
            Assert.AreEqual(3, Flow.WaveCount, "웨이브 3개 (기획)");

            Assert.AreEqual(string.Format(HudWave.WaveFormat, 1, 3), _wave.WaveText.text, "웨이브 1 / 3");
            Assert.AreEqual(HudRemaining.NoWaveText, _remaining.RemainingText.text, "웨이브 전 남은 점액 —");
            Assert.AreEqual(0f, _remaining.FillRatio);

            int max = _bootstrap.Data.Config.HatchMaxHp;
            Assert.AreEqual(20, max, "GameConfig.hatchMaxHp");
            Assert.AreEqual(string.Format(HudHatch.HpFormat, max, max), _hatch.HpText.text, "해치 20 / 20");
            Assert.AreEqual(max, _hatch.SegmentCount, "칸 수 = hatchMaxHp");
            Assert.AreEqual(max, _hatch.LitCount, "칸 전부 켜짐");

            Assert.AreEqual(string.Format(HudCoin.CoinFormat, _bootstrap.Wallet.Coins), _coin.CoinText.text, "코인 = Wallet");
            Assert.AreEqual(string.Format(HudSummonButton.CostFormat, _summon.CurrentCost), _button.CostText.text, "비용 = CurrentCost");
            Assert.AreEqual(_summon.CurrentBlock == SummonBlock.None, _button.Button.interactable);
            yield return null;
        }

        [UnityTest]
        public IEnumerator WaveStarted_UpdatesWaveAndBanner()
        {
            string banner = null;
            GameEvents.WaveStarted += (i, t, b) => banner = b;
            StepToWave();
            Assert.IsNotNull(banner, "WaveStarted 가 왔다");
            Assert.AreEqual(banner, _wave.BannerText.text, "배너 문구");
            Assert.AreEqual(string.Format(HudWave.WaveFormat, 1, Flow.WaveCount), _wave.WaveText.text);

            Raise("RaiseWaveStarted", 1, Flow.WaveCount, "TEST-2");
            Assert.AreEqual(2, _wave.Number);
            Assert.AreEqual(string.Format(HudWave.WaveFormat, 2, Flow.WaveCount), _wave.WaveText.text, "웨이브 2 / 3");
            Assert.AreEqual("TEST-2", _wave.BannerText.text);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RemainingChanged_TextAndBarRatio()
        {
            StepToWave();
            int total = Flow.RemainingInWave;
            Assert.Greater(total, 3, "첫 웨이브 점액 4 이상");
            Assert.AreEqual(string.Format(HudRemaining.RemainingFormat, total, total), _remaining.RemainingText.text);
            Assert.AreEqual(1f, _remaining.FillRatio, 0.0001f, "웨이브 시작 막대 가득");

            Raise("RaiseRemainingSlimesChanged", total - 3);
            Assert.AreEqual(string.Format(HudRemaining.RemainingFormat, total - 3, total), _remaining.RemainingText.text);
            Assert.AreEqual((float)(total - 3) / total, _remaining.FillRatio, 0.0001f, "막대 = 남은 / 총");
            yield return null;
        }

        [UnityTest]
        public IEnumerator HatchDamaged_SegmentsOff_TextUpdated()
        {
            int max = Flow.Hatch.MaxHp;
            Raise("RaiseHatchHpChanged", max - 3, max);
            Assert.AreEqual(string.Format(HudHatch.HpFormat, max - 3, max), _hatch.HpText.text);
            Assert.AreEqual(max - 3, _hatch.LitCount, "피해 3 → 칸 17개 켜짐");
            Assert.IsTrue(_hatch.IsLit(max - 4), "왼쪽 칸은 켜짐");
            Assert.IsFalse(_hatch.IsLit(max - 3), "오른쪽 끝 3칸 꺼짐");

            float lit = _hatch.GetSegment(0).color.a;
            yield return RunTweens(_hatch.FadeDuration + TweenSlack);
            Assert.AreEqual(_hatch.OffAlpha, _hatch.GetSegment(max - 1).color.a, 0.01f, "꺼진 칸 Fade 끝");
            Assert.AreEqual(lit, _hatch.GetSegment(0).color.a, 0.001f, "켜진 칸 그대로");
        }

        /// <summary>U05 부터 코인 획득(delta &gt; 0)은 코인이 도착할 때 굴린다 (CombatFxTests). 여기선 바로 굴리는 지출(delta &lt; 0).</summary>
        [UnityTest]
        public IEnumerator CoinSpent_RollsToFinalValue()
        {
            int before = _bootstrap.Wallet.Coins;
            Assert.IsTrue(_bootstrap.Wallet.TrySpend(50), "시작 코인으로 50 지출");
            int now = _bootstrap.Wallet.Coins;
            Raise("RaiseCoinChanged", now, -50);

            Assert.AreEqual(now, _coin.Target);
            Assert.AreEqual(before, _coin.Shown, "굴러가기 전 = 이전 값");
            Assert.IsTrue(_coin.IsRolling, "굴러가는 중");

            yield return RunTweens(_coin.RollDuration + TweenSlack);
            Assert.IsFalse(_coin.IsRolling);
            Assert.AreEqual(now, _coin.Shown);
            Assert.AreEqual(string.Format(HudCoin.CoinFormat, now), _coin.CoinText.text, "굴러간 뒤 글자 = now");
        }

        /// <summary>U15: 첫 준비의 작업 지시서가 떠 있는 동안은 카운트다운이 숨는다 (R1 결정) — 지시서가 끝난 뒤 본다.</summary>
        [UnityTest]
        public IEnumerator PrepTick_BelowBigFrom_ScalesUp()
        {
            int bigFrom = _countdown.BigFrom;
            WaveOrderCard card = Object.FindAnyObjectByType<WaveOrderCard>();
            Assert.IsNotNull(card);
            Raise("RaisePrepTick", bigFrom + 2);
            Assert.AreEqual(card.IsShowing, _countdown.IsSuppressed, "지시서 중이면 숨김");
            if (card.IsShowing) Assert.IsFalse(_countdown.IsVisible, "지시서 중 숨김");
            _loop.enabled = false; // 지시서가 끝날 때까지 준비 시간이 흐르지 않게
            Time.timeScale = 1f;
            float end = Time.realtimeSinceStartup + card.Duration + 2f; // U21 — 지시서 길이 (FxConfig orderAbsorbAt + orderAbsorbDuration) 기준
            while (card.IsShowing && Time.realtimeSinceStartup < end) yield return null;
            Time.timeScale = 0f;
            _loop.enabled = true;
            Assert.IsFalse(card.IsShowing);

            Raise("RaisePrepTick", bigFrom + 2);
            Assert.IsTrue(_countdown.IsVisible);
            Assert.AreEqual(string.Format(HudCountdown.CountdownFormat, bigFrom + 2), _countdown.Label.text);
            float normal = _countdown.Scale;

            Raise("RaisePrepTick", bigFrom);
            Assert.AreEqual(string.Format(HudCountdown.CountdownFormat, bigFrom), _countdown.Label.text);
            Assert.Greater(_countdown.Scale, normal, "3초 이하에서 크게");
            Assert.AreEqual(_countdown.BigScale, _countdown.Scale, 0.0001f);

            StepToWave();
            Assert.IsFalse(_countdown.IsVisible, "웨이브 중 숨김");
            yield return null;
        }

        [UnityTest]
        public IEnumerator SummonAvailability_NoCellOnly_ButtonNotInteractable()
        {
            Raise("RaiseSummonAvailabilityChanged", SummonBlock.NoCoin);
            Assert.IsTrue(_button.Button.interactable, "NoCoin → 눌림 가능 (U03 — 누르면 흔들림)");
            Raise("RaiseSummonAvailabilityChanged", SummonBlock.NoCell);
            Assert.IsFalse(_button.Button.interactable, "NoCell → 못 누름");
            Raise("RaiseSummonAvailabilityChanged", SummonBlock.None);
            Assert.IsTrue(_button.Button.interactable, "None → 누름");
            yield return null;
        }

        [UnityTest]
        public IEnumerator SummonButtonClick_CallsRequestSummon_CoinsDecrease()
        {
            int before = _bootstrap.Wallet.Coins;
            int cost = _summon.CurrentCost;
            Assert.GreaterOrEqual(before, cost, "시작 코인으로 한 번은 소환");
            int drones = 0;
            GameEvents.DroneSpawned += (c, t, g) => drones++;

            _button.Button.onClick.Invoke();

            Assert.AreEqual(1, drones, "드론 소환");
            Assert.AreEqual(before - cost, _bootstrap.Wallet.Coins, "코인 감소");
            Assert.AreEqual(_bootstrap.Wallet.Coins, _coin.Target, "HUD 코인 목표 = Wallet");
            Assert.AreEqual(string.Format(HudSummonButton.CostFormat, _summon.CurrentCost), _button.CostText.text, "다음 비용");
            yield return null;
        }

        [UnityTest]
        public IEnumerator NoEvent_NoTextChange()
        {
            // 준비 초 경계 바로 앞이면 먼저 넘겨 둔다 (PrepTick 이 오지 않는 구간에서만 본다)
            float frac = Flow.PrepRemaining - Mathf.Floor(Flow.PrepRemaining);
            if (frac < 0.05f) _loop.Step(frac + 0.02f);
            frac = Flow.PrepRemaining - Mathf.Floor(Flow.PrepRemaining);
            float dt = Mathf.Min(0.002f, frac / 20f);

            CountAllEvents();
            string before = Snapshot();
            for (int i = 0; i < 10; i++)
            {
                _loop.Step(dt);
                yield return null;
            }

            Assert.AreEqual(GameState.Prep, Flow.State);
            Assert.AreEqual(0, _events, "이 구간에 이벤트가 없어야 한다");
            Assert.AreEqual(before, Snapshot(), "이벤트 없이 Step → HUD 글자 · 게이지 그대로");
        }

        // ── F03 바로 시작 버튼 (G11) ─────────────────────────

        [UnityTest]
        public IEnumerator F03_StartButton_PrepOnly_ClickStartsWave_HiddenOnPauseWaveResult()
        {
            var start = Object.FindAnyObjectByType<HudStartButton>();
            Assert.IsNotNull(start, "UIRoot Hud/StartWave 에 HudStartButton 이 없다");
            var card = Object.FindAnyObjectByType<WaveOrderCard>();
            var receipt = Object.FindAnyObjectByType<PayslipReceipt>();

            // 시작 지시서(U15)가 떠 있는 동안은 숨김 — 흡수가 끝나면 보임 (지시서 연출은 게임 시간: 잠깐 1배속, 준비 시간은 멈춘 루프로)
            _loop.enabled = false;
            yield return null;
            Assert.IsTrue(card.IsShowing || card.IsWaiting, "시작 지시서");
            Assert.IsFalse(start.IsShown, "지시서와 겹치지 않게 숨김");
            card.SkipToAbsorb();
            Time.timeScale = 1f;
            for (int i = 0; i < 3000 && (card.IsShowing || card.IsWaiting); i++) yield return null;
            Time.timeScale = 0f;
            _loop.enabled = true;
            yield return null;
            ResumeIfFocusPaused();
            yield return null;
            Assert.AreEqual(GameState.Prep, Flow.State);
            Assert.IsTrue(start.IsShown, "준비 시간 — 보임");
            Assert.IsTrue(start.Button.gameObject.activeInHierarchy);
            Assert.IsFalse(receipt.IsShowing);

            // 글자 24px 이상 · Bagel Fat One (버튼 글자 — U22 규칙에서도 Bagel) · 투입 버튼 · 메신저와 겹침 없음
            var texts = start.GetComponentsInChildren<TMPro.TMP_Text>(true);
            Assert.Greater(texts.Length, 0);
            for (int i = 0; i < texts.Length; i++)
            {
                Assert.GreaterOrEqual(texts[i].fontSize, 24f, texts[i].name);
                Assert.AreEqual("QL_BagelFatOne SDF", texts[i].font.name, texts[i].name);
            }
            Canvas.ForceUpdateCanvases();
            Rect b = ScreenRectOf((RectTransform)start.Button.transform);
            Assert.IsFalse(b.Overlaps(ScreenRectOf((RectTransform)_button.transform)), "드론 투입 버튼과 겹침");
            Assert.IsFalse(b.Overlaps(ScreenRectOf((RectTransform)Object.FindAnyObjectByType<OpsMessenger>().transform)), "메신저와 겹침");
            Assert.IsFalse(b.Overlaps(ScreenRectOf((RectTransform)_countdown.transform)), "카운트다운과 겹침");

            // 일시정지 — 숨김, 재개 — 다시 보임
            _loop.HandleEscape();
            Assert.AreEqual(GameState.Paused, Flow.State);
            yield return null;
            Assert.IsFalse(start.IsShown, "일시정지 중 숨김");
            _loop.HandleEscape();
            Time.timeScale = 0f;
            yield return null;
            Assert.IsTrue(start.IsShown, "재개 — 다시 보임");

            ResumeIfFocusPaused();
            // 클릭 → GameLoop.RequestStartWave → 다음 Step 에 웨이브
            int waveStarted = 0;
            Action<int, int, string> onWave = (i, t, n) => waveStarted++;
            GameEvents.WaveStarted += onWave;
            start.Button.onClick.Invoke();
            Assert.AreEqual(1, start.StartCount, "바로 시작 요청 받아들여짐");
            _loop.Step(Dt);
            GameEvents.WaveStarted -= onWave;
            Assert.AreEqual(GameState.Wave, Flow.State, "클릭 → 웨이브 시작");
            Assert.AreEqual(1, waveStarted);
            yield return null;
            Assert.IsFalse(start.IsShown, "웨이브 중 숨김");

            // 결과 — 숨김 (드론 없이 패배까지)
            StepUntil(() => { SupportTestUtil.PickIfOffered(_loop); return Flow.State == GameState.Result; }, "결과"); // G13 — 카드 선택에서 멈추지 않게
            yield return null;
            Assert.IsFalse(start.IsShown, "결과 중 숨김");
        }

        /// <summary>
        /// 에디터가 포커스를 잃으면 GameLoop.OnApplicationFocus 가 휴게(Paused)로 보낸다 (U12 Alt+Tab) — 테스트 흐름과 무관한 일시정지라 되돌린다.
        /// (보류함 34 — 포커스 없이도 통과)
        /// </summary>
        private void ResumeIfFocusPaused()
        {
            if (Flow.State != GameState.Paused) return;
            _loop.HandleEscape();
            Time.timeScale = 0f;
        }

        private static Rect ScreenRectOf(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }
    }
}
