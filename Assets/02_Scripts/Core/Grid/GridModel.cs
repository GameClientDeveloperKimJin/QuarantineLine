using System;
using QL.Core.Stats;
using QL.Core.Support;
using QL.Core.Units;

namespace QL.Core.Grid
{
    /// <summary>
    /// 배치 칸 전체의 상태 (드론 · 오염 + 남은 시간 · 머지 잠금 · 드래그).
    /// 판정은 <see cref="MergeRule.Resolve"/> 에 맡기고 결과만 적용한다. 이벤트 없음 — 결과는 반환값으로만.
    /// </summary>
    public sealed class GridModel
    {
        private const int None = -1;

        private readonly UnitCatalog _catalog;
        private readonly float _mergeLockDuration;
        private readonly float _contaminateDuration;

        private readonly bool[] _hasDrone;
        private readonly DroneType[] _type;
        private readonly int[] _grade;
        private readonly bool[] _contaminated;
        private readonly float[] _lockRemaining;
        private readonly float[] _contaminateRemaining;
        private readonly int[] _laneCandidates;
        private readonly int[] _tickCleaned;

        public int LaneCount { get; }
        public int RowCount { get; }
        public int CellCount { get; }

        /// <summary>마지막 <see cref="Tick"/> 에서 자연 청소(마름)된 칸 수. 칸 번호는 <see cref="TickCleanedCell"/>.</summary>
        public int TickCleanedCount { get; private set; }

        /// <summary>집어 든 칸. 없으면 -1.</summary>
        public int DraggingIndex { get; private set; } = None;

        /// <exception cref="ArgumentNullException">catalog 가 null</exception>
        /// <exception cref="ArgumentOutOfRangeException">레인 · 줄 수가 1 미만</exception>
        public GridModel(RuleConfig config, UnitCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            if (config.LaneCount < 1) throw new ArgumentOutOfRangeException(nameof(config), "LaneCount 가 1 미만");
            if (config.RowCount < 1) throw new ArgumentOutOfRangeException(nameof(config), "RowCount 가 1 미만");

            LaneCount = config.LaneCount;
            RowCount = config.RowCount;
            CellCount = LaneCount * RowCount;
            _mergeLockDuration = config.MergeLockDuration;

            _hasDrone = new bool[CellCount];
            _type = new DroneType[CellCount];
            _grade = new int[CellCount];
            _contaminated = new bool[CellCount];
            _lockRemaining = new float[CellCount];
            _contaminateDuration = config.ContaminateDuration;
            _contaminateRemaining = new float[CellCount];
            _laneCandidates = new int[RowCount];
            _tickCleaned = new int[CellCount];
        }

        // ── 좌표 ──────────────────────────────────────────────

        /// <summary>row * LaneCount + lane. 범위 밖이면 -1.</summary>
        public int IndexOf(int lane, int row)
        {
            if (lane < 0 || lane >= LaneCount || row < 0 || row >= RowCount) return None;
            return row * LaneCount + lane;
        }

        /// <exception cref="ArgumentOutOfRangeException">index 가 칸 범위 밖</exception>
        public void CoordOf(int index, out int lane, out int row)
        {
            CheckIndex(index);
            lane = index % LaneCount;
            row = index / LaneCount;
        }

        /// <exception cref="ArgumentOutOfRangeException">index 가 칸 범위 밖</exception>
        public Cell Get(int index)
        {
            CheckIndex(index);
            return new Cell(_hasDrone[index], _type[index], _grade[index], _contaminated[index], _lockRemaining[index] > 0f);
        }

        /// <summary>드론 없고 오염 안 된 칸이 하나라도 있는가. 드래그 중인 원래 칸은 드론이 있는 칸으로 센다.</summary>
        public bool HasEmptyCell
        {
            get
            {
                for (int i = 0; i < CellCount; i++)
                    if (!_hasDrone[i] && !_contaminated[i]) return true;
                return false;
            }
        }

        // ── 배치 ──────────────────────────────────────────────

        /// <exception cref="ArgumentOutOfRangeException">index 가 칸 범위 밖</exception>
        /// <exception cref="ArgumentException">카탈로그에 없는 (type, grade)</exception>
        /// <exception cref="InvalidOperationException">드론이 있거나 오염된 칸</exception>
        public void Place(int index, DroneType type, int grade)
        {
            CheckIndex(index);
            if (!_catalog.TryGet(type, grade, out _))
                throw new ArgumentException($"카탈로그에 없는 ({type}, {grade})", nameof(grade));
            if (_hasDrone[index] || _contaminated[index])
                throw new InvalidOperationException($"칸 {index} 는 빈 비오염 칸이 아니다");

            SetDrone(index, type, grade);
        }

        // ── 드래그 ────────────────────────────────────────────

        /// <summary>집어 든 드론의 칸인가 (집어 든 드론은 공격하지 않는다).</summary>
        public bool IsDragging(int index)
        {
            return DraggingIndex != None && DraggingIndex == index;
        }

