using QL.FX.Alert;
using QL.FX.Motion;
using QL.Game;
using QL.Game.Slimes;
using UnityEngine;

namespace QL.FX.Combat
{
    /// <summary>
    /// U24 감속 · 빙결 상태 (CombatFx.prefab). 대상 = 살아 있는 점액 (SlimeSpawned 넣기 · SlimeKilled / SlimeReachedHatch 빼기 — 미리 만든 배열,
    /// swap-remove, 할당 없음 — <see cref="BurnFx"/> 와 같은 방식). LateUpdate 에서 <c>SlimeCombat.SlowRate</c> · <c>Frozen</c> 을 읽어:
    /// <list type="bullet">
    /// <item>감속 · 빙결 중: 발밑 서리 원 (FX_Frost_Ground, 바닥에 눕힘 · 숨쉬듯 깜빡) + 점액 색을 냉각 하늘색 쪽으로 (MPB _BaseColor — 히트 플래시 중엔 쉼)</item>
    /// <item>빙결 중: 얼음 껍질 (FX_Ice_Crust, 빌보드 · 아래 가장자리 = 바닥) 이 덮음</item>
    /// <item>빙결이 풀리는 프레임: 얼음 조각 깨짐 (<see cref="HitBurst"/> Shatter 풀)</item>
    /// </list>
    /// 쿼드는 미리 만든 풀에서 순서대로 옮겨 쓴다 (부모 변경 없음) — 상한 넘는 점액은 이번 프레임 표시 없음. 상한 = FxConfig U24 블록.
    /// 점액 색은 풀 반납 뒤 SlimeView.Setup 이 블록을 다시 칠하므로 재사용 점액에 남지 않는다. 구독만 — 게임 상태를 바꾸지 않는다.
    /// </summary>
    public sealed class FrostFx : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Quaternion FlatRotation = Quaternion.Euler(90f, 0f, 0f);

        [SerializeField] private FxConfig fx;
        [Tooltip("카메라 (빌보드 회전). 비어 있으면 Awake 에 한 번 찾는다")]
        [SerializeField] private FxPool pool;
        [Tooltip("점액 렌더러 · 번쩍 중 여부 (색 칠하기). 비어 있으면 Start 에 한 번 찾는다 — 없으면 색만 안 칠함")]
        [SerializeField] private HitFlashFx hitFlash;
        [Tooltip("FX_Frost_Ground 쿼드 (Mat_FX_FrostGround)")]
        [SerializeField] private Renderer groundPrefab;
        [Tooltip("FX_Ice_Crust 쿼드 (Mat_FX_IceCrust)")]
        [SerializeField] private Renderer crustPrefab;
        [Tooltip("빙결 풀림 얼음 조각 (FX_IceShatter — HitBurst Shatter)")]
        [SerializeField] private HitBurst shatterPrefab;

        private Renderer[] _grounds;
        private Renderer[] _crusts;
        private HitBurst[] _shatters;
        private int _shatterNext;

        private SlimeView[] _targets;
        private bool[] _wasFrozen;
        private Renderer[] _tintR;     // 칠한 렌더러 (null = 안 칠함)
        private bool[] _hadBlock;
        private Color[] _baseColor;
        private int _targetCount;
        private MaterialPropertyBlock _block;

        public int TrackedCount => _targetCount;
        /// <summary>지난 LateUpdate 에 켠 서리 원 · 얼음 껍질 수 · 칠한 점액 수.</summary>
        public int GroundCount { get; private set; }
        public int CrustCount { get; private set; }
        public int TintCount { get; private set; }
        public int ShatterCount { get; private set; }
        public int CreatedCount { get; private set; }

        private void Awake()
        {
            if (fx == null)
            {
                Debug.LogError("[FrostFx] FxConfig 가 비어 있다", this);
                enabled = false;
                return;
            }
            if (pool == null) pool = FindAnyObjectByType<FxPool>();
            _block = new MaterialPropertyBlock();
            _grounds = BuildQuads(groundPrefab, Mathf.Max(1, fx.frostGroundPoolSize));
            _crusts = BuildQuads(crustPrefab, Mathf.Max(1, fx.frostCrustPoolSize));
            if (shatterPrefab != null)
            {
                _shatters = new HitBurst[Mathf.Max(1, fx.frostShatterPoolSize)];
                for (int i = 0; i < _shatters.Length; i++)
                {
                    HitBurst b = Instantiate(shatterPrefab, transform);
                    b.name = shatterPrefab.name + "_" + i.ToString("00");
                    b.Init();
                    _shatters[i] = b;
                    CreatedCount++;
                }
            }

            int cap = Mathf.Max(1, fx.frostTrackCapacity);
            _targets = new SlimeView[cap];
            _wasFrozen = new bool[cap];
            _tintR = new Renderer[cap];
            _hadBlock = new bool[cap];
            _baseColor = new Color[cap];
        }

