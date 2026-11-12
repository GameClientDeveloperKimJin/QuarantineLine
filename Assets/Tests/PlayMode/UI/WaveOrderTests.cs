using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using QL.Core;
using QL.Core.Combat;
using QL.Core.Flow;
using QL.Core.Grid;
using QL.Core.Waves;
using QL.FX;
using QL.FX.Alert;
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
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace QL.Tests.PlayMode.UI
{
    /// <summary>
    /// U15 작업 지시서 — Game.unity 를 열면 GameLoop.Start 의 첫 WavePrepStarted 로 웨이브 1 지시서가 바로 돈다.
    /// 셋업은 GameLoop 를 멈추고(enabled = false — 준비 시간이 흐르지 않게) 그 첫 지시서가 끝날 때까지 기다린다. 그 뒤는 GameEvents 리플렉션 발행
    /// (WaveCleared → Wallet.Add → CoinChanged(수당) → WavePrepStarted — GameLoop 와 같은 순서) · 또는 GameLoop.Step 으로 한 판.
    /// 지시서 · 영수증은 게임 시간이라 timeScale 1 로 실제로 돌린다.
    /// </summary>
    public sealed class WaveOrderTests
    {
        private const string SceneName = "Game";
        private const int MaxWaitFrames = 300;
        private const float Slack = 0.3f;
        /// <summary>드론 위끝 높이 (칸 ×) — 호버 0.35 + 몸체 · 2등급 링 (G09 p2 실측 근사).</summary>
        private const float DroneTopHeight = 0.75f;

        private GameLoop _loop;
        private GameBootstrap _bootstrap;
        private WaveOrderCard _card;
        private PayslipReceipt _payslip;
        private HudCountdown _countdown;
        private HudWave _wave;
        private CameraShakeFx _shake;
        private FxConfig _fx;
        private int _started;
        private int _finished;
        private int _firstPlays;
        private int _firstIndex = -1;
        private WaveForecast _firstForecast;

        private GameFlow Flow => _loop.Flow;

        private GuidePrefsGuard _guidePrefs;

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
                if (found != null && found.IsReady && stage != null && stage.IsBuilt && summon != null && summon.IsReady) break;
            }
            SceneManager.sceneLoaded -= DisableSeeder;
            Assert.IsTrue(found != null && found.IsReady, "GameLoop 가 준비되지 않았다");

            _loop = found;
            _loop.enabled = false; // 준비 시간이 흘러 웨이브가 시작되지 않게 (Flow 읽기는 그대로)
            _bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            _card = Object.FindAnyObjectByType<WaveOrderCard>();
            _payslip = Object.FindAnyObjectByType<PayslipReceipt>();
            _countdown = Object.FindAnyObjectByType<HudCountdown>();
            _wave = Object.FindAnyObjectByType<HudWave>();
            _shake = Object.FindAnyObjectByType<CameraShakeFx>();
            Assert.IsNotNull(_card, "UIRoot InGameScreen/Hud 에 WaveOrderCard 가 없다");
            Assert.IsNotNull(_payslip);
            Assert.IsNotNull(_countdown);
            Assert.IsNotNull(_wave);
            Assert.IsNotNull(_shake, "[FX] 아래 CameraShakeFx");
            _fx = Object.FindAnyObjectByType<HudContext>().Fx;
            Object.FindAnyObjectByType<DragController>().enabled = false; // 실제 마우스가 끼어들지 않게 (흐름 메서드는 그대로)

            // 첫 준비 (GameLoop.Start) 지시서 — 기록해 두고 끝날 때까지
            _firstPlays = _card.PlayCount;
            _firstIndex = _card.ShownIndex;
            _firstForecast = _card.ShownForecast;
            Time.timeScale = 1f;
            yield return WaitCardDone();
            _started = 0;
            _finished = 0;
            _card.Started += OnStarted;
            _card.Finished += OnFinished;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _guidePrefs.Restore();
            SceneManager.sceneLoaded -= DisableSeeder;
            if (_card != null)
            {
                _card.Started -= OnStarted;
                _card.Finished -= OnFinished;
            }
            Time.timeScale = 1f;
            GameEvents.ClearAll();
            yield return null;
        }

        // ── 도우미 ───────────────────────────────────────────

        private void OnStarted(int index) => _started++;
        private void OnFinished(int index) => _finished++;

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

        private void Prep(int index, WaveForecast forecast) => Raise("RaiseWavePrepStarted", index, forecast);

        /// <summary>GameLoop.Publish 와 같은 순서 — WaveCleared → Wallet.Add → CoinChanged(수당) → (StateChanged) → WavePrepStarted.</summary>
        private void ClearThenPrep(int cleared, WaveForecast next)
        {
            var pay = new PayBreakdown(cleared, 50, 0, _bootstrap.Config.CleanReward);
            Raise("RaiseWaveCleared", cleared, pay);
            _bootstrap.Wallet.Add(pay.Total);
            Raise("RaiseCoinChanged", _bootstrap.Wallet.Coins, pay.Total);
            Prep(cleared + 1, next);
        }

        private static IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }

        private IEnumerator WaitCardDone()
        {
            float end = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < end && (_card.IsShowing || _card.IsWaiting || _payslip.IsPlaying)) yield return null;
            Assert.IsFalse(_card.IsShowing, "지시서가 끝나지 않았다");
        }

        private string Expected(string format, int a) => string.Format(format, a);

        private void AssertValues(int index, WaveForecast f, string what)
        {
            Assert.AreEqual(index, _card.ShownIndex, what + " 웨이브");
            Assert.AreEqual(string.Format(_card.Text.NumberFormat, index + 1), _card.NumberText.text, what + " 번호");
            string banner = _bootstrap.Data.Waves.Waves[index].BannerText;
            string site = banner.Substring(0, banner.IndexOf(','));
            Assert.AreEqual(site, _card.RowValue(WaveOrderCard.RowSite).text, what + " 현장 = 그 웨이브 bannerText 쉼표 앞");
            Assert.AreEqual("C-" + (index + 1) + " 화물칸", site, what + " (Waves.asset 실측 — C-1 · C-2 · C-3 화물칸)");
            Assert.AreEqual(string.Format(_card.Text.LoadFormat, f.SlimeCount, f.MutantCount), _card.RowValue(WaveOrderCard.RowLoad).text, what + " 예상 오염량");
            string caution = f.CautionLane >= 0 ? Expected(_card.Text.CautionFormat, f.CautionLane + 1) : _card.Text.CautionNone;
            Assert.AreEqual(caution, _card.RowValue(WaveOrderCard.RowCaution).text, what + " 주의 레인 (화면 1부터 · −1 = 없음)");
        }

        // ── 트리거 · 값 ──────────────────────────────────────

        [UnityTest]
        public IEnumerator FirstPrep_PlaysImmediately_Wave1NoMutant_ShowsNone()
        {
            Assert.AreEqual(1, _firstPlays, "GameLoop.Start 의 WavePrepStarted → 웨이브 1 지시서 바로 1번");
            Assert.AreEqual(0, _firstIndex);
            Assert.AreEqual(Flow.Forecast.SlimeCount, _firstForecast.SlimeCount, "값 = Flow.Forecast");
            Assert.AreEqual(Flow.Forecast.MutantCount, _firstForecast.MutantCount);
            Assert.AreEqual(WaveForecast.NoCautionLane, _firstForecast.CautionLane, "웨이브 1 은 변이 없음");
            AssertValues(0, _firstForecast, "웨이브 1");
            Assert.AreEqual(_card.Text.CautionNone, _card.RowValue(WaveOrderCard.RowCaution).text, "「없음」");
            Assert.AreEqual(_card.Text.Job, _card.RowValue(WaveOrderCard.RowJob).text);
            Assert.AreEqual(_card.Text.Owner, _card.RowValue(WaveOrderCard.RowOwner).text);
            Assert.AreEqual(_card.Text.Title, _card.TitleText.text);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CautionLane_ShownFromOne()
        {
            var f = new WaveForecast(1, 28, 3, 2);
            Prep(1, f);
            Assert.IsTrue(_card.IsShowing, "영수증이 없으면 바로");
            Assert.AreEqual(1, _started);
            AssertValues(1, f, "웨이브 2");
            Assert.AreEqual("3레인 (변이 반응)", _card.RowValue(WaveOrderCard.RowCaution).text, "CautionLane 2 → 화면 3레인");
            Assert.AreEqual("점액 28 · 변이 3", _card.RowValue(WaveOrderCard.RowLoad).text);
            Assert.AreEqual("No. C3-W02", _card.NumberText.text);
            float angle = _card.StampAngle;
            Assert.GreaterOrEqual(angle, _fx.orderStampAngleMin - 0.01f, "도장 각도 −18 ~ −10");
            Assert.LessOrEqual(angle, _fx.orderStampAngleMax + 0.01f);
            yield return WaitCardDone();
            Assert.AreEqual(1, _finished);
        }

        /// <summary>한 판 끝까지 — GameLoop.Step 으로 웨이브를 막고(점액은 바로 처치), 준비마다 지시서 1번 · 값 = 그 예보 = 실제 스폰.</summary>
        [UnityTest]
        public IEnumerator FullGame_EachPrepPlaysOnce_ValuesEqualForecastEqualActualSpawns()
        {
            SlimeRegistry registry = Object.FindAnyObjectByType<SlimeRegistry>();
            int waveCount = Flow.WaveCount;
            int lanes = _bootstrap.Grid.LaneCount;
            var spawned = new int[waveCount];
            var mutants = new int[waveCount];
            var perLane = new int[waveCount, lanes];
            var shown = new List<WaveForecast> { _firstForecast };
            var shownIndex = new List<int> { _firstIndex };
            var loads = new List<string> { _card.RowValue(WaveOrderCard.RowLoad).text };
            var cautions = new List<string> { _card.RowValue(WaveOrderCard.RowCaution).text };
            var sites = new List<string> { _card.RowValue(WaveOrderCard.RowSite).text };
            GameEvents.SlimeSpawned += s =>
            {
                int w = Flow.WaveIndex;
                spawned[w]++;
                if (s.Type != SlimeType.Mutant) return;
                mutants[w]++;
                perLane[w, s.Lane]++;
            };
            _card.Started += i =>
            {
                shownIndex.Add(i);
                shown.Add(_card.ShownForecast);
                loads.Add(_card.RowValue(WaveOrderCard.RowLoad).text);
                cautions.Add(_card.RowValue(WaveOrderCard.RowCaution).text);
                sites.Add(_card.RowValue(WaveOrderCard.RowSite).text);
            };
            _loop.enabled = true; // 처치 알림 구독 (OnDisable 이 끊는다) — 시간은 아래에서 timeScale 로

            for (int frame = 0; frame < 20000 && Flow.State != GameState.Result; frame++)
            {
                // 연출이 도는 동안은 실제 시간으로 (준비 시간도 같이 흐른다), 그 밖에는 timeScale 0 에서 한 프레임에 여러 Step
                if (_card.IsShowing || _card.IsWaiting || _payslip.IsPlaying)
                {
                    Assert.AreEqual(GameState.Prep, Flow.State, "연출은 준비 시간 안에서");
                    Assert.IsTrue(_countdown.IsSuppressed, "지시서 · 기다림 중 카운트다운 숨김");
                    Assert.IsFalse(_countdown.IsVisible);
                    Time.timeScale = 1f;
                    yield return null;
                    continue;
                }
                Time.timeScale = 0f;
                for (int k = 0; k < 20 && Flow.State != GameState.Result; k++)
                {
                    _loop.Step(0.05f);
                    SlimeTarget[] t = registry.Targets;
                    for (int j = registry.TargetCount - 1; j >= 0; j--)
                        if (registry.TryGet(t[j].Id, out SlimeView s)) registry.Despawn(s, DespawnReason.Killed);
                    SupportTestUtil.PickIfOffered(_loop); // G13 — 카드 선택에서 멈추지 않게
                    if (_card.IsShowing || _payslip.IsPlaying) break;
                }
                yield return null;
            }

            Assert.AreEqual(GameOutcome.Victory, Flow.Outcome, "한 판 끝 (승리)");
            Assert.AreEqual(waveCount, shown.Count, "웨이브 1 · 2 · 3 준비마다 1번");
            Assert.AreEqual(waveCount - 1, _started, "셋업 뒤 (웨이브 2 · 3) 2번");
            for (int w = 0; w < waveCount; w++)
            {
                WaveForecast f = shown[w];
                Assert.AreEqual(w, shownIndex[w]);
                Assert.AreEqual(spawned[w], f.SlimeCount, "웨이브 " + (w + 1) + " 점액 수 = 실제 스폰");
                Assert.AreEqual(mutants[w], f.MutantCount, "웨이브 " + (w + 1) + " 변이 수 = 실제");
                int caution = WaveForecast.NoCautionLane, best = 0;
                for (int l = 0; l < lanes; l++)
                    if (perLane[w, l] > best)
                    {
                        best = perLane[w, l];
                        caution = l;
                    }
                Assert.AreEqual(caution, f.CautionLane, "웨이브 " + (w + 1) + " 주의 레인 = 실제 변이 최다 레인");
                Assert.AreEqual(string.Format(_card.Text.LoadFormat, spawned[w], mutants[w]), loads[w]);
                string banner = _bootstrap.Data.Waves.Waves[w].BannerText;
                Assert.AreEqual(banner.Substring(0, banner.IndexOf(',')), sites[w], "웨이브 " + (w + 1) + " 현장 = bannerText 쉼표 앞");
                Assert.AreEqual(caution >= 0 ? string.Format(_card.Text.CautionFormat, caution + 1) : _card.Text.CautionNone, cautions[w]);
            }
            Assert.Greater(mutants[waveCount - 1], 0, "마지막 웨이브엔 변이가 있다 (주의 레인 숫자 확인)");
            // 판 전체 TotalActiveTweens 는 U04 SlimeWobbleFx (새 점액 칸 첫 등장 +2) · 결과 화면 (+1 Sequence) 이 늘린다 — 지시서 몫만 본다
            Assert.AreEqual(1, _card.SequenceBuilds, "3웨이브 완주 — 지시서 Sequence 1개 재사용");
        }

        // ── 영수증 뒤 · 카운트다운 ────────────────────────────

        [UnityTest]
        public IEnumerator PayslipPlaying_WaitsForFinished_ThenStarts_CountdownHiddenUntilEnd()
        {
            var f = new WaveForecast(1, 20, 2, 4);
            ClearThenPrep(0, f); // 같은 프레임 — 영수증이 먼저 돈다
            Assert.IsTrue(_payslip.IsPlaying, "영수증");
            Assert.IsTrue(_card.IsWaiting, "지시서는 영수증 끝을 기다림");
            Assert.IsFalse(_card.IsShowing);
            Assert.AreEqual(0, _started);
            Assert.IsTrue(_countdown.IsSuppressed, "기다리는 동안 카운트다운 숨김");

            Raise("RaisePrepTick", 4);
            Assert.IsFalse(_countdown.IsVisible, "PrepTick 이 와도 숨김");
            Assert.AreEqual("4", _countdown.Label.text, "숫자는 그대로 갱신");

            yield return Wait(_fx.payslipEndAt - 0.2f);
            Assert.AreEqual(0, _started, "영수증 끝(2.0초) 전에는 시작 안 함");
            float end = Time.realtimeSinceStartup + 3f;
            while (_payslip.IsPlaying && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsFalse(_payslip.IsPlaying);
            Assert.AreEqual(1, _started, "Finished 뒤 시작");
            Assert.IsTrue(_card.IsShowing);
            Assert.IsFalse(_card.IsWaiting);
            AssertValues(1, f, "웨이브 2");
            Assert.AreEqual("5레인 (변이 반응)", _card.RowValue(WaveOrderCard.RowCaution).text);
            Assert.IsFalse(_countdown.IsVisible, "지시서 중 숨김");

            Raise("RaisePrepTick", 3);
            yield return Wait(_card.Duration - 0.3f);
            Assert.IsTrue(_card.IsShowing);
            Assert.IsFalse(_countdown.IsVisible);
            yield return WaitCardDone();
            Assert.AreEqual(1, _finished);
            Assert.IsFalse(_countdown.IsSuppressed, "지시서 끝 → 숨김 풀림");
            Assert.IsTrue(_countdown.IsVisible, "남은 초부터 다시 보임");
            Assert.AreEqual("3", _countdown.Label.text);
        }

        /// <summary>앞 지시서가 아직 떠 있는데 영수증 + 다음 준비가 오면 — 앞 지시서가 끝나도 카운트다운은 다음 지시서 끝까지 숨김.</summary>
        [UnityTest]
        public IEnumerator PreviousCardEnds_WhileWaitingForPayslip_CountdownStaysHidden()
        {
            Prep(0, new WaveForecast(0, 15, 0, -1));
            yield return Wait(0.2f);
            ClearThenPrep(0, new WaveForecast(1, 28, 3, 2));
            Assert.IsTrue(_card.IsWaiting);
            Raise("RaisePrepTick", 4);
            float end = Time.realtimeSinceStartup + 3f;
            while (_card.IsShowing && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(_payslip.IsPlaying, "앞 지시서(1.6초)가 영수증(2.0초)보다 먼저 끝남");
            Assert.IsTrue(_countdown.IsSuppressed, "기다리는 중 — 그대로 숨김");
            Assert.IsFalse(_countdown.IsVisible);
            yield return WaitCardDone();
            Assert.AreEqual(2, _started);
            Assert.AreEqual(1, _card.ShownIndex);
            Assert.IsTrue(_countdown.IsVisible, "다음 지시서 끝 → 다시 보임");
        }

        [UnityTest]
        public IEnumerator End_HidesAndPunchesWavePanel_CameraKickSmallerThanHit()
        {
            int punches = _wave.AbsorbPunchCount;
            int kicks = _shake.KickCount;
            int hits = _shake.HitCount;
            Prep(1, new WaveForecast(1, 20, 0, -1));
            Assert.AreEqual(_card.Text.CautionNone, _card.RowValue(WaveOrderCard.RowCaution).text);
            yield return Wait(_fx.orderPunchAt + 0.05f);
            Assert.AreEqual(kicks + 1, _shake.KickCount, "도장 충격 — 작은 카메라 흔들림");
            Assert.Less(_fx.orderCameraKickStrength, _fx.shakeStrength, "해치 피격보다 약하게");
            Assert.AreEqual(hits, _shake.HitCount, "피격 흔들림 횟수와 섞이지 않음");
            Assert.IsFalse(_shake.IsShaking);
            Assert.AreEqual(1f, _card.Stamp.localScale.x, 0.001f, "도장 다 찍힘");
            yield return WaitCardDone();
            Assert.IsFalse(_card.Panel.gameObject.activeSelf, "끝 → 숨김");
            Assert.AreEqual(punches + 1, _wave.AbsorbPunchCount, "웨이브 패널 Punch");
            Assert.AreEqual(1, _finished);
            yield return Wait(_fx.orderCameraKickDuration + 0.1f);
            Assert.IsFalse(_shake.IsKicking);
            Assert.AreEqual(Vector3.zero, _shake.Offset, "흔들림 뒤 카메라 원위치");
        }

        // ── 클릭 · 입력 ──────────────────────────────────────

        [UnityTest]
        public IEnumerator PaperClick_SkipsToAbsorb_OnlyPaperTakesRaycast()
        {
            Prep(1, new WaveForecast(1, 20, 2, 0));
            yield return Wait(0.3f);
            Assert.Less(_card.Elapsed, _fx.orderAbsorbAt);

            Graphic[] gs = _card.Panel.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < gs.Length; i++)
                Assert.AreEqual(gs[i] == _card.Paper, gs[i].raycastTarget, gs[i].name + " — 종이만 raycastTarget");

            // 종이 가운데를 EventSystem 처럼 — 맨 위 그림 = 종이, 클릭은 부모로 올라가 WaveOrderCard 가 받는다
            Canvas.ForceUpdateCanvases();
            Vector2 at = RectTransformUtility.WorldToScreenPoint(null, _card.Paper.rectTransform.TransformPoint(_card.Paper.rectTransform.rect.center));
            EventSystem es = EventSystem.current;
            var data = new PointerEventData(es) { position = at, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            es.RaycastAll(data, hits);
            Assert.Greater(hits.Count, 0);
            Assert.AreEqual(_card.Paper.gameObject, hits[0].gameObject, "종이 위 클릭 = 종이");
            data.pointerCurrentRaycast = hits[0];
            GameObject handler = ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerClickHandler);
            Assert.AreEqual(_card.gameObject, handler, "WaveOrderCard 가 클릭을 받는다");

            Assert.AreEqual(1, _card.SkipCount);
            Assert.AreEqual(_fx.orderAbsorbAt, _card.Elapsed, 0.02f, "바로 1.20 흡수로");
            Assert.AreEqual(1f, _card.Stamp.localScale.x, 0.001f, "도장은 찍힌 모양");
            Assert.AreEqual(_card.SheetHeight, _card.Clip.rect.height, 0.5f, "종이는 다 나온 모양");
            yield return Wait(_fx.orderAbsorbDuration + Slack);
            Assert.IsFalse(_card.IsShowing, "흡수 끝 → 숨김");
            Assert.AreEqual(1, _finished);
        }

        [UnityTest]
        public IEnumerator DuringCard_SummonButtonAndDragMerge_Work()
        {
            Prep(1, new WaveForecast(1, 20, 2, 0));
            yield return Wait(0.5f);
            Assert.IsTrue(_card.IsShowing);

            // 소환 버튼 위 클릭이 지시서에 막히지 않는다
            HudSummonButton button = Object.FindAnyObjectByType<HudSummonButton>();
            Canvas.ForceUpdateCanvases();
            Vector2 at = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
            EventSystem es = EventSystem.current;
            var hits = new List<RaycastResult>();
            es.RaycastAll(new PointerEventData(es) { position = at }, hits);
            Assert.Greater(hits.Count, 0);
            Assert.IsTrue(hits[0].gameObject.transform.IsChildOf(button.transform), "소환 버튼 위 맨 위 그림 = 버튼");

            int drones = 0;
            GameEvents.DroneSpawned += (c, t, g) => drones++;
            _bootstrap.Wallet.Add(500);
            button.Button.onClick.Invoke();
            Assert.AreEqual(1, drones, "연출 중 소환");

            // 같은 드론 둘 → 드래그 머지
            var grid = _bootstrap.Grid;
            DroneBoard board = Object.FindAnyObjectByType<DroneBoard>();
            DragController drag = Object.FindAnyObjectByType<DragController>();
            int a = -1, b = -1;
            for (int i = 0; i < grid.CellCount && b < 0; i++)
            {
                if (grid.Get(i).HasDrone) continue;
                if (a < 0) a = i;
                else b = i;
            }
            grid.Place(a, DroneType.Suction, 1);
            grid.Place(b, DroneType.Suction, 1);
            board.SyncCell(a);
            board.SyncCell(b);
            int merged = 0;
            GameEvents.DroneMerged += (f, t, ty, g) => merged++;
            Assert.IsTrue(drag.BeginAt(a), "연출 중 집기");
            Assert.AreEqual(DropOutcome.Merge, drag.ReleaseAt(b).Outcome, "연출 중 머지");
            Assert.AreEqual(1, merged);
            Assert.IsTrue(_card.IsShowing, "지시서는 그대로 돈다");
            yield return WaitCardDone();
        }

        // ── 시간 · 판 끝 ─────────────────────────────────────

        [UnityTest]
        public IEnumerator Paused_Freezes_ResumeContinues()
        {
            Prep(1, new WaveForecast(1, 20, 2, 0));
            yield return Wait(0.4f);
            Time.timeScale = 0f;
            float at = _card.Elapsed;
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(at, _card.Elapsed, 1e-4f, "일시정지 중 멈춤 (게임 시간)");
            Assert.IsTrue(_card.IsShowing);
            Time.timeScale = 1f;
            yield return WaitCardDone();
            Assert.AreEqual(1, _finished);
        }

        [UnityTest]
        public IEnumerator GameEnded_HidesCancelsWaiting_NoFinished()
        {
            Prep(1, new WaveForecast(1, 20, 2, 0));
            yield return Wait(0.5f);
            Raise("RaiseGameEnded", default(ResultSummary));
            Assert.IsFalse(_card.IsShowing, "판 끝 → 즉시 숨김");
            Assert.IsFalse(_card.Panel.gameObject.activeSelf);
            Assert.IsFalse(_countdown.IsSuppressed);

            int started = _started;
            ClearThenPrep(0, new WaveForecast(1, 20, 2, 0));
            Assert.IsTrue(_card.IsWaiting);
            Raise("RaiseGameEnded", default(ResultSummary));
            Assert.IsFalse(_card.IsWaiting, "기다림 취소");
            yield return Wait(_fx.payslipEndAt + _card.Duration + 0.2f);
            Assert.AreEqual(0, _finished, "판 끝으로 멈추면 끝남 알림 없음");
            Assert.AreEqual(started, _started, "기다리던 지시서는 시작 안 함");
        }

        // ── U17 보류함 32 — 흡수 뒤 종이 잔상 ─────────────────

        [UnityTest]
        public IEnumerator AbsorbEnd_PanelAndSlotInactive_FinishedOnce()
        {
            Prep(1, new WaveForecast(1, 20, 2, 0));
            yield return Wait(_card.Duration + Slack);
            Assert.IsFalse(_card.IsShowing, "흡수 끝 → 숨김");
            Assert.IsFalse(_card.Panel.gameObject.activeSelf, "작은 종이(Scale 0.1)가 웨이브 패널 위에 남지 않는다");
            Assert.IsFalse(_card.Slot.gameObject.activeSelf, "슬롯도 꺼짐");
            Assert.AreEqual(1, _finished, "끝남 알림 1번 (흡수 1 도달 + 끝 콜백이 겹쳐도)");
            yield return Wait(0.3f);
            Assert.AreEqual(1, _finished);
        }

        [UnityTest]
        public IEnumerator SeekToDuration_SkipsEndCallback_StillHidden_FinishedOnce()
        {
            Prep(1, new WaveForecast(1, 20, 2, 0));
            yield return Wait(0.2f);
            _card.Seek(_card.Duration); // Goto — 끝 콜백을 건너뛰는 길 (G09 캡처 방식)
            Assert.IsFalse(_card.IsShowing, "Seek 로 끝까지 가도 숨김");
            Assert.IsFalse(_card.Panel.gameObject.activeSelf);
            Assert.AreEqual(1, _finished, "끝남 알림 1번");
            _card.Seek(_card.Duration);
            _card.Resume();
            yield return Wait(0.3f);
            Assert.IsFalse(_card.Panel.gameObject.activeSelf);
            Assert.AreEqual(1, _finished, "다시 Seek · Resume 해도 늘지 않음");

            // 다음 재생은 정상 (가드가 다음 판에 새지 않음)
            Prep(2, new WaveForecast(2, 30, 6, 3));
            Assert.IsTrue(_card.Panel.gameObject.activeSelf, "다음 지시서는 보인다");
            yield return WaitCardDone();
            Assert.AreEqual(2, _finished);
        }

        [UnityTest]
        public IEnumerator WaveStarted_ForceHides_CancelsWaiting_FinishedOnce()
        {
            Prep(1, new WaveForecast(1, 20, 2, 0));
            yield return Wait(0.5f);
            Assert.IsTrue(_card.IsShowing);
            Raise("RaiseWaveStarted", 1, 3, "C-2");
            Assert.IsFalse(_card.IsShowing, "웨이브 시작 → 즉시 숨김");
            Assert.IsFalse(_card.Panel.gameObject.activeSelf);
            Assert.IsFalse(_card.Slot.gameObject.activeSelf);
            Assert.IsFalse(_countdown.IsSuppressed, "카운트다운 숨김 풀림");
            Assert.AreEqual(1, _finished, "흡수 끝으로 마무리 — 알림 1번");
            yield return Wait(_card.Duration + Slack);
            Assert.AreEqual(1, _finished, "남은 시퀀스가 끝 콜백을 또 부르지 않음");

            // 영수증 끝을 기다리던 지시서는 취소
            int started = _started;
            ClearThenPrep(1, new WaveForecast(2, 30, 6, 3));
            Assert.IsTrue(_card.IsWaiting);
            Raise("RaiseWaveStarted", 2, 3, "C-3");
            Assert.IsFalse(_card.IsWaiting, "기다림 취소");
            Assert.IsFalse(_countdown.IsSuppressed);
            float end = Time.realtimeSinceStartup + 8f;
            while (_payslip.IsPlaying && Time.realtimeSinceStartup < end) yield return null;
            yield return null;
            Assert.AreEqual(started, _started, "기다리던 지시서는 시작 안 함");
            Assert.IsFalse(_card.Panel.gameObject.activeSelf);
        }

        // ── 재사용 · 할당 ────────────────────────────────────

        [UnityTest]
        public IEnumerator SecondPlay_ReusesSequence_ZeroAllocFrames()
        {
            Prep(1, new WaveForecast(1, 20, 2, 0));
            int tweens = ActiveTweens();
            yield return WaitCardDone();
            Prep(2, new WaveForecast(2, 30, 6, 3));
            Assert.AreEqual(1, _card.SequenceBuilds, "Sequence 재사용 (Restart)");
            Assert.AreEqual(tweens, ActiveTweens(), "트윈 수 늘지 않음");

            // 연출 프레임 할당 0 — 시퀀스를 시점마다 옮겨 (트윈이 매 프레임 부르는 같은 setter 경로) 이 스레드 할당 바이트를 잰다
            // (끝 콜백 1.60 은 건너뛰도록 1.58 까지만 — 끝은 아래 실제 재생에서)
            for (int i = 0; i <= 79; i++) _card.Seek(i * 0.02f);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i <= 79; i++) _card.Seek(i * 0.02f);
            _card.Seek(0.1f);
            _card.SkipToAbsorb();
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, bytes, "연출 프레임 · 건너뛰기 할당 0");
            _card.Resume();
            yield return WaitCardDone();
        }

        // ── 배치 ─────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Layout_BelowHatchPanel_WithinItsWidth_TextAtLeast24()
        {
            Prep(1, new WaveForecast(1, 28, 3, 2));
            _card.Seek(_fx.orderPunchAt);
            Canvas.ForceUpdateCanvases();

            Rect sheet = ScreenRect(_card.Sheet);
            Rect slot = ScreenRect((RectTransform)_card.Slot.transform);
            Rect hatch = ScreenRect((RectTransform)GameObject.Find("HatchPanel").transform);
            Rect wave = ScreenRect((RectTransform)_wave.transform);
            Assert.LessOrEqual(slot.yMax, hatch.yMin + 0.5f, "슬롯은 해치 패널 바로 아래 (겹침 없음)");
            Assert.LessOrEqual(sheet.yMax, hatch.yMin + 0.5f, "종이도 해치 패널 아래");
            Assert.GreaterOrEqual(sheet.xMin, hatch.xMin - 0.5f, "종이 폭은 해치 패널 폭 안");
            Assert.LessOrEqual(sheet.xMax, hatch.xMax + 0.5f);
            Assert.IsFalse(sheet.Overlaps(wave), "웨이브 패널과 겹치지 않음");
            Assert.GreaterOrEqual(sheet.yMin, 0f, "화면 안");
            // U17 — 종이 아래끝(찢긴 끝)이 그리드 첫 줄(격리선 쪽) 드론 윗부분에 닿지 않는다 (U10 21:9 p3b 에서 닿음 → 종이 452 → 432)
            Camera cam = Camera.main;
            var space = _bootstrap.Space;
            float droneTop = 0f;
            for (int lane = 0; lane < _bootstrap.Grid.LaneCount; lane++)
            {
                Vector3 top = space.CellToWorld(_bootstrap.Grid.IndexOf(lane, 0)) + Vector3.up * (DroneTopHeight * space.CellSize);
                droneTop = Mathf.Max(droneTop, cam.WorldToScreenPoint(top).y);
            }
            Assert.GreaterOrEqual(sheet.yMin, droneTop + 8f, "종이 아래끝 y " + sheet.yMin.ToString("0") + " ≥ 첫 줄 드론 위끝 y " + droneTop.ToString("0") + " + 8px");

            var texts = _card.Panel.GetComponentsInChildren<TMPro.TMP_Text>(true);
            Assert.AreEqual(3 + 2 * WaveOrderCard.RowCount, texts.Length, "머리 3 · 줄마다 2");
            for (int i = 0; i < texts.Length; i++)
            {
                Assert.GreaterOrEqual(texts[i].fontSize, 24f, texts[i].name + " 24px 이상");
                Assert.AreEqual("QL_BagelFatOne SDF", texts[i].font.name, "글꼴 하나");
                Assert.LessOrEqual(texts[i].preferredWidth, texts[i].rectTransform.rect.width + 0.5f, texts[i].name + " 칸 안에 들어감");
            }
            _card.Resume();
            yield return WaitCardDone();
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
