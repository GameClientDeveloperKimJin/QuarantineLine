using QL.Core;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Flow;
using QL.Game.Stage;
using UnityEngine;

namespace QL.FX.Alert
{
    /// <summary>
    /// 해치 경고등 (AlertFx.prefab). 무대 <c>Posts</c> (A09 M2 경고등 기둥, StageBuilder 가 Start 에 만든다) 의 렌더러에
    /// MaterialPropertyBlock 으로 점멸을 건다 — 재질 · 프리팹 파일은 고치지 않는다.
    /// · WaveStarted: postWaveBlinkDuration 동안 빠르게 점멸 (postWaveColor) → 평소 값 (블록 없음)
    /// · 위기 (HatchHpChanged 로 HP ≤ hatchCriticalHp, 시작값은 Flow.Hatch.IsCritical): 느린 붉은 점멸 유지 · 결과(GameEnded) 때 정리
    /// 블록 = <c>_EmissionColor</c> (색 × 세기 × 점멸값) + <c>_BaseColor</c> (흰색 → 색, 점멸값 × postBaseTint).
    /// M_LinePost 는 발광 칸이 따로 없다 (재질 하나 · _EmissionMap 마스크) 이고 <c>_EMISSION</c> 키워드가 꺼져 있어 _EmissionColor 만으로는 보이지 않는다
    /// (MPB 로는 키워드를 켤 수 없음 — U07). 그래서 기둥 꼭대기 경고등 자리에 가산 빛 쿼드(Mat_FX_GlowDot)를 하나씩 두고 같은 점멸값으로 알파를 준다.
    /// 시간은 게임 시간 (일시정지 중 멈춤). 할당 없음 (렌더러 · 쿼드는 처음 한 번 찾고 만든다).
    /// </summary>
    public sealed class HatchAlertFx : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private FxConfig fx;
        [Tooltip("꼭대기 빛 재질 (가산 Unlit, _BaseColor 틴트 — Mat_FX_GlowDot)")]
        [SerializeField] private Material glowMaterial;
        [Tooltip("꼭대기 빛 메시 (기본 Quad)")]
        [SerializeField] private Mesh glowMesh;
        [Tooltip("StageBuilder 아래 기둥 묶음 이름")]
        [SerializeField] private string postsGroupName = "Posts";

        private StageBuilder _stage;
        private GameLoop _loop;
        private Camera _camera;
        private Renderer[] _posts;
        private Color[] _postBase;
        private MeshRenderer[] _glows;
        private MaterialPropertyBlock _block;
        private int _criticalHp = -1;
        private float _cellSize = 1f;
        private bool _critical;
        private float _waveLeft;
        private float _clock;
        private bool _painted;

