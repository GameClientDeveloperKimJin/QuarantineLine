using QL.Core;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Drones;
using UnityEngine;

namespace QL.FX.Motion
{
    /// <summary>
    /// 머지 「펑」 (MotionFx.prefab). DroneMerged(from, to, type, newGrade) →
    /// ① to 뷰 Model 이 from 쪽으로 밀려 있다가 제자리 ② 0.8 → 1.25 → 1 ③ 약한 Punch — <see cref="DroneMotion.PlayMerge"/>,
    /// ④ 칸 위 FX_Ring 퍼짐 + FX_Spark 여럿 + FX_Glow_Dot 섬광 (색 = 새 등급 UnitData.tintColor — 아트 프리팹은 MPB 틴트를 끄므로 머티리얼 대신 이펙트로).
    /// 전체 길이 = GameConfig.mergeLockDuration (S3 규칙 3 — 판정을 기다리게 하지 않는다). 이펙트 수명도 그 비율.
    /// 등급 부품(링 · 포드) 토글은 R3 DroneView.Show 가 같은 프레임에 이미 함 → 섬광 첫 프레임과 겹친다.
    /// </summary>
    public sealed class MergeFx : MonoBehaviour
    {
        [SerializeField] private FxConfig fx;
        [SerializeField] private DroneIdleFx motions;
        [SerializeField] private FxPool pool;
        [Tooltip("씬 인스턴스에서 지정. 비어 있으면 처음 한 번 찾는다")]
        [SerializeField] private GameBootstrap bootstrap;

        /// <summary>마지막 머지 칸 (-1 = 없음).</summary>
        public int LastCell { get; private set; } = -1;
        /// <summary>머지 전체 길이 = GameConfig.mergeLockDuration (준비 전 0).</summary>
        public float TotalDuration => EnsureReady() ? bootstrap.Data.Config.MergeLockDuration : 0f;

        private void Awake()
        {
            if (motions == null) motions = GetComponent<DroneIdleFx>();
        }

        private void OnEnable() => GameEvents.DroneMerged += OnMerged;
        private void OnDisable() => GameEvents.DroneMerged -= OnMerged;

        /// <summary>그 칸 뷰의 머지 Sequence 길이 (아직 없으면 0).</summary>
        public float SequenceDurationAt(int cell)
        {
            DroneMotion m = motions != null ? motions.MotionAt(cell) : null;
            return m != null ? m.MergeDuration : 0f;
        }

        private void OnMerged(int from, int to, DroneType type, int newGrade)
        {
            if (!EnsureReady()) return;
            DroneMotion m = motions.MotionAt(to);
            if (m == null) return;

            float total = bootstrap.Data.Config.MergeLockDuration;
            float cell = bootstrap.Space.CellSize;
            Vector3 toWorld = bootstrap.Space.CellToWorld(to);
            Vector3 dir = bootstrap.Space.CellToWorld(from) - toWorld;
            dir.y = 0f;
            Vector3 pull = dir.sqrMagnitude > 1e-6f ? dir.normalized * (fx.mergePullDistance * cell) : Vector3.zero;
            m.PlayMerge(pull, total);
            LastCell = to;

            if (pool == null) return;
            Color tint = motions.Board.TintOf(type, newGrade);
            Vector3 center = m.View.transform.position; // 읽기만 (뜬 높이 = 섬광 · 반짝임 중심)
            // 섬광 · 반짝임은 드론 몸체(불투명)에 가리지 않게 카메라 쪽으로 당겨 그린다
            Camera cam = pool.ViewCamera;
            if (cam != null) center += (cam.transform.position - center).normalized * (fx.mergeFxCameraOffset * cell);

            var p = new FxSpriteParams();

            // 링 — 바닥에서 퍼짐
            p.Position = new Vector3(toWorld.x, fx.worldFxHeight, toWorld.z);
            p.Travel = Vector3.zero;
            p.SizeFrom = fx.mergeRingSizeFrom * cell;
            p.SizeTo = fx.mergeRingSizeTo * cell;
            p.Color = Bright(tint, fx.mergeRingIntensity, fx.mergeRingAlpha);
            p.Duration = total * fx.mergeRingLife;
            pool.Play(FxKind.Ring, in p);

            // 섬광 — 드론 위치에서 확 커졌다 사라짐
            p.Position = center;
            p.SizeFrom = fx.mergeGlowSizeFrom * cell;
            p.SizeTo = fx.mergeGlowSizeTo * cell;
            p.Color = Bright(tint, fx.mergeGlowIntensity, fx.mergeGlowAlpha);
            p.Duration = total * fx.mergeGlowLife;
            pool.Play(FxKind.GlowDot, in p);

            // 반짝임 — 원 둘레로 고르게 (칸 번호로 시작 각만 돌림, 무작위 없음)
            int n = fx.mergeSparkCount;
            float step = n > 0 ? Mathf.PI * 2f / n : 0f;
            float start = to * 0.5f * step;
            p.SizeFrom = fx.mergeSparkSize * cell;
            p.SizeTo = fx.mergeSparkSizeEnd * cell;
            p.Color = Bright(tint, fx.mergeSparkIntensity, tint.a);
            p.Duration = total * fx.mergeSparkLife;
            for (int i = 0; i < n; i++)
            {
                float a = start + i * step;
                p.Travel = new Vector3(Mathf.Cos(a) * fx.mergeSparkDistance, fx.mergeSparkRise, Mathf.Sin(a) * fx.mergeSparkDistance) * cell;
                pool.Play(FxKind.Spark, in p);
            }
        }

        /// <summary>틴트 × 밝기 (가산 재질 — 1 보다 크면 더 밝게), 알파 = 시작 세기.</summary>
        private static Color Bright(Color c, float intensity, float alpha)
        {
            return new Color(c.r * intensity, c.g * intensity, c.b * intensity, alpha);
        }

        private bool EnsureReady()
        {
            if (bootstrap == null) bootstrap = FindAnyObjectByType<GameBootstrap>();
            if (pool == null) pool = FindAnyObjectByType<FxPool>();
            return fx != null && motions != null && bootstrap != null && bootstrap.IsReady && bootstrap.Data != null && bootstrap.Data.Config != null;
        }
    }
}
