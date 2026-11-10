using System.Collections;
using NUnit.Framework;
using QL.Core;
using QL.Core.Economy;
using QL.Core.Grid;
using QL.Core.Stats;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Drones;
using QL.Game.Flow;
using QL.Game.Input;
using QL.Game.Stage;
using QL.Game.Summon;
using QL.UI.Feedback;
using QL.UI.Hud;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace QL.Tests.PlayMode.UI
{
    /// <summary>
    /// U03 입력 피드백 — Game.unity 를 열고 DroneSeeder 를 끈 빈 그리드에 직접 배치한 뒤 DragController 흐름 메서드
    /// (HoverAt · BeginAt) 와 SummonController.RequestSummon · 버튼 onClick 으로 이벤트 → 연출을 본다 (마우스 · 키보드 없음).
    /// DragController 는 끄고 부른다 (실제 마우스가 Update 로 끼어들지 않게). 시간은 멈춘다 (timeScale 0, 흐름은 Prep 그대로).
    /// </summary>
    public sealed class FeedbackTests
    {
        private const string SceneName = "Game";
        private const int MaxWaitFrames = 300;

        private GameBootstrap _bootstrap;
        private DroneBoard _board;
        private DragController _drag;
        private CellHighlighter _highlighter;
        private SummonController _summon;
        private HoverOutline _outline;
        private RangePreview _range;
        private HudSummonButton _button;
        private SummonButtonStates _states;

        private GridModel Grid => _bootstrap.Grid;
        private GridSpace Space => _bootstrap.Space;

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
                _summon = Object.FindAnyObjectByType<SummonController>();
                StageBuilder stage = Object.FindAnyObjectByType<StageBuilder>();
                if (loop != null && loop.IsReady && stage != null && stage.IsBuilt && _summon != null && _summon.IsReady) break;
            }
            SceneManager.sceneLoaded -= DisableSeeder;

            Assert.IsTrue(loop != null && loop.IsReady, "GameLoop 가 준비되지 않았다");
            _bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            _board = Object.FindAnyObjectByType<DroneBoard>();
            _drag = Object.FindAnyObjectByType<DragController>();
            _highlighter = Object.FindAnyObjectByType<CellHighlighter>();
            _outline = Object.FindAnyObjectByType<HoverOutline>();
            _range = Object.FindAnyObjectByType<RangePreview>();
            _button = Object.FindAnyObjectByType<HudSummonButton>();
            _states = Object.FindAnyObjectByType<SummonButtonStates>();
            Assert.IsNotNull(_drag);
            Assert.IsNotNull(_highlighter);
            Assert.IsNotNull(_outline, "[FX] 아래 HoverOutline 이 없다");
            Assert.IsNotNull(_range, "[FX] 아래 RangePreview 가 없다");
            Assert.IsNotNull(_button);
            Assert.IsNotNull(_states, "DEPLOY 버튼에 SummonButtonStates 가 없다");
            _drag.enabled = false;
            Time.timeScale = 0f;

            for (int i = 0; i < Grid.CellCount; i++)
                Assert.IsFalse(Grid.Get(i).HasDrone, "시드 배치가 꺼지지 않았다 (칸 " + i + ")");
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

        private int Place(int lane, int row, DroneType type, int grade)
        {
            int index = Grid.IndexOf(lane, row);
            Grid.Place(index, type, grade);
            _board.SyncCell(index);
            return index;
        }

        private void Hover(int cell) => _drag.HoverAt(cell, cell >= 0 ? Space.CellToWorld(cell) : Vector3.zero);

        private UnitStats Stats(DroneType type, int grade) => _bootstrap.Catalog.Get(type, grade);

        private void AssertRangeFor(int cell, DroneType type, int grade)
        {
            UnitStats s = Stats(type, grade);
            int lane = cell % Space.LaneCount;
            Assert.IsTrue(_range.IsShown, "사거리 미리보기가 보여야 한다");
            Assert.AreEqual(lane, _range.Lane, "미리보기 레인 = 칸 레인");
            Assert.AreEqual(type, _range.Type);
            Assert.AreEqual(grade, _range.Grade);
            Assert.AreEqual(s.Range, _range.RangeCells, 1e-4f, "미리보기 길이 = UnitStats.Range (칸)");
            Assert.AreEqual(s.Range * Space.CellSize, _range.WorldLength, 1e-4f, "월드 길이 = Range × cellSize");
            int lo = s.AttackKind == AttackKind.Cone ? Mathf.Max(0, lane - s.ConeLaneSpread) : lane;
            int hi = s.AttackKind == AttackKind.Cone ? Mathf.Min(Space.LaneCount - 1, lane + s.ConeLaneSpread) : lane;
            Assert.AreEqual(hi - lo + 1, _range.LaneSpan, "덮는 레인 수 = 판정(SelectCone) 폭");
            Vector3 c = (Space.PathToWorld(lo, 0f) + Space.PathToWorld(hi, 0f)) * 0.5f;
            Assert.AreEqual(c.x, _range.transform.GetChild(0).position.x, 1e-4f, "미리보기 가운데 x");
            Assert.AreEqual(s.Range * Space.CellSize * 0.5f, _range.transform.GetChild(0).position.z, 1e-4f, "격리선(z 0)부터 시작");
            Color tint = _board.TintOf(type, grade);
            Assert.AreEqual(tint.r, _range.Tint.r, 1e-4f, "색 = UnitData.tintColor");
            Assert.AreEqual(tint.g, _range.Tint.g, 1e-4f);
            Assert.AreEqual(tint.b, _range.Tint.b, 1e-4f);
        }

        private static float ColorDistance(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
        }

        private void DrainToNoCoin()
        {
            _bootstrap.Wallet.TrySpend(_bootstrap.Wallet.Coins - (_summon.CurrentCost - 1));
            GameEvents_RaiseCoinChanged(); // SummonController 가 다시 계산해 NoCoin 발행
            Assert.AreEqual(SummonBlock.NoCoin, _summon.CurrentBlock, "코인 < 비용");
        }

        private void GameEvents_RaiseCoinChanged()
        {
            System.Reflection.MethodInfo m = typeof(GameEvents).GetMethod("RaiseCoinChanged",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            m.Invoke(null, new object[] { _bootstrap.Wallet.Coins, 0 });
        }

        // ── 테스트 ────────────────────────────────────────────

        [UnityTest]
        public IEnumerator HoverDroneCell_OutlineAndRangeShown()
        {
            int cell = Place(1, 2, DroneType.Suction, 1);
            Hover(cell);

            Assert.IsTrue(_outline.IsShown, "외곽선 보임");
            Assert.AreEqual(cell, _outline.Cell);
            Vector3 center = Space.CellToWorld(cell);
            Assert.AreEqual(center.x, _outline.Position.x, 1e-4f, "외곽선 위치 = 그 칸");
            Assert.AreEqual(center.z, _outline.Position.z, 1e-4f, "외곽선 위치 = 그 칸");
            AssertRangeFor(cell, DroneType.Suction, 1);

            Hover(-1);
            Assert.IsFalse(_outline.IsShown, "-1 → 외곽선 숨김");
            Assert.IsFalse(_range.IsShown, "-1 → 미리보기 숨김");
            yield return null;
        }

        [UnityTest]
        public IEnumerator HoverPlasmaG2_RangeWidthIsCone()
        {
            int cell = Place(2, 0, DroneType.Plasma, 2);
            Hover(cell);
            AssertRangeFor(cell, DroneType.Plasma, 2);
            Assert.Greater(_range.LaneSpan, 1, "소각 2등급은 부채꼴 — 레인 여러 개");
            yield return null;
        }

        [UnityTest]
        public IEnumerator HoverEmptyCell_NothingShown()
        {
            int drone = Place(0, 0, DroneType.Cryo, 1);
            Hover(drone);
            Assert.IsTrue(_outline.IsShown);

            Hover(Grid.IndexOf(3, 1)); // 빈 칸
            Assert.IsFalse(_outline.IsShown, "빈 칸 → 외곽선 없음");
            Assert.IsFalse(_range.IsShown, "빈 칸 → 미리보기 없음");
            yield return null;
        }

        [UnityTest]
        public IEnumerator DragStart_HoverHidden_OriginTranslucent()
        {
            int from = Place(1, 1, DroneType.Suction, 1);
            Hover(from);
            Color normal = _highlighter.ColorAt(Grid.IndexOf(4, 2));
            Assert.IsTrue(_outline.IsShown);

            Assert.IsTrue(_drag.BeginAt(from));
            Assert.IsFalse(_outline.IsShown, "집으면 외곽선 숨김");
            Assert.IsFalse(_range.IsShown, "집으면 미리보기 숨김");
            Color origin = _highlighter.ColorAt(from);
            Assert.Less(origin.a, 1f, "집은 칸 반투명");
            Assert.Less(origin.a, normal.a, "집은 칸이 다른 칸보다 흐리다");
            _drag.Cancel();
            yield return null;
        }

        [UnityTest]
        public IEnumerator DragOverSameDrone_PreviewMergeColor_RangeOfNextGrade()
        {
            int from = Place(0, 1, DroneType.Plasma, 1);
            int to = Place(2, 1, DroneType.Plasma, 1);
            int previews = 0;
            DropOutcome last = DropOutcome.Return;
            GameEvents.DropPreviewChanged += (c, o, has) => { previews++; last = o; };

            Assert.IsTrue(_drag.BeginAt(from));
            Hover(to);

            Assert.AreEqual(1, previews);
            Assert.AreEqual(DropOutcome.Merge, last);
            Color merge = _highlighter.ColorAt(to);
            Assert.Greater(merge.g, merge.r, "머지 = 초록");
            Assert.Greater(merge.g, merge.b, "머지 = 초록");
            AssertRangeFor(to, DroneType.Plasma, 2); // 머지 뒤 등급 기준 (2등급 = 부채꼴 폭)

            Hover(-1);
            Assert.IsFalse(_range.IsShown, "칸 밖 → has=false → 숨김");
            _drag.Cancel();
            yield return null;
        }

        [UnityTest]
        public IEnumerator DropPreview_FourOutcomes_DistinctColors()
        {
            int from = Place(1, 1, DroneType.Suction, 1);
            int same = Place(2, 1, DroneType.Suction, 1);
            int other = Place(3, 1, DroneType.Cryo, 1);
            int dirty = Grid.IndexOf(0, 1);
            Grid.Contaminate(dirty); // 오염된 빈 칸 → Reject (MergeRule 5)
            int empty = Grid.IndexOf(4, 2);

            Assert.IsTrue(_drag.BeginAt(from));
            Hover(same);
            Color merge = _highlighter.ColorAt(same);
            Assert.IsTrue(_outline.IsShown && _outline.Cell == same, "대상 칸 외곽선");
            Assert.AreEqual(merge, _outline.Tint, "대상 칸 외곽선 색 = 칸 색");
            Hover(empty);
            Color move = _highlighter.ColorAt(empty);
            Assert.AreEqual(move, _outline.Tint);
            Hover(other);
            Color swap = _highlighter.ColorAt(other);
            Assert.AreEqual(swap, _outline.Tint);
            Hover(dirty);
            Color reject = _highlighter.ColorAt(dirty);
            Assert.AreEqual(reject, _outline.Tint);
            DropOutcome rejectOutcome = MergeRule.Resolve(Grid.Get(from), Grid.Get(dirty), from, dirty, _bootstrap.Catalog);
            bool rangeOnReject = _range.IsShown;
            _drag.Cancel();
            Assert.IsFalse(_outline.IsShown, "드래그 끝 → 대상 칸 외곽선 숨김");

            Color[] all = { merge, move, swap, reject };
            string[] names = { "머지", "이동", "교환", "거부" };
            for (int i = 0; i < all.Length; i++)
                for (int j = i + 1; j < all.Length; j++)
                    Assert.Greater(ColorDistance(all[i], all[j]), 0.3f, names[i] + " 와 " + names[j] + " 색이 비슷하다");
            Assert.AreEqual(DropOutcome.Reject, rejectOutcome, "오염된 빈 칸 = 거부");
            Assert.IsFalse(rangeOnReject, "거부 칸에는 사거리 미리보기 없음");
            yield return null;
        }

        [UnityTest]
        public IEnumerator SummonNoCoin_Click_ShakeStarted_NoSummon()
        {
            DrainToNoCoin();
            Assert.IsTrue(_button.Button.interactable, "NoCoin 도 눌림 가능");
            Color red = _states.CostColor;
            Assert.Greater(red.r, red.g, "비용 숫자 빨강");
            Assert.Greater(red.r, red.b, "비용 숫자 빨강");

            int spawned = 0;
            GameEvents.DroneSpawned += (c, t, g) => spawned++;
            int coins = _bootstrap.Wallet.Coins;
            _button.Button.onClick.Invoke();

            Assert.AreEqual(0, spawned, "소환 안 됨");
            Assert.AreEqual(coins, _bootstrap.Wallet.Coins, "코인 그대로");
            Assert.IsTrue(_states.IsShaking, "버튼 흔들림 시작");
            Assert.IsTrue(_states.IsCostPunching, "비용 숫자 Punch");
            yield return null;
        }

        [UnityTest]
        public IEnumerator SummonNoCell_ShowsFullMessage()
        {
            for (int i = 0; i < Grid.CellCount; i++)
            {
                Grid.Place(i, DroneType.Suction, 1);
                _board.SyncCell(i);
            }
            GameEvents_RaiseCoinChanged(); // SummonController 가 다시 계산 → NoCell
            Assert.AreEqual(SummonBlock.NoCell, _summon.CurrentBlock);

            Assert.IsFalse(_button.Button.interactable, "NoCell → Disabled");
            Assert.AreEqual(_button.Button.spriteState.disabledSprite, _button.Button.image.overrideSprite, "Disabled 그림");
            Assert.IsTrue(_states.IsTooltipShown, "「칸이 가득 찼습니다」 말풍선");
            TMPro.TMP_Text label = _states.FullTooltip.GetComponentInChildren<TMPro.TMP_Text>(true);
            Assert.AreEqual("칸이 가득 찼습니다", label.text);

            Assert.IsFalse(_summon.RequestSummon(), "Space 길 거부");
            Assert.IsTrue(_states.IsTooltipPunching, "거부 → 말풍선 Punch");
            yield return null;
        }

        [UnityTest]
        public IEnumerator SummonRejected_RaisedOncePerRejectedRequest()
        {
            DrainToNoCoin();
            int rejected = 0;
            SummonBlock last = SummonBlock.None;
            GameEvents.SummonRejected += b => { rejected++; last = b; };

            Assert.IsFalse(_summon.RequestSummon()); // Space 와 같은 길
            Assert.AreEqual(1, rejected, "요청 한 번 = 이벤트 한 번");
            Assert.AreEqual(SummonBlock.NoCoin, last);

            Assert.IsFalse(_summon.RequestSummon(), "같은 프레임 연타 가드");
            Assert.AreEqual(1, rejected, "같은 프레임 두 번째 요청은 거부 이벤트 없음");

            yield return null;
            _button.Button.onClick.Invoke(); // 버튼 길
            Assert.AreEqual(2, rejected, "다음 프레임 버튼 클릭 → 한 번 더");

            _bootstrap.Wallet.Add(1000);
            GameEvents_RaiseCoinChanged();
            yield return null;
            Assert.IsTrue(_summon.RequestSummon(), "코인 있으면 소환");
            Assert.AreEqual(2, rejected, "성공한 요청은 거부 이벤트 없음");
        }

        [UnityTest]
        public IEnumerator CellHoverChanged_NotRaisedWhenSameCell()
        {
            int cell = Place(2, 2, DroneType.Cryo, 1);
            int raised = 0;
            GameEvents.CellHoverChanged += c => raised++;

            Hover(cell);
            Hover(cell);
            Hover(cell);
            Assert.AreEqual(1, raised, "같은 칸이면 한 번만");

            Hover(Grid.IndexOf(0, 0)); // 빈 칸 → -1
            Hover(Grid.IndexOf(1, 0)); // 빈 칸 → -1 그대로
            Assert.AreEqual(2, raised, "-1 로 한 번, 그 뒤 -1 은 재발행 없음");

            int previews = 0;
            GameEvents.DropPreviewChanged += (c, o, h) => previews++;
            Assert.IsTrue(_drag.BeginAt(cell));
            int empty = Grid.IndexOf(4, 0);
            Hover(empty);
            Hover(empty);
            Assert.AreEqual(1, previews, "같은 대상 칸 · 결과면 DropPreviewChanged 한 번만");
            _drag.Cancel();
            Assert.AreEqual(2, previews, "취소 → has=false 한 번");
            yield return null;
        }
    }
}
