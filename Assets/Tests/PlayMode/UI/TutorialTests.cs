using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using QL.Core;
using QL.Core.Grid;
using QL.FX;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Drones;
using QL.Game.Flow;
using QL.Game.Input;
using QL.Game.Stage;
using QL.Game.Summon;
using QL.UI.Data;
using QL.UI.Hud;
using QL.UI.Tutorial;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using TPhase = QL.UI.Tutorial.TutorialDirector.Phase;

namespace QL.Tests.PlayMode.UI
{
    /// <summary>
    /// U21 첫 판 상세 튜토리얼. Game.unity 를 열고 GameLoop 를 멈춘 채 (Update 끔 — 준비 시간 · Enter · Esc 를 테스트가 쥔다) 단계를 넘긴다.
    /// 소환은 SummonController.RequestSummon, 머지는 DragController.BeginAt · ReleaseAt, 웨이브 시작은 RequestStartWave + Step.
    /// 드론 3종 · 오염은 G14 API 가 실제로 놓은 것을 읽는다. PlayerPrefs QL.GuideDone 은 GuidePrefsGuard 로 원복.
    /// 「다음」 은 TutorialDirector.Next() (= 버튼 · Enter 가 부르는 길). Enter 키 자체는 InputTestFixture 가 있으면 2단계에서 한 줄 추가.
    /// </summary>
    public sealed class TutorialTests
    {
        private const string SceneName = "Game";
        private const int MaxWaitFrames = 300;
        private const float PixelTolerance = 2f;

        private GuidePrefsGuard _guidePrefs;
        private GameLoop _loop;
        private GameBootstrap _bootstrap;
        private DragController _drag;
        private SummonController _summon;
        private WaveOrderCard _card;
        private TutorialDirector _tut;
        private CursorGuide _pointer;
        private TutorialTargets _targets;
        private HudStartButton _start;
        private FxConfig _fx;

        private GridModel Grid => _bootstrap.Grid;
        private TutorialSteps Steps => _tut.Steps;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _guidePrefs = GuidePrefsGuard.Save();
            PlayerPrefs.DeleteKey(CursorGuide.PrefsKey); // 첫 판
            yield return LoadGame();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SceneManager.sceneLoaded -= DisableSeeder;
            yield return SetResolution(1920, 1080);
            Time.timeScale = 1f;
            GameEvents.ClearAll();
            _guidePrefs.Restore();
        }

        // ── 도우미 ───────────────────────────────────────────

