using System;
using QL.Core.Stats;
using QL.Core.Units;

namespace QL.Core.Grid
{
    /// <summary>
    /// 배치 칸 전체의 상태 (드론 · 오염 · 머지 잠금 · 드래그).
    /// 판정은 <see cref="MergeRule.Resolve"/> 에 맡기고 결과만 적용한다. 이벤트 없음 — 결과는 반환값으로만.
    /// </summary>
    public sealed class GridModel
    {
        private const int None = -1;

        private readonly UnitCatalog _catalog;
        private readonly float _mergeLockDuration;

        private readonly bool[] _hasDrone;
        private readonly DroneType[] _type;
        private readonly int[] _grade;
        private readonly bool[] _contaminated;
        private readonly float[] _lockRemaining;

        public int LaneCount { get; }
        public int RowCount { get; }
        public int CellCount { get; }

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
            if (DraggingIndex == None) throw new InvalidOperationException("드래그 중이 아니다");

            int from = DraggingIndex;
            DraggingIndex = None;

            if (to >= CellCount) to = None;

            Cell fromCell = Get(from);
            Cell toCell = to == None ? default : Get(to);
            DropOutcome outcome = MergeRule.Resolve(fromCell, toCell, from, to, _catalog);

            DroneType type = fromCell.Type;
            int grade = fromCell.Grade;

            switch (outcome)
            {
                case DropOutcome.Move:
                    ClearDrone(from);
                    SetDrone(to, type, grade);
                    break;

                case DropOutcome.Merge:
                    grade += 1;
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
                    ClearDrone(from);
                    SetDrone(to, type, grade);
                    _contaminated[to] = false;
                    _lockRemaining[to] = _mergeLockDuration;
                    break;

                // Reject · Return: 칸 상태 그대로
            }

            return new DropResult(outcome, from, to, type, grade);
        }

        /// <summary>드래그 중이 아니어도 안전. 칸 상태는 바꾸지 않는다.</summary>
        public void CancelDrag()
        {
            DraggingIndex = None;
        }

        // ── 오염 (상태 토글만) ────────────────────────────────

        /// <exception cref="ArgumentOutOfRangeException">index 가 칸 범위 밖</exception>
        public void Contaminate(int index)
        {
            CheckIndex(index);
            _contaminated[index] = true;
        }

        /// <exception cref="ArgumentOutOfRangeException">index 가 칸 범위 밖</exception>
        public void Clean(int index)
        {
            CheckIndex(index);
            _contaminated[index] = false;
        }

        // ── 시간 ──────────────────────────────────────────────

        /// <summary>머지 잠금 남은 시간을 줄인다. 0 이하가 되면 해제. dt ≤ 0 은 무시.</summary>
        public void Tick(float dt)
        {
            if (dt <= 0f) return;

            for (int i = 0; i < CellCount; i++)
            {
                if (_lockRemaining[i] <= 0f) continue;
                float left = _lockRemaining[i] - dt;
                _lockRemaining[i] = left > 0f ? left : 0f;
            }
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
