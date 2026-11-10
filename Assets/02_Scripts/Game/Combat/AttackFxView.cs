using QL.Core;
using UnityEngine;

namespace QL.Game.Combat
{
    /// <summary>
    /// 자리표시 공격 표시 하나 (풀 대상). 받은 월드 위치에 그리기만 한다 — 타겟 · 사거리 계산 없음 (DroneCombat 이 넘긴다).
    /// 선(LineRenderer) + 바닥 쿼드 1장, 재질 1개 공유(Mat_AttackFx, 가산). 텍스처 · 색은 MaterialPropertyBlock.
    /// 텍스처 · 블렌딩 · 피벗은 05_Docs/_아트납품.md 「A10」. 다듬는 연출은 U04~U06.
    /// </summary>
    public sealed class AttackFxView : MonoBehaviour
    {
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private LineRenderer line;
        [SerializeField] private MeshRenderer quad;

        [Header("텍스처 (Assets/04_Resources/FX)")]
        [SerializeField] private Texture2D beamTexture;
        [SerializeField] private Texture2D coneTexture;
        [SerializeField] private Texture2D frostTexture;
        [SerializeField] private Texture2D ringTexture;

        [Header("종류 기본색 (UnitData.tintColor 아님)")]
        [SerializeField] private Color suctionColor = new Color(0.15f, 0.95f, 0.95f, 1f);
        [SerializeField] private Color plasmaColor = new Color(1f, 0.55f, 0.12f, 1f);
        [SerializeField] private Color cryoColor = new Color(0.55f, 0.85f, 1f, 1f);

        [Header("연출값 (칸 단위)")]
        [Tooltip("보이는 시간 (초)")]
        [SerializeField, Min(0.01f)] private float duration = 0.1f;
        [Tooltip("흡입 빔 두께")]
        [SerializeField] private float beamWidth = 0.18f;
        [Tooltip("냉각 분사 두께")]
        [SerializeField] private float frostWidth = 0.12f;
        [Tooltip("냉각 끝 Frost_Burst 크기")]
        [SerializeField] private float burstSize = 0.9f;
        [Tooltip("바닥에서 띄우는 높이 (선 · 쿼드)")]
        [SerializeField] private float height = 0.3f;
        [Tooltip("Cone_Gradient 꼭짓점 v (A10: 피벗 (0.5, 0.047))")]
        [SerializeField] private float coneApexV = 0.047f;
        [Tooltip("Ring 반지름 / 텍스처 폭 (A10: 104/256)")]
        [SerializeField] private float ringRadiusFraction = 104f / 256f;

        private MaterialPropertyBlock _lineBlock;
        private MaterialPropertyBlock _quadBlock;
        private float _cellSize = 1f;

        /// <summary>남은 표시 시간. 0 이하면 반납 대상.</summary>
        public float Remaining { get; private set; }
        public float Duration => duration;

        /// <summary>풀에서 처음 꺼낼 때 한 번 (DroneCombat).</summary>
        public void Init(float cellSize)
        {
            if (_lineBlock != null) return;
            _cellSize = cellSize;
            _lineBlock = new MaterialPropertyBlock();
            _quadBlock = new MaterialPropertyBlock();
            if (line != null)
            {
                line.positionCount = 2;
                line.useWorldSpace = true;
            }
        }

        /// <summary>시간 진행. 다 보였으면 true.</summary>
        public bool Tick(float dt)
        {
            Remaining -= dt;
            return Remaining <= 0f;
        }

        public Color ColorOf(DroneType type)
        {
            switch (type)
            {
                case DroneType.Suction: return suctionColor;
                case DroneType.Plasma: return plasmaColor;
                default: return cryoColor;
            }
        }

        /// <summary>흡입 — 드론 → 빔 끝 (가장 먼 맞은 점액).</summary>
        public void ShowBeam(Vector3 from, Vector3 to, DroneType type)
        {
            Begin();
            SetLine(from, to, beamWidth, beamTexture, ColorOf(type));
            HideQuad();
        }

        /// <summary>냉각 — 드론 → 타겟 선 + 끝에 Frost_Burst.</summary>
        public void ShowFrost(Vector3 from, Vector3 to, DroneType type)
        {
            Begin();
            Color c = ColorOf(type);
            SetLine(from, to, frostWidth, frostTexture, c);
            SetFlatQuad(Lifted(to), Vector3.forward, burstSize * _cellSize, burstSize * _cellSize, 0.5f, frostTexture, c);
        }

        /// <summary>소각 — 꼭짓점(드론) → 먼 끝 중심까지 부채꼴, 먼 끝 폭 farWidth (월드).</summary>
        public void ShowCone(Vector3 apex, Vector3 farCenter, float farWidth, DroneType type)
        {
            Begin();
            HideLine();
            apex = Lifted(apex);
            farCenter = Lifted(farCenter);
            Vector3 d = farCenter - apex;
            float length = d.magnitude;
            if (length <= 0f) { HideQuad(); return; }
            float quadHeight = length / (1f - coneApexV);
            SetFlatQuad(apex, d / length, farWidth, quadHeight, coneApexV, coneTexture, ColorOf(type));
        }

        /// <summary>냉각 3등급 빙결 — 중심에 반경 radius (월드) 링.</summary>
        public void ShowRing(Vector3 center, float radius, DroneType type)
        {
            Begin();
            HideLine();
            float size = radius / ringRadiusFraction;
            SetFlatQuad(Lifted(center), Vector3.forward, size, size, 0.5f, ringTexture, ColorOf(type));
        }

        // ── 내부 ──────────────────────────────────────────────

        private void Begin()
        {
            Remaining = duration;
        }

        private Vector3 Lifted(Vector3 p)
        {
            p.y = height * _cellSize;
            return p;
        }

        private void SetLine(Vector3 from, Vector3 to, float width, Texture2D tex, Color color)
        {
            if (line == null) return;
            line.enabled = true;
            line.SetPosition(0, Lifted(from));
            line.SetPosition(1, Lifted(to));
            line.widthMultiplier = width * _cellSize;
            _lineBlock.Clear();
            if (tex != null) _lineBlock.SetTexture(BaseMapId, tex);
            _lineBlock.SetColor(BaseColorId, color);
            line.SetPropertyBlock(_lineBlock);
        }

        /// <summary>바닥에 눕힌 쿼드. 텍스처 +V = forward, pivotV 지점이 anchor 에 온다.</summary>
        private void SetFlatQuad(Vector3 anchor, Vector3 forward, float width, float length, float pivotV,
                                 Texture2D tex, Color color)
        {
            if (quad == null) return;
            Transform t = quad.transform;
            t.rotation = Quaternion.LookRotation(Vector3.down, forward); // 면이 위, 로컬 +Y = forward
            t.position = anchor + forward * ((0.5f - pivotV) * length);
            t.localScale = new Vector3(width, length, 1f);
            _quadBlock.Clear();
            if (tex != null) _quadBlock.SetTexture(BaseMapId, tex);
            _quadBlock.SetColor(BaseColorId, color);
            quad.SetPropertyBlock(_quadBlock);
            quad.enabled = true;
        }

        private void HideLine()
        {
            if (line != null) line.enabled = false;
        }

        private void HideQuad()
        {
            if (quad != null) quad.enabled = false;
        }
    }
}
