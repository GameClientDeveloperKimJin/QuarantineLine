using QL.Game.Bootstrap;
using UnityEngine;

namespace QL.FX.Alert
{
    /// <summary>
    /// U16 레인 위치 핑 (AlertFx.prefab). 관제실 메신저(<c>QL.UI.Hud.OpsMessenger</c>)가 말풍선 팝 순간에 <see cref="Ping"/> 을 부른다 —
    /// 바닥(y = worldFxHeight)에 주황 링 <see cref="FxConfig.opsPingRings"/> 개가 Scale 0.5 → 2 + Fade 1 → 0 으로 퍼지기를
    /// <see cref="FxConfig.opsPingLoops"/> 번 (원본 1초 × 2회). 링마다 <see cref="FxConfig.opsPingRingStagger"/> 씩 늦게 출발.
    /// 링 쿼드는 Awake 에 슬롯 × 링 수만큼 한 번 만든다 (Mat_FX_Ring 가산 · MPB 틴트) — 그 뒤 Instantiate · 할당 없음.
    /// 슬롯이 모자라면 가장 오래된 것을 다시 쓴다. 게임 시간 (일시정지 · 결과 때 같이 멈춤). 위치 계산은 메신저가 GridSpace 로 (읽기만).
    /// </summary>
    public sealed class LanePingFx : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private FxConfig fx;
        [Tooltip("링 재질 (Mat_FX_Ring — 가산 Unlit, _BaseColor 틴트)")]
        [SerializeField] private Material ringMaterial;
        [Tooltip("링 메시 (기본 Quad — X 90° 로 바닥에 눕힌다)")]
        [SerializeField] private Mesh ringMesh;
        [Tooltip("비어 있으면 처음 한 번 찾는다 (cellSize)")]
        [SerializeField] private GameBootstrap bootstrap;

        private Transform[] _slots;
        private Transform[][] _rings;
        private MeshRenderer[][] _renderers;
        private float[] _time;
        private MaterialPropertyBlock _block;
        private int _next;
        private int _playCount;

        /// <summary>Awake 에 만든 링 쿼드 수 (이후 늘지 않는다).</summary>
        public int CreatedCount { get; private set; }
        public int SlotCount => _slots != null ? _slots.Length : 0;
        public int RingCount => fx != null ? Mathf.Max(1, fx.opsPingRings) : 1;
        /// <summary>핑 요청 총수.</summary>
        public int PlayCount => _playCount;
        /// <summary>마지막 핑 위치 (월드, 바닥 높이 포함).</summary>
        public Vector3 LastPosition { get; private set; }
        /// <summary>마지막 핑 슬롯 (없으면 −1).</summary>
        public int LastSlot { get; private set; } = -1;
        /// <summary>한 번 핑 전체 길이 (마지막 링 출발 + 반복).</summary>
        public float Duration => fx != null ? (RingCount - 1) * fx.opsPingRingStagger + Mathf.Max(1, fx.opsPingLoops) * fx.opsPingPeriod : 0f;

        public bool IsActive(int slot) => _slots != null && _slots[slot].gameObject.activeSelf;
        public Vector3 SlotPosition(int slot) => _slots[slot].position;
        public Transform RingAt(int slot, int ring) => _rings[slot][ring];
        public float ElapsedAt(int slot) => _time[slot];

        /// <summary>지금 퍼지는 핑 수.</summary>
        public int ActiveCount
        {
            get
            {
                if (_slots == null) return 0;
                int n = 0;
                for (int i = 0; i < _slots.Length; i++)
                    if (_slots[i].gameObject.activeSelf) n++;
                return n;
            }
        }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (fx == null || ringMaterial == null || ringMesh == null)
            {
                Debug.LogError("[LanePingFx] FxConfig · 링 재질 · 메시가 비어 있다", this);
                enabled = false;
                return;
            }