        private Renderer[] BuildQuads(Renderer prefab, int size)
        {
            if (prefab == null) return new Renderer[0];
            var arr = new Renderer[size];
            for (int i = 0; i < size; i++)
            {
                Renderer r = Instantiate(prefab, transform);
                r.name = prefab.name + "_" + i.ToString("00");
                r.gameObject.SetActive(false);
                arr[i] = r;
                CreatedCount++;
            }
            return arr;
        }

        private void Start()
        {
            if (hitFlash == null) hitFlash = FindAnyObjectByType<HitFlashFx>();
        }

        private void OnEnable()
        {
            GameEvents.SlimeSpawned += OnSpawned;
            GameEvents.SlimeKilled += OnGone;
            GameEvents.SlimeReachedHatch += OnGone;
        }

        private void OnDisable()
        {
            GameEvents.SlimeSpawned -= OnSpawned;
            GameEvents.SlimeKilled -= OnGone;
            GameEvents.SlimeReachedHatch -= OnGone;
        }

        private void OnSpawned(SlimeView view)
        {
            if (view == null || _targets == null || IndexOf(view) >= 0 || _targetCount >= _targets.Length) return;
            int i = _targetCount++;
            _targets[i] = view;
            _wasFrozen[i] = false;
            _tintR[i] = null; // Setup 이 막 블록을 칠했다 — 새로 시작
        }

        private void OnGone(SlimeView view)
        {
            int i = IndexOf(view);
            if (i < 0) return;
            // 색은 되돌리지 않아도 된다 (반납 뒤 Setup 이 다시 칠함) — 히트 플래시 복원과 순서가 엇갈리지 않게 그냥 놓는다
            int last = --_targetCount;
            _targets[i] = _targets[last];
            _wasFrozen[i] = _wasFrozen[last];
            _tintR[i] = _tintR[last];
            _hadBlock[i] = _hadBlock[last];
            _baseColor[i] = _baseColor[last];
            _targets[last] = null;
            _tintR[last] = null;
        }

        private int IndexOf(SlimeView view)
        {
            for (int i = 0; i < _targetCount; i++)
                if (_targets[i] == view) return i;
            return -1;
        }

        private void Update()
        {
            if (_shatters == null) return;
            float dt = Time.deltaTime;
            for (int i = 0; i < _shatters.Length; i++)
                if (_shatters[i].Active) _shatters[i].Tick(dt);
        }