        private IEnumerator LoadGame()
        {
            SceneManager.sceneLoaded += DisableSeeder;
            GameEvents.ClearAll();
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
            _loop.enabled = false;
            _bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            _drag = Object.FindAnyObjectByType<DragController>();
            _drag.enabled = false;
            _summon = Object.FindAnyObjectByType<SummonController>();
            _card = Object.FindAnyObjectByType<WaveOrderCard>();
            _tut = Object.FindAnyObjectByType<TutorialDirector>();
            _pointer = Object.FindAnyObjectByType<CursorGuide>();
            _targets = Object.FindAnyObjectByType<TutorialTargets>();
            _start = Object.FindAnyObjectByType<HudStartButton>();
            Assert.IsNotNull(_tut, "UIRoot InGameScreen/Hud 에 TutorialDirector 가 없다");
            _fx = Object.FindAnyObjectByType<HudContext>().Fx;
            Time.timeScale = 1f;
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

        private IEnumerator SkipOrderCard()
        {
            _card.SkipToAbsorb();
            yield return WaitFor(() => !_card.IsShowing && !_card.IsWaiting);
            yield return null;
            yield return null;
        }

        private static IEnumerator WaitFor(Func<bool> done, int maxFrames = 3000)
        {
            float hardEnd = Time.realtimeSinceStartup + 60f;
            for (int i = 0; i < maxFrames && !done() && Time.realtimeSinceStartup < hardEnd; i++) yield return null;
        }

        private static IEnumerator SetResolution(int w, int h)
        {
#if UNITY_EDITOR
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution((uint)w, (uint)h, "U21 TutorialTests");
#endif
            for (int i = 0; i < MaxWaitFrames && (Screen.width != w || Screen.height != h); i++) yield return null;
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>단계 번호 (0부터) 가 될 때까지 Next 로 넘긴다 — 직접 단계를 만나면 멈춤 (Assert).</summary>
        private IEnumerator NextUntil(int index)
        {
            for (int guard = 0; guard < 32 && _tut.StepIndex < index; guard++)
            {
                Assert.IsTrue(_tut.Next(), "Next 단계여야 한다: " + _tut.StepIndex);
                yield return null;
            }
            Assert.AreEqual(index, _tut.StepIndex);
        }

        private int IndexOf(TutorialAdvance a)
        {
            for (int i = 0; i < Steps.StepCount; i++) if (Steps.Step(i).advance == a) return i;
            Assert.Fail("단계 없음: " + a);
            return -1;
        }

        private int IndexOf(TutorialSetup s)
        {
            for (int i = 0; i < Steps.StepCount; i++) if (Steps.Step(i).setup == s) return i;
            Assert.Fail("단계 없음: " + s);
            return -1;
        }

        /// <summary>6 투입 · 11 머지 직접 단계를 실제 행동으로 넘기며 index 까지.</summary>
        private IEnumerator PlayUntil(int index)
        {
            for (int guard = 0; guard < 32 && _tut.StepIndex < index; guard++)
            {
                TutorialAdvance a = Steps.Step(_tut.StepIndex).advance;
                if (a == TutorialAdvance.Summon) Assert.IsTrue(_summon.RequestSummon(), "투입");
                else if (a == TutorialAdvance.Merge)
                {
                    Assert.IsTrue(_pointer.HasPair, "머지 쌍");
                    Assert.IsTrue(_drag.BeginAt(_pointer.PairFrom));
                    Assert.AreEqual(DropOutcome.Merge, _drag.ReleaseAt(_pointer.PairTo).Outcome);
                }
                else if (a == TutorialAdvance.Move)
                {
                    Assert.GreaterOrEqual(_tut.MoveFrom, 0, "옮길 드론");
                    Assert.IsTrue(_drag.BeginAt(_tut.MoveFrom));
                    Assert.AreEqual(DropOutcome.Move, _drag.ReleaseAt(_tut.MoveTo).Outcome);
                }
                else Assert.IsTrue(_tut.Next());
                yield return null;
                yield return null;
            }
            Assert.AreEqual(index, _tut.StepIndex);
        }

        // ── 시작 · 넘김 ──────────────────────────────────────

        [UnityTest]
        public IEnumerator FirstRun_HoldsPrep_WaitsOrderCard_ThenStep1()
        {
            yield return null;
            Assert.AreEqual(TPhase.Waiting, _tut.State);
            Assert.IsTrue(_loop.IsPrepHeld, "처음부터 준비 시간 잡음");
            Assert.IsTrue(_tut.IsKeyLocked, "Enter · Esc 키 잠금");
            Assert.IsFalse(_tut.IsOverlayShown, "지시서가 도는 동안 안 뜸");

            yield return SkipOrderCard();
            Assert.AreEqual(TPhase.Running, _tut.State);
            Assert.AreEqual(0, _tut.StepIndex, "1단계부터");
            Assert.IsTrue(_tut.IsOverlayShown);
            Assert.AreEqual("1 / " + Steps.StepCount, _tut.Bubble.CounterText.text);
            Assert.AreEqual(Steps.Step(0).title, _tut.Bubble.TitleText.text);
            Assert.GreaterOrEqual(_tut.Bubble.BodyText.fontSize, 24f, "글자 24px 이상");
            Assert.AreEqual("QL_BagelFatOne SDF", _tut.Bubble.BodyText.font.name);
            Assert.IsFalse(_start.IsShown, "튜토리얼 중 「작업 시작」 숨김");
            Assert.AreEqual(15, Steps.StepCount, "R1 — 4+5 합쳐 14 + 배치 직접 1 = 15단계");

            // 어둠 = 3D 만 (UI 아래 캔버스) · 발판 15칸은 밝게
            Canvas dim = _tut.DimRoot.GetComponent<Canvas>();
            Canvas ui = _tut.GetComponentInParent<QL.UI.Screens.UIRoot>().GetComponent<Canvas>();
            Assert.IsTrue(dim.overrideSorting);
            Assert.Less(dim.sortingOrder, ui.sortingOrder, "어둠은 UI 캔버스 아래 (UI 전부 밝게)");
            Assert.IsTrue(_tut.DimRoot.activeInHierarchy);
            Assert.IsTrue(_tut.Spotlight.HasHoleAt(0), "발판 구멍");
            Rect grid = HoleScreenRect(0);
            for (int c = 0; c < Grid.CellCount; c++)
            {
                Vector3 s = Camera.main.WorldToScreenPoint(_bootstrap.Space.CellToWorld(c));
                Assert.IsTrue(grid.Contains(new Vector2(s.x, s.y)), "칸 " + c + " 이 밝은 발판 안");
            }

            HudCountdown countdown = Object.FindAnyObjectByType<HudCountdown>();
            Assert.IsTrue(countdown.IsHeldShown, "카운트다운 자리 = 교육 중");
            Assert.AreEqual(Steps.PrepHeld, countdown.Label.text, "문구 = TutorialSteps SO (「교육 중 · Enter 로 다음」)");
            Assert.GreaterOrEqual(countdown.Label.fontSize, 24f);
        }

        [UnityTest]
        public IEnumerator Body_RangeAndContamTokens_FilledFromData()
        {
            yield return SkipOrderCard();
            int place = IndexOf(TutorialSetup.PlaceThreeTypes);
            yield return PlayUntil(place);
            Assert.IsTrue(_bootstrap.Catalog.TryGet(DroneType.Suction, 1, out QL.Core.Stats.UnitStats st));
            StringAssert.Contains(Mathf.RoundToInt(st.Range).ToString(), _tut.Bubble.BodyText.text, "흡입 사거리 = UnitCatalog");
            StringAssert.DoesNotContain("{", _tut.Bubble.BodyText.text, "토큰이 남지 않음");

            yield return PlayUntil(IndexOf(TutorialSetup.ContaminateCell));
            StringAssert.Contains(Mathf.RoundToInt(_bootstrap.Config.ContaminateDuration).ToString(), _tut.Bubble.BodyText.text, "오염 시간 = GameConfig");
            StringAssert.DoesNotContain("{", _tut.Bubble.BodyText.text);
        }

        [UnityTest]
        public IEnumerator Contaminate_PartnerPlaced_MergeCleans_Advances()
        {
            Assert.IsTrue(Steps.PlaceCleanPartner, "R1 — 직접 청소 허용 (기본 켬)");
            yield return SkipOrderCard();
            int contam = IndexOf(TutorialSetup.ContaminateCell);
            yield return PlayUntil(contam);
            int cell = _tut.ContaminatedCell;
            Cell c = Grid.Get(cell);
            Assert.IsTrue(c.HasDrone && c.IsContaminated, "드론 있는 칸 오염");
            int partner = TutorialBoard.FindDrone(Grid, c.Type, c.Grade, cell);
            Assert.GreaterOrEqual(partner, 0, "같은 드론 1대 더 (청소 짝)");

            Assert.IsTrue(_drag.BeginAt(partner));
            Assert.AreEqual(DropOutcome.MergeClean, _drag.ReleaseAt(cell).Outcome);
            yield return null;
            Assert.IsFalse(Grid.Get(cell).IsContaminated, "씻김");
            Assert.AreEqual(contam + 1, _tut.StepIndex, "직접 청소 → 다음");
        }

        [UnityTest]
        public IEnumerator Next_Advances_ActionStepWaitsForAction()
        {
            yield return SkipOrderCard();
            int summonStep = IndexOf(TutorialAdvance.Summon);
            yield return NextUntil(summonStep);
            Assert.IsFalse(_tut.Next(), "직접 단계 — 다음으로 안 넘어감");
            Assert.IsFalse(_tut.Bubble.IsNextShown);
            Assert.AreEqual(CursorGuide.PointerMode.Tap, _pointer.Mode, "손가락 = 투입 버튼");
            for (int i = 0; i < 10; i++) yield return null;
            Assert.AreEqual(summonStep, _tut.StepIndex, "행동 전엔 그대로");

            int coins = _bootstrap.Wallet.Coins;
            Assert.IsTrue(_summon.RequestSummon());
            yield return null;
            Assert.AreEqual(summonStep + 1, _tut.StepIndex, "투입 1번 → 다음");
            Assert.Less(_bootstrap.Wallet.Coins, coins, "진짜 소환 (코인 씀)");

            // 배치 직접 단계 — 방금 투입한 드론 → 목표 칸 끌기 루프
            Assert.AreEqual(TutorialAdvance.Move, Steps.Step(_tut.StepIndex).advance);
            Assert.AreEqual(_tut.LastSummonCell, _tut.MoveFrom, "옮길 드론 = 방금 투입한 드론");
            Assert.IsTrue(TutorialBoard.IsEmpty(Grid, _tut.MoveTo), "목표 = 빈 칸");
            Grid.CoordOf(_tut.MoveFrom, out int fl, out int fr);
            Grid.CoordOf(_tut.MoveTo, out int tl, out int tr);
            Assert.GreaterOrEqual(Mathf.Abs(fl - tl) + Mathf.Abs(fr - tr), Steps.MoveMinDistance, "목표 칸은 드론에서 2칸 이상 (R1)");
            Assert.AreEqual(CursorGuide.PointerMode.Drag, _pointer.Mode, "손가락 = 끌기");
            Assert.AreEqual(_tut.MoveFrom, _pointer.PairFrom);
            Assert.AreEqual(_tut.MoveTo, _pointer.PairTo);
            Assert.IsFalse(_tut.Next(), "옮기기 전엔 안 넘어감");
            int moveStep = _tut.StepIndex;
            Assert.IsTrue(_drag.BeginAt(_tut.MoveFrom));
            Assert.AreEqual(DropOutcome.Move, _drag.ReleaseAt(_tut.MoveTo).Outcome);
            yield return null;
            Assert.AreEqual(moveStep + 1, _tut.StepIndex, "옮기면 다음 (DroneMoved)");
            Assert.AreEqual(CursorGuide.PointerMode.Hidden, _pointer.Mode);
        }

        [UnityTest]
        public IEnumerator Hold_PrepRemainingUnchanged_ThroughTutorial()
        {
            yield return SkipOrderCard();
            float r0 = _loop.PrepRemaining;
            Assert.Greater(r0, 1f);
            for (int i = 0; i < 100; i++) _loop.Step(0.1f);
            Assert.AreEqual(r0, _loop.PrepRemaining, 1e-4f, "튜토리얼 동안 PrepRemaining 그대로");
            Assert.AreEqual(GameState.Prep, _loop.Flow.State);
        }

        // ── G14 배치 · 오염 ──────────────────────────────────

        [UnityTest]
        public IEnumerator ThreeTypes_PlacedFree_SpotlightEachDrone()
        {
            yield return SkipOrderCard();
            int place = IndexOf(TutorialSetup.PlaceThreeTypes);
            yield return PlayUntil(place - 1);
            int coins = _bootstrap.Wallet.Coins;
            int cost = _summon.CurrentCost;
            Assert.IsTrue(_tut.Next());
            yield return null;
            for (int t = 0; t < 3; t++)
            {
                int cell = _tut.TypeCell((DroneType)t);
                Assert.GreaterOrEqual(cell, 0, (DroneType)t + " 칸");
                Cell c = Grid.Get(cell);
                Assert.IsTrue(c.HasDrone && c.Type == (DroneType)t && c.Grade == 1, (DroneType)t + " 1등급");
            }
            Assert.AreEqual(coins, _bootstrap.Wallet.Coins, "코인 그대로");
            Assert.AreEqual(cost, _summon.CurrentCost, "소환 비용 그대로");
            Assert.AreEqual(place, _tut.StepIndex, "배치는 「다음」 이 아님 (DroneSpawned 무시)");
            Assert.IsTrue(_tut.Spotlight.HasHole);
        }

        [UnityTest]
        public IEnumerator Contaminate_TimerFrozen_LastStep_CountdownStarts_StartButtonPunch()
        {
            yield return SkipOrderCard();
            int contam = IndexOf(TutorialSetup.ContaminateCell);
            yield return PlayUntil(contam);
            int cell = _tut.ContaminatedCell;
            Assert.GreaterOrEqual(cell, 0);
            Assert.IsTrue(Grid.Get(cell).IsContaminated);
            float left = Grid.ContaminationRemaining(cell);
            for (int i = 0; i < 60; i++) yield return null; // GameBootstrap.Update 의 Grid.Tick 도 돈다
            Assert.AreEqual(left, Grid.ContaminationRemaining(cell), 1e-4f, "hold 중 오염 시간 멈춤 (G14)");

            int last = Steps.StepCount - 1;
            yield return PlayUntil(last);
            Assert.IsTrue(_loop.IsPrepHeld, "마지막 단계까지 준비 시간 멈춤");
            Assert.IsTrue(_tut.IsKeyLocked, "마지막 단계까지 Enter 잠금");
            Assert.IsFalse(_start.IsShown, "마지막 단계까지 「작업 시작」 숨김");
            Assert.IsFalse(CursorGuide.IsDoneSaved);
            float r0 = _loop.PrepRemaining;
            yield return new WaitForSecondsRealtime(_fx.tutorialBubblePopDuration + 0.1f);
            Assert.GreaterOrEqual(ScreenRect(_tut.Bubble.Rect).yMin, HoleScreenRect(0).yMax - PixelTolerance, "15단계 말풍선은 발판 위 복도 (R1)");

            Assert.IsTrue(_tut.Next(), "마지막 단계 「다음」 = 튜토리얼 끝");
            Assert.AreEqual(TPhase.AwaitSupport, _tut.State, "끝 — 16 대기");
            Assert.IsTrue(CursorGuide.IsDoneSaved, "QL.GuideDone 1");
            Assert.IsFalse(_loop.IsPrepHeld, "hold 해제 — 카운트다운 시작");
            Assert.IsFalse(_tut.IsOverlayShown);
            Assert.IsFalse(_tut.DimRoot.activeSelf, "어둠도 닫힘");
            Assert.IsTrue(_tut.IsKeyLocked, "같은 프레임엔 아직 잠금 (같은 Enter 가 바로 시작으로 읽히지 않게)");
            yield return null; // LateUpdate
            Assert.IsFalse(_tut.IsKeyLocked, "프레임 끝 — Enter · Esc 돌려줌");
            yield return WaitFor(() => _start.IsShown, 10);
            Assert.IsTrue(_start.IsShown, "「작업 시작」 나타남");
            Assert.AreEqual(1, _start.RevealPunchCount, "나타날 때 펀치 1번");
            _loop.Step(0.5f);
            Assert.Less(_loop.PrepRemaining, r0, "준비 시간이 흐른다");
            Assert.AreEqual(GameState.Prep, _loop.Flow.State, "웨이브는 아직 — 버튼 · Enter · 시간 끝으로 평소대로");

            _start.Button.onClick.Invoke();
            _loop.Step(0.02f);
            Assert.AreEqual(GameState.Wave, _loop.Flow.State, "작업 시작 → 웨이브 1");
            Assert.GreaterOrEqual(TutorialBoard.FindType(Grid, DroneType.Cryo), 0, "놓은 냉각 드론은 남김");
        }

        // ── 건너뛰기 ─────────────────────────────────────────

        [UnityTest]
        public IEnumerator Skip_SavesDone_ReleasesHoldAndKeys()
        {
            yield return SkipOrderCard();
            yield return NextUntil(2);
            _tut.Bubble.SkipButton.onClick.Invoke();
            yield return null;
            Assert.IsTrue(CursorGuide.IsDoneSaved, "QL.GuideDone 1");
            Assert.IsFalse(_loop.IsPrepHeld, "hold 해제");
            Assert.IsFalse(_tut.IsKeyLocked);
            Assert.AreEqual(TPhase.Done, _tut.State);
            Assert.IsFalse(_tut.IsOverlayShown);
            float r0 = _loop.PrepRemaining;
            _loop.Step(0.5f);
            Assert.Less(_loop.PrepRemaining, r0, "준비 시간이 다시 흐른다");
        }

        [UnityTest]
        public IEnumerator AlreadyDone_NeverStarts_NoHold()
        {
            CursorGuide.SaveDone();
            yield return LoadGame();
            yield return SkipOrderCard();
            Assert.AreEqual(TPhase.Off, _tut.State);
            Assert.IsFalse(_loop.IsPrepHeld);
            Assert.IsFalse(_tut.IsOverlayShown);
        }

        // ── 스포트라이트 = 대상 화면 사각형 ──────────────────

        [UnityTest]
        public IEnumerator FrameRect_EqualsTargetRect_16x9_21x9()
        {
            int[,] res = { { 1920, 1080 }, { 2560, 1080 } };
            for (int k = 0; k < 2; k++)
            {
                yield return SetResolution(res[k, 0], res[k, 1]);
                if (_tut.State == TPhase.Waiting) yield return SkipOrderCard();
                if (_tut.StepIndex < IndexOf(TutorialAdvance.Summon)) yield return NextUntil(IndexOf(TutorialAdvance.Summon));
                yield return new WaitForSecondsRealtime(_fx.tutorialHoleMoveDuration + 0.3f); // 구멍 이동 끝 (실제 시간)

                Rect expected = ScreenRect(_targets.DeployButton);
                Rect hole = ScreenRect(_tut.Frame);
                float pad = (_fx.tutorialHolePadding + _fx.tutorialFramePadding) * _targets.Space.lossyScale.x;
                Assert.AreEqual(expected.xMin - pad, hole.xMin, PixelTolerance, res[k, 0] + " xMin");
                Assert.AreEqual(expected.yMin - pad, hole.yMin, PixelTolerance, res[k, 0] + " yMin");
                Assert.AreEqual(expected.xMax + pad, hole.xMax, PixelTolerance, res[k, 0] + " xMax");
                Assert.AreEqual(expected.yMax + pad, hole.yMax, PixelTolerance, res[k, 0] + " yMax");

                // 말풍선은 화면 안 · 구멍과 안 겹침
                Rect b = ScreenRect(_tut.Bubble.Rect);
                Assert.GreaterOrEqual(b.xMin, -0.5f);
                Assert.LessOrEqual(b.xMax, Screen.width + 0.5f);
                Assert.IsFalse(b.Overlaps(hole), res[k, 0] + " 말풍선이 대상을 가림");
            }
        }

        private static Rect ScreenRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        private Rect HoleScreenRect(int i)
        {
            Rect h = _tut.Spotlight.HoleAt(i);
            RectTransform s = (RectTransform)_tut.Spotlight.transform;
            Vector3 a = s.TransformPoint(new Vector3(h.xMin, h.yMin, 0f));
            Vector3 b = s.TransformPoint(new Vector3(h.xMax, h.yMax, 0f));
            return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        }

        // ── 할당 0 ───────────────────────────────────────────

        [UnityTest]
        public IEnumerator WaitingFrames_NoAlloc_NoRebuild()
        {
            yield return SkipOrderCard();
            yield return NextUntil(1); // 월드 대상 (게이트) — 매 프레임 투영
            yield return WaitFor(() => false, 60);
            const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;
            var update = (Action)typeof(TutorialDirector).GetMethod("Update", F).CreateDelegate(typeof(Action), _tut);
            var late = (Action)typeof(TutorialDirector).GetMethod("LateUpdate", F).CreateDelegate(typeof(Action), _tut);
            for (int i = 0; i < 10; i++) { update(); late(); }
            int rebuilds = _tut.Spotlight.RebuildCount;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 200; i++) { update(); late(); _loop.Step(0.02f); }
            Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before, "대기 프레임 할당 0");
            yield return null;
            Assert.AreEqual(rebuilds, _tut.Spotlight.RebuildCount, "구멍이 그대로면 메시 재빌드 없음");
        }
    }
}