        public FxConfig Config => fx;
        /// <summary>찾은 기둥 렌더러 수 (0 이면 Posts 를 아직 못 찾음).</summary>
        public int PostCount => _posts != null ? _posts.Length : 0;
        public Renderer PostAt(int i) => _posts[i];
        public MeshRenderer GlowAt(int i) => _glows[i];
        public bool IsCritical => _critical;
        public bool IsWaveBlinking => _waveLeft > 0f;
        /// <summary>지난 프레임 점멸값 (0~1).</summary>
        public float Level { get; private set; }
        /// <summary>지난 프레임 점멸 색.</summary>
        public Color LevelColor { get; private set; }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            GameEvents.WaveStarted += OnWaveStarted;
            GameEvents.HatchHpChanged += OnHatchHp;
            GameEvents.StateChanged += OnStateChanged;
            GameEvents.GameEnded += OnGameEnded;
        }

        private void OnDisable()
        {
            GameEvents.WaveStarted -= OnWaveStarted;
            GameEvents.HatchHpChanged -= OnHatchHp;
            GameEvents.StateChanged -= OnStateChanged;
            GameEvents.GameEnded -= OnGameEnded;
        }

        private void Start()
        {
            _loop = FindAnyObjectByType<GameLoop>();
            _stage = FindAnyObjectByType<StageBuilder>();
            GameBootstrap b = FindAnyObjectByType<GameBootstrap>();
            if (b != null && b.Data != null && b.Data.Config != null)
            {
                _criticalHp = b.Data.Config.HatchCriticalHp;
                _cellSize = b.Data.Config.CellSize;
            }
            ReadInitial();
        }

        /// <summary>점멸을 끄고 기둥을 원래대로.</summary>
        public void StopAll()
        {
            _waveLeft = 0f;
            _critical = false;
            Restore();
        }

        private void ReadInitial()
        {
            if (_loop != null && _loop.IsReady) _critical = _loop.Flow.Hatch.IsCritical;
        }

        private void OnWaveStarted(int index, int total, string banner)
        {
            _waveLeft = fx.postWaveBlinkDuration;
            _clock = 0f;
        }

        private void OnHatchHp(int now, int max)
        {
            bool critical = now > 0 && now <= _criticalHp; // = HatchModel.IsCritical
            if (critical && !_critical) _clock = 0f;
            _critical = critical;
        }

        private void OnStateChanged(GameState state)
        {
            if (state == GameState.Result) StopAll();
            else if (state == GameState.Prep) ReadInitial();
        }

        private void OnGameEnded(ResultSummary summary) => StopAll();

        private void LateUpdate()
        {
            if (_posts == null && !TryResolve()) return;

            float dt = Time.deltaTime; // 게임 시간
            if (_waveLeft > 0f) _waveLeft -= dt;
            if (_waveLeft <= 0f && !_critical)
            {
                if (_painted) Restore();
                return;
            }
            _clock += dt;

            float level;
            Color color;
            if (_waveLeft > 0f)
            {
                level = Pulse(_clock, fx.postWaveBlinkPeriod);
                color = _critical ? fx.postCriticalColor : fx.postWaveColor;
            }
            else
            {
                level = Pulse(_clock, fx.postCriticalPeriod);
                color = fx.postCriticalColor;
            }
            Paint(level, color);
        }

        /// <summary>0 → 1 → 0 (주기 period, 0 에서 시작해 반 주기에 최고).</summary>
        private static float Pulse(float t, float period) => 0.5f - 0.5f * Mathf.Cos(t * (Mathf.PI * 2f) / period);

        private void Paint(float level, Color color)
        {
            Level = level;
            LevelColor = color;
            _painted = true;
            for (int i = 0; i < _posts.Length; i++)
            {
                _block.Clear();
                _block.SetColor(EmissionColorId, color * (fx.postEmissionIntensity * level));
                Color baseColor = Color.Lerp(_postBase[i], color, fx.postBaseTint * level);
                baseColor.a = _postBase[i].a;
                _block.SetColor(BaseColorId, baseColor);
                _posts[i].SetPropertyBlock(_block);
            }
            for (int i = 0; i < _glows.Length; i++)
            {
                MeshRenderer g = _glows[i];
                Color c = color;
                c.a = fx.postGlowAlpha * level;
                _block.Clear();
                _block.SetColor(BaseColorId, c);
                g.SetPropertyBlock(_block);
                if (!g.enabled) g.enabled = true;
            }
        }

        private void Restore()
        {
            Level = 0f;
            _painted = false;
            if (_posts == null) return;
            for (int i = 0; i < _posts.Length; i++) _posts[i].SetPropertyBlock(null);
            for (int i = 0; i < _glows.Length; i++) _glows[i].enabled = false;
        }

        /// <summary>StageBuilder 가 만든 Posts 아래 기둥마다 렌더러 하나 · 꼭대기 빛 쿼드 하나 (처음 한 번).</summary>
        private bool TryResolve()
        {
            if (_stage == null || !_stage.IsBuilt) return false;
            Transform group = _stage.transform.Find(postsGroupName);
            if (group == null)
            {
                Debug.LogWarning("[HatchAlertFx] 무대에 '" + postsGroupName + "' 가 없다 — 경고등 연출 없음", this);
                _posts = new Renderer[0];
                _postBase = new Color[0];
                _glows = new MeshRenderer[0];
                return true;
            }

            int n = group.childCount;
            _posts = new Renderer[n];
            _postBase = new Color[n];
            _glows = new MeshRenderer[n];
            if (_camera == null) _camera = Camera.main;
            Quaternion facing = _camera != null ? _camera.transform.rotation : Quaternion.identity;
            Vector3 toCamera = _camera != null ? -_camera.transform.forward : Vector3.back;

            for (int i = 0; i < n; i++)
            {
                Transform post = group.GetChild(i);
                Renderer r = post.GetComponentInChildren<Renderer>();
                _posts[i] = r;
                Material m = r != null ? r.sharedMaterial : null;
                _postBase[i] = m != null && m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : Color.white;

                var go = new GameObject("PostGlow_" + i, typeof(MeshFilter), typeof(MeshRenderer));
                Transform t = go.transform;
                t.SetParent(transform, false);
                t.position = post.position + Vector3.up * (fx.postGlowHeight * _cellSize) + toCamera * (fx.postGlowCameraOffset * _cellSize);
                t.rotation = facing; // 고정 쿼터뷰 — 처음 한 번 카메라를 본다
                t.localScale = Vector3.one * (fx.postGlowSize * _cellSize);
                go.GetComponent<MeshFilter>().sharedMesh = glowMesh;
                MeshRenderer mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = glowMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.enabled = false;
                _glows[i] = mr;
            }
            // 기둥 렌더러가 없는 자식은 빼지 않는다 — 렌더러 null 이면 Paint 가 건너뛰도록 아래 정리
            int ok = 0;
            for (int i = 0; i < n; i++) if (_posts[i] != null) ok++;
            if (ok != n)
            {
                var posts = new Renderer[ok];
                var bases = new Color[ok];
                for (int i = 0, j = 0; i < n; i++)
                {
                    if (_posts[i] == null) continue;
                    posts[j] = _posts[i];
                    bases[j++] = _postBase[i];
                }
                _posts = posts;
                _postBase = bases;
            }
            return true;
        }
    }
}
