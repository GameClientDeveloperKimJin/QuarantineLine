using System;
using System.Collections.Generic;
using QL.Core;
using QL.Core.Grid;
using QL.Data;
using QL.Game.Bootstrap;
using QL.Game.Stage;
using UnityEngine;

namespace QL.Game.Drones
{
    /// <summary>
    /// 칸 인덱스 ↔ DroneView. Core(GridModel) 상태를 화면에 맞추기만 한다 — 판정 없음.
    /// 뷰는 풀에서 꺼내고 반납 (프리팹마다 CellCount 개를 Awake 에 미리 생성, 런타임 Instantiate 없음).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class DroneBoard : MonoBehaviour
    {
        [SerializeField] private GameBootstrap bootstrap;
        [Tooltip("풀에서 쉬는 뷰를 둘 부모. 비우면 이 오브젝트")]
        [SerializeField] private Transform poolRoot;

        private GridModel _grid;
        private GridSpace _space;
        private DroneView[] _views;
        private UnitData[][] _units;   // [(int)type][grade - 1]
        private readonly Dictionary<GameObject, Stack<DroneView>> _pool = new Dictionary<GameObject, Stack<DroneView>>();

        public bool IsReady => _views != null;
        public int CellCount => _views != null ? _views.Length : 0;

        private void Awake()
        {
            if (bootstrap == null || !bootstrap.IsReady)
            {
                Debug.LogError("[DroneBoard] GameBootstrap 이 없거나 준비되지 않았다", this);
                enabled = false;
                return;
            }

            _grid = bootstrap.Grid;
            _space = bootstrap.Space;
            if (poolRoot == null) poolRoot = transform;

            if (!BuildUnitTable()) { enabled = false; return; }
            _views = new DroneView[_grid.CellCount];
        }

        private bool BuildUnitTable()
        {
            Array types = Enum.GetValues(typeof(DroneType));
            int maxType = 0;
            foreach (DroneType t in types) maxType = Mathf.Max(maxType, (int)t);
            _units = new UnitData[maxType + 1][];
            foreach (DroneType t in types)
                _units[(int)t] = new UnitData[bootstrap.Catalog.MaxGrade(t)];

            IReadOnlyList<UnitData> source = bootstrap.Data.Units;
            for (int i = 0; i < source.Count; i++)
            {
                UnitData u = source[i];
                if (u == null) continue;
                _units[(int)u.DroneType][u.Grade - 1] = u;
            }

            for (int t = 0; t < _units.Length; t++)
            {
                UnitData[] grades = _units[t];
                if (grades == null) continue;
                for (int g = 0; g < grades.Length; g++)
                {
                    UnitData u = grades[g];
                    if (u == null || u.Prefab == null || u.Prefab.GetComponent<DroneView>() == null)
                    {
                        Debug.LogError("[DroneBoard] UnitData (" + (DroneType)t + ", " + (g + 1) + ") 의 prefab 이 없거나 DroneView 가 없다", this);
                        return false;
                    }
                    if (!_pool.ContainsKey(u.Prefab)) Prewarm(u.Prefab);
                }
            }
            return true;
        }

        private void Prewarm(GameObject prefab)
        {
            int count = _grid.CellCount;
            var stack = new Stack<DroneView>(count);
            for (int i = 0; i < count; i++)
            {
                GameObject go = Instantiate(prefab, poolRoot);
                go.name = prefab.name + "_" + i.ToString("00");
                go.SetActive(false);
                DroneView view = go.GetComponent<DroneView>();
                view.Init(prefab, _space.CellSize);
                stack.Push(view);
            }
            _pool.Add(prefab, stack);
        }

        // ── 조회 ──────────────────────────────────────────────

        /// <summary>칸의 뷰. 없으면 null.</summary>
        public DroneView ViewAt(int index)
        {
            if (_views == null || index < 0 || index >= _views.Length) return null;
            return _views[index];
        }

        /// <summary>집어 든 드론의 뷰.</summary>
        public bool TryGetDragged(out DroneView view)
        {
            view = _grid != null ? ViewAt(_grid.DraggingIndex) : null;
            return view != null;
        }

        /// <summary>UnitData 의 tintColor. (type, grade) 가 없으면 흰색.</summary>
        public Color TintOf(DroneType type, int grade)
        {
            UnitData u = FindUnit(type, grade);
            return u != null ? u.TintColor : Color.white;
        }

        // ── Core 와 맞추기 ────────────────────────────────────

        /// <summary>Grid.Get(index) 를 읽어 뷰를 맞춘다 (있으면 Show, 없으면 반납). 집어 든 칸은 위치를 건드리지 않는다.</summary>
        public void SyncCell(int index)
        {
            if (_views == null || index < 0 || index >= _views.Length) return;

            Cell cell = _grid.Get(index);
            DroneView view = _views[index];

            if (!cell.HasDrone)
            {
                if (view != null) Release(index);
                return;
            }

            UnitData unit = FindUnit(cell.Type, cell.Grade);
            if (unit == null) return;

            if (view != null && view.SourcePrefab != unit.Prefab)
            {
                Release(index);
                view = null;
            }
            if (view == null)
            {
                view = Acquire(unit.Prefab);
                if (view == null) return;
                _views[index] = view;
            }

            view.Show(cell.Type, cell.Grade, unit.TintColor);
            if (!_grid.IsDragging(index)) PutDown(index);
        }

        public void SyncAll()
        {
            if (_views == null) return;
            for (int i = 0; i < _views.Length; i++) SyncCell(i);
        }

        /// <summary>GridModel.Drop 결과를 뷰에 적용한 뒤 관련 칸을 Core 와 다시 맞춘다.</summary>
        public void Apply(in DropResult r)
        {
            if (_views == null) return;
            int from = r.From;
            int to = r.To;

            switch (r.Outcome)
            {
                case DropOutcome.Move:
                    if (_views[to] != null) Release(to);
                    _views[to] = _views[from];
                    _views[from] = null;
                    PutDown(to);
                    break;

                case DropOutcome.Swap:
                case DropOutcome.SwapClean:
                    DroneView tmp = _views[to];
                    _views[to] = _views[from];
                    _views[from] = tmp;
                    PutDown(from);
                    PutDown(to);
                    break;

                case DropOutcome.Merge:
                case DropOutcome.MergeClean:
                    Release(from);
                    if (_views[to] != null) _views[to].Show(r.Type, r.NewGrade, TintOf(r.Type, r.NewGrade));
                    PutDown(to);
                    break;

                default: // Reject · Return
                    PutDown(from);
                    break;
            }

            SyncCell(from);
            if (to >= 0) SyncCell(to);
        }

        /// <summary>칸 위치(hoverHeight)로 내려놓는다. 뷰가 없으면 아무 일 없음.</summary>
        public void PutDown(int index)
        {
            DroneView view = ViewAt(index);
            if (view == null) return;
            view.SetLifted(false);
            view.PlaceOver(_space.CellToWorld(index));
        }

        // ── 풀 ────────────────────────────────────────────────

        private DroneView Acquire(GameObject prefab)
        {
            if (!_pool.TryGetValue(prefab, out Stack<DroneView> stack) || stack.Count == 0)
            {
                Debug.LogError("[DroneBoard] 풀이 비었다: " + prefab.name, this);
                return null;
            }
            return stack.Pop();
        }

        private void Release(int index)
        {
            DroneView view = _views[index];
            _views[index] = null;
            if (view == null) return;
            view.SetLifted(false);
            view.gameObject.SetActive(false);
            _pool[view.SourcePrefab].Push(view);
        }

        private UnitData FindUnit(DroneType type, int grade)
        {
            int t = (int)type;
            if (_units == null || t < 0 || t >= _units.Length || _units[t] == null) return null;
            UnitData[] grades = _units[t];
            int g = grade - 1;
            return g >= 0 && g < grades.Length ? grades[g] : null;
        }
    }
}
