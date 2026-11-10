using QL.Core;
using QL.Game;
using QL.Game.Bootstrap;
using UnityEngine;

namespace QL.FX.Motion
{
    /// <summary>
    /// 소환 팝 (MotionFx.prefab). DroneSpawned(cell, type, grade) → 그 칸 뷰 Model 0 → 1.1 → 1 (<see cref="DroneMotion.PlayPop"/>)
    /// + 칸 바닥에 작은 FX_Ring (색 = UnitData.tintColor). 소리는 U08.
    /// </summary>
    public sealed class SpawnPopFx : MonoBehaviour
    {
        [SerializeField] private FxConfig fx;
        [SerializeField] private DroneIdleFx motions;
        [SerializeField] private FxPool pool;
        [Tooltip("씬 인스턴스에서 지정. 비어 있으면 처음 한 번 찾는다")]
        [SerializeField] private GameBootstrap bootstrap;

        /// <summary>마지막 소환 칸 (-1 = 없음).</summary>
        public int LastCell { get; private set; } = -1;

        private void Awake()
        {
            if (motions == null) motions = GetComponent<DroneIdleFx>();
        }

        private void OnEnable() => GameEvents.DroneSpawned += OnSpawned;
        private void OnDisable() => GameEvents.DroneSpawned -= OnSpawned;

        private void OnSpawned(int cell, DroneType type, int grade)
        {
            if (!EnsureReady()) return;
            DroneMotion m = motions.MotionAt(cell);
            if (m == null) return;
            m.PlayPop();
            LastCell = cell;

            if (pool == null) return;
            float size = bootstrap.Space.CellSize;
            Vector3 w = bootstrap.Space.CellToWorld(cell);
            Color tint = motions.Board.TintOf(type, grade);
            tint.a = fx.spawnRingAlpha;

            var p = new FxSpriteParams
            {
                Position = new Vector3(w.x, fx.worldFxHeight, w.z),
                Travel = Vector3.zero,
                SizeFrom = fx.spawnRingSizeFrom * size,
                SizeTo = fx.spawnRingSizeTo * size,
                Color = tint,
                Duration = fx.spawnRingDuration,
            };
            pool.Play(FxKind.Ring, in p);
        }

        private bool EnsureReady()
        {
            if (bootstrap == null) bootstrap = FindAnyObjectByType<GameBootstrap>();
            if (pool == null) pool = FindAnyObjectByType<FxPool>();
            return fx != null && motions != null && bootstrap != null && bootstrap.IsReady;
        }
    }
}