        /// <summary>범위 밖 · 드론 없음 · 잠김 · 이미 드래그 중이면 false.</summary>
        public bool BeginDrag(int from)
        {
            if (DraggingIndex != None) return false;
            if (from < 0 || from >= CellCount) return false;
            if (!_hasDrone[from]) return false;
            if (_lockRemaining[from] > 0f) return false;

            DraggingIndex = from;
            return true;
        }

        /// <summary>
        /// 판정 → 적용 → 드래그 종료. 판정은 놓는 순간의 칸 상태로 한다.
        /// to 가 음수거나 칸 범위 밖이면 그리드 밖으로 보고 Return (To = -1).
        /// </summary>
        /// <exception cref="InvalidOperationException">드래그 중이 아님</exception>
        public DropResult Drop(int to)
        {
            return Drop(to, null);
        }

        /// <summary>
        /// (K02 · C15) <see cref="Drop(int)"/> + 지원 카드 MergeGradeBoost — 1등급끼리 머지 (Merge · MergeClean) 면
        /// 1회 써서 등급을 효과 값만큼 더 올린다 (최고 등급에서 멈춤). 2등급 이상 머지 · Swap · SwapClean 에서는 쓰지 않는다.
        /// 더 올릴 등급이 없으면 (최고 등급 = 머지 결과) 쓰지 않는다. mods 가 null 이면 Drop(int) 와 같다.
        /// </summary>
        /// <exception cref="InvalidOperationException">드래그 중이 아님</exception>
        public DropResult Drop(int to, RunModifiers mods)
        {
            if (DraggingIndex == None) throw new InvalidOperationException("드래그 중이 아니다");

            int from = DraggingIndex;
            DraggingIndex = None;

            if (to >= CellCount) to = None;

            Cell fromCell = Get(from);
            Cell toCell = to == None ? default : Get(to);
            DropOutcome outcome = MergeRule.Resolve(fromCell, toCell, from, to, _catalog);

            DroneType type = fromCell.Type;
            int grade = fromCell.Grade;
            bool boosted = false;

            switch (outcome)
            {
                case DropOutcome.Move:
                    ClearDrone(from);
                    SetDrone(to, type, grade);
                    break;

                case DropOutcome.Merge:
                    grade += 1;
                    boosted = TryBoost(mods, type, ref grade);
                    ClearDrone(from);
                    SetDrone(to, type, grade);
                    _lockRemaining[to] = _mergeLockDuration;
                    break;

                case DropOutcome.Swap:
                    SetDrone(from, toCell.Type, toCell.Grade);
                    SetDrone(to, type, grade);
                    break;

                case DropOutcome.MergeClean:
                    grade += 1;
                    boosted = TryBoost(mods, type, ref grade);
                    ClearDrone(from);
                    SetDrone(to, type, grade);
                    _contaminated[to] = false;
                    _contaminateRemaining[to] = 0f;
                    _lockRemaining[to] = _mergeLockDuration;
                    break;

                case DropOutcome.SwapClean:
                    // 같은 종류 · 같은 등급이라 교환해도 칸 내용은 같다 — 놓은 칸 오염만 씻는다 (잠금 없음, Swap 과 같게)
                    SetDrone(from, toCell.Type, toCell.Grade);
                    SetDrone(to, type, grade);
                    _contaminated[to] = false;
                    _contaminateRemaining[to] = 0f;
                    break;

                // Reject · Return: 칸 상태 그대로
            }

            return new DropResult(outcome, from, to, type, grade, boosted);
        }

        /// <summary>grade = 머지 직후 등급 (원래 + 1). 올렸으면 소모하고 true.</summary>
        private bool TryBoost(RunModifiers mods, DroneType type, ref int grade)
        {
            if (mods == null || grade - 1 != MergeRule.BoostSourceGrade) return false;
            if (!mods.TryGetCounted(StatKey.MergeGradeBoost, out CardEffectSpec effect)) return false;

            int extra = (int)Math.Round(effect.Value, MidpointRounding.AwayFromZero);
            int target = MergeRule.BoostedGrade(_catalog, type, grade, extra);
            if (target <= grade) return false;

            mods.TryConsume(StatKey.MergeGradeBoost);
            grade = target;
            return true;
        }

        /// <summary>드래그 중이 아니어도 안전. 칸 상태는 바꾸지 않는다.</summary>
        public void CancelDrag()
        {
            DraggingIndex = None;
        }

        // ── 오염 · 청소 (O01) ─────────────────────────────────

        /// <summary>
        /// 칸을 오염시키고 남은 시간을 <see cref="RuleConfig.ContaminateDuration"/> 으로 (이미 오염이면 시간만 다시).
        /// ContaminateDuration ≤ 0 이면 자연 청소 없음 (머지 · <see cref="Clean"/> 으로만 지워짐).
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">index 가 칸 범위 밖</exception>
        public void Contaminate(int index)
        {
            Contaminate(index, _contaminateDuration);
        }

