using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using QL.Core.Flow;
using QL.FX;
using QL.FX.Combat;
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
    /// U14 수당 영수증 — Game.unity 를 열고 GameLoop 를 멈춘 채(enabled = false — 준비 시간이 흐르지 않게) WaveCleared → Wallet.Add →
    /// CoinChanged(수당) 를 GameLoop 와 같은 순서로 리플렉션 발행한다. 영수증 · 배너 · HUD 코인 굴림은 게임 시간이라 timeScale 1 로 실제로 돌린다.
    /// </summary>
    public sealed class PayslipTests
    {
        private const string SceneName = "Game";
        private const int MaxWaitFrames = 300;
        private const float Slack = 0.3f;

        private GameLoop _loop;
        private GameBootstrap _bootstrap;
        private PayslipReceipt _receipt;
        private WaveClearBanner _banner;
        private HudCoin _coin;
        private CoinFlyFx _flyer;
        private FxConfig _fx;
        private int _finished;

        private int Wallet => _bootstrap.Wallet.Coins;
        private int WaveCount => _loop.Flow.WaveCount;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
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
                if (found != null && found.IsReady && stage != null && stage.IsBuilt && summon != null && summon.IsReady) break;
            }
            SceneManager.sceneLoaded -= DisableSeeder;
            Assert.IsTrue(found != null && found.IsReady, "GameLoop 가 준비되지 않았다");
            yield return null; // HudCoin.Start

            _loop = found;
            _loop.enabled = false; // 준비 시간이 흘러 웨이브가 시작되지 않게 (Flow 읽기는 그대로)
            _bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            _receipt = Object.FindAnyObjectByType<PayslipReceipt>();
            _banner = Object.FindAnyObjectByType<WaveClearBanner>();
            _coin = Object.FindAnyObjectByType<HudCoin>();
            _flyer = Object.FindAnyObjectByType<CoinFlyFx>();
            Assert.IsNotNull(_receipt, "UIRoot InGameScreen/Hud 에 PayslipReceipt 가 없다");
            Assert.IsNotNull(_banner, "UIRoot InGameScreen 에 WaveClearBanner 가 없다");
            Assert.IsNotNull(_coin);
            _fx = Object.FindAnyObjectByType<HudContext>().Fx;
            Object.FindAnyObjectByType<DragController>().enabled = false;
            _finished = 0;
            _receipt.Finished += OnFinished;
            Time.timeScale = 1f;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SceneManager.sceneLoaded -= DisableSeeder;
            if (_receipt != null) _receipt.Finished -= OnFinished;
            Time.timeScale = 1f;
            GameEvents.ClearAll();
            yield return null;
        }

        // ── 도우미 ───────────────────────────────────────────

        private void OnFinished() => _finished++;

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

        /// <summary>GameLoop.Publish 와 같은 순서 — WaveCleared → Wallet.Add → CoinChanged(수당).</summary>
        private PayBreakdown Clear(int index, int clearBonus, int cleanCount)
        {
            var pay = new PayBreakdown(index, clearBonus, cleanCount, _bootstrap.Config.CleanReward);
            Raise("RaiseWaveCleared", index, pay);
            if (pay.Total > 0)
            {
                _bootstrap.Wallet.Add(pay.Total);
                Raise("RaiseCoinChanged", Wallet, pay.Total);
            }
            return pay;
        }

        private static IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }

        private IEnumerator WaitSettled()
        {
            float end = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < end && (_receipt.IsPlaying || _coin.IsRolling || (_flyer != null && _flyer.Pending > 0)))
                yield return null;
        }

        // ── 트리거 · 범위 ─────────────────────────────────────

        [UnityTest]
        public IEnumerator Waves0And1_ShowReceiptAndBanner_LastWaveDoesNot()
        {
            yield return WaitSettled();
            Assert.IsFalse(_receipt.IsShowing);
            Assert.IsFalse(_banner.IsShowing);
            Assert.GreaterOrEqual(WaveCount, 3, "웨이브 3개 기준");

            for (int w = 0; w < WaveCount - 1; w++)
            {
                Clear(w, 50, 0);
                Assert.IsTrue(_receipt.IsPlaying, "웨이브 " + w + " → 영수증");
                Assert.IsTrue(_receipt.IsShowing);
                Assert.IsTrue(_banner.IsShowing, "배너도");
                Assert.AreEqual(w + 1, _banner.ShownNumber);
                Assert.AreEqual(string.Format(_fx.clearBannerFormat, w + 1), _banner.Label.text);
                Assert.AreEqual(string.Format(_fx.payslipWaveFormat, w + 1), _receipt.WaveText.text);
                Assert.AreEqual(string.Format(_fx.payslipNextFormat, w + 2), _receipt.NextText.text);
                yield return WaitSettled();
                Assert.IsFalse(_receipt.IsShowing, "끝나면 숨김");
            }

            int plays = _receipt.PlayCount;
            int wallet = Wallet;
            Clear(WaveCount - 1, 50, 1);
            Assert.IsFalse(_receipt.IsPlaying, "마지막 웨이브 → 영수증 없음 (결과 화면이 보여 줌)");
            Assert.IsFalse(_receipt.IsShowing);
            Assert.IsFalse(_banner.IsShowing, "마지막 웨이브 → 클리어 배너 없음");
            Assert.AreEqual(plays, _receipt.PlayCount);
            Assert.AreEqual(0, _coin.Held, "붙잡지 않음");
            Assert.AreEqual(Wallet, _coin.Target, "마지막 웨이브 수당은 바로 굴림 (GameEnded 가 곧 맞춤)");
            Assert.Greater(Wallet, wallet);
        }

        // ── 항목 줄 · 합계 ────────────────────────────────────

        [UnityTest]
        public IEnumerator CleanZero_RowHidden_CleanTwo_RowShown_TotalEqualsPay()
        {
            yield return WaitSettled();
            PayBreakdown pay = Clear(0, 50, 0);
            Assert.AreEqual(1, _receipt.RowCount, "청소 0 회 → 청소 줄 없음");
            Assert.IsTrue(_receipt.RowActive(0));
            Assert.IsFalse(_receipt.RowActive(1), "값 0 줄은 꺼짐");
            Assert.AreEqual(_fx.payslipClearLabel, _receipt.RowLabel(0).text);
            Assert.AreEqual(string.Format(_fx.payslipValueFormat, 50), _receipt.RowValue(0).text);
            float shortSheet = _receipt.SheetHeight;
            yield return WaitSettled();
            Assert.AreEqual(string.Format(_fx.payslipValueFormat, pay.Total), _receipt.TotalValue.text, "합계 = Pay.Total");

            pay = Clear(1, 50, 2);
            Assert.AreEqual(2, _receipt.RowCount);
            Assert.IsTrue(_receipt.RowActive(1));
            Assert.IsFalse(_receipt.RowActive(2));
            Assert.IsFalse(_receipt.RowActive(3));
            Assert.AreEqual(string.Format(_fx.payslipCleanFormat, 2), _receipt.RowLabel(1).text);
            Assert.AreEqual(string.Format(_fx.payslipValueFormat, pay.CleanReward), _receipt.RowValue(1).text);
            Assert.AreEqual("+20", _receipt.RowValue(1).text, "×2 +20 (cleanReward 10)");
            Assert.Greater(_receipt.SheetHeight, shortSheet, "줄이 늘면 종이도 길어짐");
            Assert.AreEqual(string.Format(_fx.payslipValueFormat, 0), _receipt.TotalValue.text, "합계는 0 부터");
            yield return Wait(_fx.payslipTotalAt + _fx.payslipTotalDuration + 0.05f);
            Assert.AreEqual(string.Format(_fx.payslipValueFormat, pay.Total), _receipt.TotalValue.text, "합계 = Pay.Total (70)");
            Assert.AreEqual(70, pay.Total);
            yield return WaitSettled();
        }

        // ── HUD 코인 표시값 ──────────────────────────────────

        [UnityTest]
        public IEnumerator HudHeldUntilReceiptEnd_ThenEqualsWallet_FinishedOnce()
        {
            yield return WaitSettled();
            int shown = _coin.Shown;
            Assert.AreEqual(Wallet, shown);
            PayBreakdown pay = Clear(0, 50, 2);
            Assert.AreEqual(shown + pay.Total, Wallet, "지갑은 바로");
            Assert.AreEqual(pay.Total, _coin.Held);
            Assert.AreEqual(shown, _coin.Target, "표시 목표는 그대로 (영수증 합계만큼 붙잡음)");

            yield return Wait(_fx.payslipCoinAt - 0.1f);
            Assert.AreEqual(shown, _coin.Shown, "1.9초 전까지 표시 그대로");
            Assert.AreEqual(0, _finished);

            yield return Wait(_fx.payslipEndAt - _fx.payslipCoinAt + 0.15f);
            Assert.AreEqual(0, _coin.Held, "영수증 끝에 풀림");
            Assert.AreEqual(Wallet, _coin.Target, "끝 (2.0초) 에 표시 목표 = 지갑");
            Assert.IsFalse(_receipt.IsPlaying);
            Assert.AreEqual(1, _finished, "끝남 알림 1회");
            Assert.IsTrue(_coin.IsGainShowing, "「+합계」 표시");
            Assert.AreEqual(string.Format(_fx.coinGainFormat, pay.Total), _coin.GainText.text);

            yield return Wait(_fx.coinRollDuration + Slack);
            Assert.AreEqual(Wallet, _coin.Shown, "굴림 끝 — 표시 = 지갑");
            Assert.AreEqual(Wallet.ToString(), _coin.CoinText.text);
            yield return Wait(1.5f);
            Assert.AreEqual(1, _finished, "더 부르지 않음");
            Assert.IsFalse(_coin.IsGainShowing, "「+합계」 사라짐");
        }

        [UnityTest]
        public IEnumerator SpendDuringReceipt_RollsImmediately_EndEqualsWallet()
        {
            yield return WaitSettled();
            PayBreakdown pay = Clear(0, 50, 1);
            yield return Wait(0.5f);

            int cost = 10;
            Assert.IsTrue(_bootstrap.Wallet.TrySpend(cost));
            Raise("RaiseCoinChanged", Wallet, -cost);
            Assert.AreEqual(Wallet - pay.Total, _coin.Target, "지출은 바로 굴림 (영수증 몫은 빼고)");
            Assert.IsTrue(_coin.IsRolling);

            yield return WaitSettled();
            Assert.AreEqual(Wallet, _coin.Shown, "끝난 뒤 HUD = 지갑 (영수증 중 소환 포함)");
            Assert.AreEqual(Wallet.ToString(), _coin.CoinText.text);
        }

        [UnityTest]
        public IEnumerator Paused_ReceiptFreezes_ResumeContinues()
        {
            yield return WaitSettled();
            Clear(0, 50, 0);
            yield return Wait(0.5f);
            Time.timeScale = 0f;
            float at = _receipt.Elapsed;
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.AreEqual(at, _receipt.Elapsed, 1e-4f, "일시정지 중 멈춤 (게임 시간)");
            Assert.IsTrue(_receipt.IsPlaying, "멈춰도 돌고 있는 중");
            Assert.AreEqual(0, _finished);
            Assert.Greater(_coin.Held, 0, "표시는 아직 붙잡힘");

            Time.timeScale = 1f;
            yield return WaitSettled();
            Assert.AreEqual(1, _finished, "재개 뒤 이어서 끝남");
            Assert.AreEqual(Wallet, _coin.Shown);
        }

        [UnityTest]
        public IEnumerator GameEnded_DuringReceipt_HidesAndSnapsWallet()
        {
            yield return WaitSettled();
            Clear(0, 50, 2);
            yield return Wait(0.6f);
            Raise("RaiseGameEnded", default(ResultSummary));
            Assert.IsFalse(_receipt.IsShowing, "판 끝 → 즉시 숨김");
            Assert.IsFalse(_receipt.IsPlaying);
            Assert.IsFalse(_banner.IsShowing);
            Assert.AreEqual(0, _coin.Held);
            Assert.AreEqual(Wallet, _coin.Shown, "결과 화면 전에 표시 = 지갑");
            yield return Wait(_fx.payslipEndAt);
            Assert.AreEqual(0, _finished, "판 끝으로 멈추면 끝남 알림 없음");
        }

        // ── 재사용 · 할당 ────────────────────────────────────

        [UnityTest]
        public IEnumerator SecondReceipt_ReusesSequence_ZeroAllocFrames()
        {
            yield return WaitSettled();
            Clear(0, 50, 2);
            int tweens = ActiveTweens();
            yield return WaitSettled();
            Clear(1, 50, 1);
            Assert.AreEqual(1, _receipt.SequenceBuilds, "Sequence 재사용 (Restart)");
            Assert.AreEqual(tweens, ActiveTweens(), "트윈 수 늘지 않음");

            // 연출 프레임 할당 0 — 시퀀스를 시점마다 옮겨 (트윈이 매 프레임 부르는 같은 setter 경로) 이 스레드 할당 바이트를 잰다. 한 바퀴 데운 뒤
            // (1.9초 HUD 풀림 · 2.0초 끝 콜백은 건너뛰도록 1.88 까지만 — 그 둘은 아래 실제 재생에서)
            for (int i = 0; i <= 94; i++) _receipt.Seek(i * 0.02f);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i <= 94; i++) _receipt.Seek(i * 0.02f);
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, bytes, "연출 프레임 할당 0");
            _receipt.Resume();
            yield return WaitSettled();
            Assert.AreEqual(Wallet, _coin.Shown);
        }

        // ── 배치 ─────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Layout_BelowCoinPanel_NoOverlapWithPauseButton_TextAtLeast24()
        {
            yield return WaitSettled();
            Clear(0, 50, 2);
            yield return Wait(_fx.payslipStampAt + _fx.payslipStampDuration + 0.05f);
            _receipt.Seek(_fx.payslipStampAt + _fx.payslipStampDuration + 0.05f);
            Canvas.ForceUpdateCanvases();

            Rect paper = ScreenRect(_receipt.Sheet);
            Rect coin = ScreenRect((RectTransform)_coin.transform);
            Rect pause = ScreenRect((RectTransform)GameObject.Find("PauseButton").transform);
            Assert.LessOrEqual(paper.yMax, coin.yMin + 0.5f, "코인 패널 바로 아래 (겹침 없음)");
            Assert.IsFalse(paper.Overlaps(pause), "일시정지 버튼과 겹치지 않음");
            Assert.LessOrEqual(paper.xMax, Screen.width + 0.5f, "화면 안");
            Assert.GreaterOrEqual(paper.yMin, 0f, "화면 안");

            var texts = _receipt.Panel.GetComponentsInChildren<TMPro.TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++) Assert.GreaterOrEqual(texts[i].fontSize, 24f, texts[i].name + " 24px 이상");
            Assert.GreaterOrEqual(_banner.Label.fontSize, 24f);
            Assert.GreaterOrEqual(_coin.GainText.fontSize, 24f);
            _receipt.Resume();
            yield return WaitSettled();
        }

        // 테스트 asmdef 가 DOTween 을 참조하지 않아 리플렉션으로 센다 (ScreenFlowTests 와 같음)
        private static int ActiveTweens()
        {
            Type t = Type.GetType("DG.Tweening.DOTween, DOTween");
            Assert.IsNotNull(t, "DOTween 형식을 찾지 못했다");
            return (int)t.GetMethod("TotalActiveTweens", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
        }

        private static Rect ScreenRect(RectTransform r)
        {
            var c = new Vector3[4];
            r.GetWorldCorners(c); // 오버레이 캔버스 — 월드 = 화면 픽셀
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }
    }
}