            int slots = Mathf.Max(1, fx.opsPingSlots);
            int rings = RingCount;
            var holder = new GameObject("LanePings").transform;
            holder.SetParent(transform, false);
            _slots = new Transform[slots];
            _rings = new Transform[slots][];
            _renderers = new MeshRenderer[slots][];
            _time = new float[slots];
            for (int s = 0; s < slots; s++)
            {
                var slot = new GameObject("Ping_" + s.ToString("00")).transform;
                slot.SetParent(holder, false);
                _slots[s] = slot;
                _rings[s] = new Transform[rings];
                _renderers[s] = new MeshRenderer[rings];
                for (int r = 0; r < rings; r++)
                {
                    var go = new GameObject("Ring_" + r);
                    go.layer = gameObject.layer;
                    Transform t = go.transform;
                    t.SetParent(slot, false);
                    t.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    go.AddComponent<MeshFilter>().sharedMesh = ringMesh;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = ringMaterial;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    _rings[s][r] = t;
                    _renderers[s][r] = mr;
                    CreatedCount++;
                }
                slot.gameObject.SetActive(false);
            }
        }

        /// <summary>그 위치 바닥(높이는 worldFxHeight 로 맞춤)에 핑 한 번. 쓴 슬롯 번호 (준비 안 됐으면 −1).</summary>
        public int Ping(Vector3 world)
        {
            if (_slots == null) return -1;
            int s = _next;
            _next = (_next + 1) % _slots.Length;

            world.y = fx.worldFxHeight;
            Transform slot = _slots[s];
            slot.position = world;
            _time[s] = 0f;
            _playCount++;
            LastPosition = world;
            LastSlot = s;
            if (!slot.gameObject.activeSelf) slot.gameObject.SetActive(true);
            Apply(s);
            return s;
        }

        /// <summary>전부 즉시 끄기.</summary>
        public void StopAll()
        {
            if (_slots == null) return;
            for (int s = 0; s < _slots.Length; s++)
                if (_slots[s].gameObject.activeSelf) _slots[s].gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_slots == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            float end = Duration;
            for (int s = 0; s < _slots.Length; s++)
            {
                if (!_slots[s].gameObject.activeSelf) continue;
                _time[s] += dt;
                if (_time[s] >= end)
                {
                    _slots[s].gameObject.SetActive(false);
                    continue;
                }
                Apply(s);
            }
        }

        private void Apply(int s)
        {
            float cell = CellSize();
            float period = fx.opsPingPeriod;
            float life = Mathf.Max(1, fx.opsPingLoops) * period;
            Color baseColor = fx.opsPingColor;
            float k = fx.opsPingIntensity;
            Transform[] rings = _rings[s];
            MeshRenderer[] rs = _renderers[s];
            for (int r = 0; r < rings.Length; r++)
            {
                float lt = _time[s] - r * fx.opsPingRingStagger;
                float scale = 0f;
                float alpha = 0f;
                if (lt >= 0f && lt < life)
                {
                    float phase = (lt % period) / period;
                    scale = Mathf.LerpUnclamped(fx.opsPingScaleFrom, fx.opsPingScaleTo, DG.Tweening.DOVirtual.EasedValue(0f, 1f, phase, fx.opsPingScaleEase));
                    alpha = baseColor.a * (1f - phase);
                }
                float d = scale * fx.opsPingSize * cell;
                rings[r].localScale = new Vector3(d, d, d);
                bool on = alpha > 0f && d > 0f;
                if (rs[r].enabled != on) rs[r].enabled = on;
                if (!on) continue;
                _block.SetColor(BaseColorId, new Color(baseColor.r * k, baseColor.g * k, baseColor.b * k, alpha));
                rs[r].SetPropertyBlock(_block);
            }
        }

        /// <summary>링 하나의 지금 알파 (MPB 에 칠한 값 — 꺼져 있으면 0).</summary>
        public float RingAlpha(int slot, int ring)
        {
            MeshRenderer mr = _renderers[slot][ring];
            if (!mr.enabled) return 0f;
            mr.GetPropertyBlock(_block);
            return _block.GetColor(BaseColorId).a;
        }

        private float CellSize()
        {
            if (bootstrap == null) bootstrap = FindAnyObjectByType<GameBootstrap>();
            return bootstrap != null && bootstrap.IsReady ? bootstrap.Space.CellSize : 1f;
        }
    }
}