        /// <summary>
        /// (G14 튜토리얼) <see cref="Contaminate(int)"/> 과 같되 남은 시간을 duration 으로 (이미 오염이면 시간만 다시).
        /// duration ≤ 0 (또는 NaN) 이면 자연 청소 없음 (머지 · <see cref="Clean"/> 으로만 지워짐).
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">index 가 칸 범위 밖</exception>
        public void Contaminate(int index, float duration)
        {
            CheckIndex(index);
            _contaminated[index] = true;
            _contaminateRemaining[index] = duration > 0f ? duration : 0f;
        }

        /// <summary>
        /// 점액이 해치에 닿았을 때 — 그 레인의 아직 오염 안 된 칸 중 무작위 1칸 (<see cref="Contamination.PickCell"/>).
        /// 레인 전체가 오염이면 난수를 뽑지 않고 -1. 할당 없음.
        /// </summary>
        /// <returns>오염된 칸 번호, 없으면 -1</returns>
        /// <exception cref="ArgumentOutOfRangeException">lane 이 레인 범위 밖</exception>
        /// <exception cref="ArgumentNullException">rng 가 null</exception>
        public int ContaminateInLane(int lane, IRandom rng)
        {
            int index = Contamination.PickCell(this, lane, rng, _laneCandidates);
            if (index != None) Contaminate(index);
            return index;
        }

        /// <exception cref="ArgumentOutOfRangeException">index 가 칸 범위 밖</exception>
        public void Clean(int index)
        {
            CheckIndex(index);
            _contaminated[index] = false;
            _contaminateRemaining[index] = 0f;
        }

        /// <summary>오염 남은 시간 (오염 아니거나 자연 청소 없음이면 0).</summary>
        /// <exception cref="ArgumentOutOfRangeException">index 가 칸 범위 밖</exception>
        public float ContaminationRemaining(int index)
        {
            CheckIndex(index);
            return _contaminateRemaining[index];
        }

        /// <summary>마지막 Tick 에서 자연 청소된 i 번째 칸 (0 ≤ i &lt; <see cref="TickCleanedCount"/>, 칸 번호 오름차순).</summary>
        /// <exception cref="ArgumentOutOfRangeException">i 가 범위 밖</exception>
        public int TickCleanedCell(int i)
        {
            if (i < 0 || i >= TickCleanedCount)
                throw new ArgumentOutOfRangeException(nameof(i), i, $"0 ~ {TickCleanedCount - 1}");
            return _tickCleaned[i];
        }

        // ── 시간 ──────────────────────────────────────────────

        /// <summary>
        /// 머지 잠금 · 오염 남은 시간을 줄인다. 0 이하가 되면 잠금 해제 · 자연 청소.
        /// 자연 청소된 칸은 <see cref="TickCleanedCount"/> · <see cref="TickCleanedCell"/> 로 (다음 Tick 까지 유지). dt ≤ 0 은 무시 (목록은 비운다).
        /// 할당 없음.
        /// </summary>
        /// <returns>이번 Tick 에 자연 청소된 칸 수</returns>
        public int Tick(float dt)
        {
            return Tick(dt, dt);
        }

        /// <summary>
        /// (G14) <see cref="Tick(float)"/> 과 같되 머지 잠금은 dt, 오염 남은 시간은 contaminationDt 만큼 줄인다
        /// (튜토리얼 설명 중 오염만 멈출 때 contaminationDt = 0). 각각 ≤ 0 이면 그쪽은 그대로. 할당 없음.
        /// </summary>
        /// <returns>이번 Tick 에 자연 청소된 칸 수</returns>
        public int Tick(float dt, float contaminationDt)
        {
            TickCleanedCount = 0;
            bool locks = dt > 0f;
            bool dry = contaminationDt > 0f;
            if (!locks && !dry) return 0;

            for (int i = 0; i < CellCount; i++)
            {
                if (locks && _lockRemaining[i] > 0f)
                {
                    float left = _lockRemaining[i] - dt;
                    _lockRemaining[i] = left > 0f ? left : 0f;
                }

                if (dry && _contaminated[i] && _contaminateRemaining[i] > 0f)
                {
                    float left = _contaminateRemaining[i] - contaminationDt;
                    if (left > 0f)
                    {
                        _contaminateRemaining[i] = left;
                    }
                    else
                    {
                        _contaminateRemaining[i] = 0f;
                        _contaminated[i] = false;
                        _tickCleaned[TickCleanedCount++] = i;
                    }
                }
            }

            return TickCleanedCount;
        }

        // ── 내부 ──────────────────────────────────────────────

        private void SetDrone(int index, DroneType type, int grade)
        {
            _hasDrone[index] = true;
            _type[index] = type;
            _grade[index] = grade;
        }

        private void ClearDrone(int index)
        {
            _hasDrone[index] = false;
            _type[index] = default;
            _grade[index] = default;
        }

        private void CheckIndex(int index)
        {
            if (index < 0 || index >= CellCount)
                throw new ArgumentOutOfRangeException(nameof(index), index, $"칸 범위는 0 ~ {CellCount - 1}");
        }
    }
}