        private void LateUpdate()
        {
            if (_targets == null) return;
            Camera cam = pool != null ? pool.ViewCamera : Camera.main;
            Quaternion rot = cam != null ? cam.transform.rotation : Quaternion.identity;
            Vector3 camPos = cam != null ? cam.transform.position : Vector3.zero;
            float time = Time.time;
            Color cryo = fx.hitCryoColor;

            int usedG = 0, usedC = 0, tinted = 0;
            for (int i = 0; i < _targetCount; i++)
            {
                SlimeView v = _targets[i];
                if (v == null || v.Combat == null || !v.gameObject.activeInHierarchy) continue;
                bool frozen = v.Combat.Frozen;
                bool slowed = v.Combat.SlowRate > 0f;
                Transform t = v.transform;
                Vector3 foot = t.position;
                float scale = t.localScale.y;

                if (_wasFrozen[i] && !frozen) Shatter(foot, scale, rot, cam != null ? (camPos - foot).normalized : Vector3.zero);
                _wasFrozen[i] = frozen;

                if ((slowed || frozen) && usedG < _grounds.Length)
                {
                    Renderer g = _grounds[usedG++];
                    if (!g.gameObject.activeSelf) g.gameObject.SetActive(true);
                    float pulse = fx.frostGroundPulse * Mathf.Sin(time * fx.frostGroundPulseSpeed + v.Id * 1.3f);
                    float s = fx.frostGroundSize * scale * (1f + pulse);
                    Transform gt = g.transform;
                    gt.SetPositionAndRotation(new Vector3(foot.x, foot.y + fx.worldFxHeight, foot.z),
                        Quaternion.AngleAxis(v.Id * 47f, Vector3.up) * FlatRotation);
                    gt.localScale = new Vector3(s, s, s);
                    Paint(g, cryo, fx.frostGroundAlpha * (1f + 2f * pulse));
                }

                if (frozen && usedC < _crusts.Length)
                {
                    Renderer c = _crusts[usedC++];
                    if (!c.gameObject.activeSelf) c.gameObject.SetActive(true);
                    float s = fx.frostCrustSize * scale;
                    Vector3 toCam = cam != null ? (camPos - foot).normalized : Vector3.zero;
                    // 쿼드 가운데 = 발 + 위로 (반 변 − 아래 여백) — 그림 아래 가장자리가 바닥에 닿게. 카메라 쪽으로 당겨 점액 몸 앞에
                    Vector3 up = rot * Vector3.up;
                    Vector3 center = foot + up * (s * (0.5f - fx.frostCrustBottomMargin)) + toCam * (fx.hitImpactCameraOffset * scale);
                    c.transform.SetPositionAndRotation(center, rot);
                    c.transform.localScale = new Vector3(s, s, s);
                    Paint(c, Color.Lerp(Color.white, cryo, 0.35f), fx.frostCrustAlpha);
                }

                if (Tint(i, v, slowed || frozen, frozen ? fx.frostFrozenTint : fx.frostSlowTint, cryo)) tinted++;
            }
            for (int i = usedG; i < _grounds.Length; i++)
                if (_grounds[i].gameObject.activeSelf) _grounds[i].gameObject.SetActive(false);
            for (int i = usedC; i < _crusts.Length; i++)
                if (_crusts[i].gameObject.activeSelf) _crusts[i].gameObject.SetActive(false);
            GroundCount = usedG;
            CrustCount = usedC;
            TintCount = tinted;
        }

        /// <summary>점액 색 — 원하면 원래 색 × (흰 ↔ 냉각색) 을 칠하고, 끝나면 원래 블록으로. 히트 플래시 중엔 손대지 않는다.</summary>
        private bool Tint(int i, SlimeView v, bool want, float mix, Color cryo)
        {
            if (hitFlash == null) return false;
            if (hitFlash.IsFlashing(v)) return want; // 플래시가 끝나면 (원래 = 우리 색으로 복원됨) 다음 프레임에 맞춘다

            Renderer r = _tintR[i];
            if (want)
            {
                if (r == null)
                {
                    r = hitFlash.RendererOf(v);
                    if (r == null) return false;
                    _tintR[i] = r;
                    _hadBlock[i] = r.HasPropertyBlock();
                    Color baseColor = Color.white;
                    Material m = r.sharedMaterial;
                    if (m != null && m.HasProperty(BaseColorId)) baseColor = m.GetColor(BaseColorId);
                    if (_hadBlock[i])
                    {
                        r.GetPropertyBlock(_block);
                        if (_block.HasColor(BaseColorId)) baseColor = _block.GetColor(BaseColorId);
                    }
                    _baseColor[i] = baseColor;
                }
                Color b = _baseColor[i];
                Color c = b * Color.Lerp(Color.white, cryo, mix);
                c.a = b.a;
                _block.Clear();
                _block.SetColor(BaseColorId, c);
                r.SetPropertyBlock(_block);
                return true;
            }

            if (r != null)
            {
                if (_hadBlock[i])
                {
                    _block.Clear();
                    _block.SetColor(BaseColorId, _baseColor[i]);
                    r.SetPropertyBlock(_block);
                }
                else
                {
                    r.SetPropertyBlock(null);
                }
                _tintR[i] = null;
            }
            return false;
        }

        private void Shatter(Vector3 foot, float scale, Quaternion rot, Vector3 toCam)
        {
            if (_shatters == null) return;
            HitBurst b = _shatters[_shatterNext];
            _shatterNext = (_shatterNext + 1) % _shatters.Length;
            b.Play(foot, scale, Vector3.back, rot, toCam, fx.frostShatterCount,
                Color.Lerp(Color.white, fx.hitCryoColor, 0.5f), fx.frostShatterDuration, fx);
            ShatterCount++;
        }

        private void Paint(Renderer r, Color c, float alpha)
        {
            c.a *= Mathf.Clamp01(alpha);
            _block.Clear();
            _block.SetColor(BaseColorId, c);
            r.SetPropertyBlock(_block);
        }
    }
}
