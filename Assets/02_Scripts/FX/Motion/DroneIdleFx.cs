using System.Collections.Generic;
using QL.Core;
using QL.Core.Grid;
using QL.Game;
using QL.Game.Drones;
using UnityEngine;

namespace QL.FX.Motion
{
    /// <summary>
    /// 드론 대기 호버링 (MotionFx.prefab, Game.unity [FX] 아래). 칸 뷰(<see cref="DroneBoard.ViewAt"/>)의 자식 Model 이 위아래 Yoyo.
    /// DroneSpawned · DroneMoved · DroneSwapped · DroneMerged → 그 칸 뷰 둥실 시작 (위상 = 칸 번호로 어긋남, 무작위 없음).
    /// DragStarted → 그 뷰 멈춤 + 원래 높이, DragEnded → 다시. 뷰가 풀로 돌아가면(비활성) → <see cref="DroneMotion.Park"/>.
    /// 뷰별 연출 상태(<see cref="DroneMotion"/>)는 여기 한 곳에 두고 MergeFx · SpawnPopFx 가 같이 쓴다 (같은 Model 을 한 곳에서 합쳐 씀).
    /// </summary>
    public sealed class DroneIdleFx : MonoBehaviour
    {
        [SerializeField] private FxConfig fx;
        [Tooltip("씬 인스턴스에서 지정. 비어 있으면 처음 한 번 찾는다")]
        [SerializeField] private DroneBoard board;
        [Tooltip("DroneView 프리팹의 모델 자식 이름 (G07 구조 — 프리팹 공통)")]
        [SerializeField] private string modelChildName = "Model";

        private readonly Dictionary<DroneView, DroneMotion> _byView = new Dictionary<DroneView, DroneMotion>();
        private readonly List<DroneMotion> _all = new List<DroneMotion>();

        public FxConfig Config => fx;
        public DroneBoard Board => EnsureBoard() ? board : null;
        /// <summary>지금까지 만든 뷰별 상태 수 (= 연출을 받은 뷰 수).</summary>
        public int MotionCount => _all.Count;

        /// <summary>뷰별 트윈 총수 (누수 확인용).</summary>
        public int TweenCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _all.Count; i++) n += _all[i].TweenCount;
                return n;
            }
        }

        private void OnEnable()
        {
            GameEvents.DroneSpawned += OnSpawned;
            GameEvents.DroneMoved += OnMoved;
            GameEvents.DroneSwapped += OnMoved;
            GameEvents.DroneMerged += OnMerged;
            GameEvents.DragStarted += OnDragStarted;
            GameEvents.DragEnded += OnDragEnded;
        }

        private void OnDisable()
        {
            GameEvents.DroneSpawned -= OnSpawned;
            GameEvents.DroneMoved -= OnMoved;
            GameEvents.DroneSwapped -= OnMoved;
            GameEvents.DroneMerged -= OnMerged;
            GameEvents.DragStarted -= OnDragStarted;
            GameEvents.DragEnded -= OnDragEnded;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _all.Count; i++) _all[i].Kill();
            _all.Clear();
            _byView.Clear();
        }

        // ── 조회 ──────────────────────────────────────────────

        /// <summary>칸 뷰의 연출 상태 (처음이면 만든다). 칸이 비었거나 준비 전이면 null.</summary>
        public DroneMotion MotionAt(int cell)
        {
            if (fx == null || !EnsureBoard()) return null;
            DroneView view = board.ViewAt(cell);
            if (view == null) return null;
            if (!_byView.TryGetValue(view, out DroneMotion m))
            {
                m = new DroneMotion(view, modelChildName, fx);
                _byView.Add(view, m);
                _all.Add(m);
            }
            return m;
        }

        /// <summary>이미 만든 상태만 (없으면 null, 만들지 않음).</summary>
        public DroneMotion Find(DroneView view)
        {
            return view != null && _byView.TryGetValue(view, out DroneMotion m) ? m : null;
        }

        // ── 이벤트 ────────────────────────────────────────────

        private void OnSpawned(int cell, DroneType type, int grade) => Resume(cell);

        private void OnMoved(int a, int b)
        {
            Resume(a);
            Resume(b);
            ParkReleased();
        }

        private void OnMerged(int from, int to, DroneType type, int grade)
        {
            Resume(to);
            ParkReleased();
        }

        private void OnDragStarted(int from)
        {
            DroneMotion m = MotionAt(from);
            if (m != null) m.StopBob();
        }

        private void OnDragEnded(int from, DropOutcome outcome)
        {
            Resume(from); // Return · Reject 면 같은 칸, 이동 · 교환이면 새 주인 (이미 돌고 있으면 그대로)
            ParkReleased();
        }

        private void Resume(int cell)
        {
            DroneMotion m = MotionAt(cell);
            if (m != null) m.Bob(cell);
        }

        /// <summary>풀로 돌아간(비활성) 뷰는 멈추고 원복 — 다시 꺼내지면 위 이벤트에서 Play.</summary>
        private void ParkReleased()
        {
            for (int i = 0; i < _all.Count; i++)
            {
                DroneMotion m = _all[i];
                if (m.View == null || !m.View.gameObject.activeInHierarchy) m.Park();
            }
        }

        private bool EnsureBoard()
        {
            if (board == null) board = FindAnyObjectByType<DroneBoard>();
            return board != null && board.IsReady;
        }
    }
}
