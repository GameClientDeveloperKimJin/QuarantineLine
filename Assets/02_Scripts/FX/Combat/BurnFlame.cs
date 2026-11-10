using UnityEngine;

namespace QL.FX.Combat
{
    /// <summary>
    /// 잔열 불꽃 하나 (FX_BurnFlame.prefab — 가산 쿼드: FX_Glow_Dot 빛 1 + FX_Spark 불꽃 혀 여럿 + FX_Ring 발밑 링). 트윈 없이 <see cref="BurnFx"/> 가
    /// LateUpdate 마다 <see cref="Show"/> 로 위치 · 시간을 준다 — 빛은 깜빡이고 반짝임은 위로 솟으며 사라지기를 반복 (위상 = 점액 Id).
    /// 점액에 붙이지 않는다 (부모 변경 없음). 색은 MPB _BaseColor.
    /// </summary>
    public sealed class BurnFlame : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Renderer glow;
        [SerializeField] private Renderer[] sparks;
        [Tooltip("발밑 링 (FX_Ring 쿼드) — 바닥에 눕혀 그린다. 위에서 보는 카메라라 몰려 있어도 하나씩 셀 수 있게 (R1 p5 반려)")]
        [SerializeField] private Renderer floor;

        private Transform _root;
        private Transform _glowT;
        private Transform _floorT;
        private static readonly Quaternion FlatRotation = Quaternion.Euler(90f, 0f, 0f);
        private Transform[] _sparkT;
        private MaterialPropertyBlock _block;

        public bool Active => gameObject.activeSelf;
        /// <summary>따라가는 점액 Id (-1 = 없음).</summary>
        public int TargetId { get; private set; } = -1;

        internal void Init()
        {
            _root = transform;
            _block = new MaterialPropertyBlock();
            _glowT = glow.transform;
            _floorT = floor != null ? floor.transform : null;
            _sparkT = new Transform[sparks.Length];
            for (int i = 0; i < sparks.Length; i++) _sparkT[i] = sparks[i].transform;
            gameObject.SetActive(false);
        }

        /// <param name="foot">점액 발 위치 (월드)</param>
        /// <param name="scale">점액 크기 (EnemyData.scale × cellSize)</param>
        /// <param name="toCamera">카메라 쪽 단위 벡터</param>
        internal void Show(int id, Vector3 foot, float scale, Quaternion cameraRotation, Vector3 toCamera, float time, FxConfig fx)
        {
            TargetId = id;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            Vector3 center = foot + Vector3.up * (fx.burnHeight * scale) + toCamera * (fx.burnCameraOffset * scale);
            _root.SetPositionAndRotation(center, cameraRotation);

            float phase = id * 1.7f;
            float w = time * fx.burnFlickerSpeed + phase;
            float flicker = fx.burnFlickerAmp * (0.6f * Mathf.Sin(w) + 0.4f * Mathf.Sin(w * 2.3f + 1.1f));
            float g = fx.burnGlowSize * scale * (1f + flicker);
            _glowT.localScale = new Vector3(g, g, g);
            Color c = fx.burnColor;
            c.a = fx.burnGlowAlpha * Mathf.Clamp01(1f + flicker);
            Paint(glow, c);

            // 발밑 링 — 점액 폭 × burnFloorSize, 바닥에 눕혀 깜빡임 (점액 렌더러 · MPB 는 건드리지 않는다)
            if (_floorT != null)
            {
                _floorT.SetPositionAndRotation(new Vector3(foot.x, fx.worldFxHeight, foot.z), FlatRotation);
                float f = fx.burnFloorSize * scale * (1f + 0.5f * flicker);
                _floorT.localScale = new Vector3(f, f, f);
                c = fx.burnFloorColor;
                c.a = fx.burnFloorAlpha * Mathf.Clamp01(1f + 2f * flicker);
                Paint(floor, c);
            }

            // 불꽃 혀 — 머리 위(burnHeight)에서 burnSparkRise 까지 솟으며 작아지고 사라짐. 부채꼴보다 붉은 burnSparkColor
            int n = sparks.Length;
            Vector3 right = cameraRotation * Vector3.right;
            Vector3 head = foot + Vector3.up * (fx.burnHeight * scale) + toCamera * (fx.burnCameraOffset * scale);
            for (int i = 0; i < n; i++)
            {
                float u = Mathf.Repeat(time / fx.burnSparkPeriod + (float)i / n + id * 0.37f, 1f);
                float side = Mathf.Sin(i * 2.1f + phase) * fx.burnSparkSpread * scale * (1f - 0.5f * u);
                Transform st = _sparkT[i];
                st.position = head + Vector3.up * (fx.burnSparkRise * scale * u) + right * side;
                float s = fx.burnSparkSize * scale * (1f - 0.6f * u);
                st.localScale = new Vector3(s, s, s);
                c = fx.burnSparkColor;
                c.a = fx.burnSparkAlpha * (1f - u * u);
                Paint(sparks[i], c);
            }
        }

        internal void Hide()
        {
            TargetId = -1;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        private void Paint(Renderer r, Color c)
        {
            _block.SetColor(BaseColorId, c);
            r.SetPropertyBlock(_block);
        }
    }
}
