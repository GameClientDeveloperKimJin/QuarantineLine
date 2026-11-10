using QL.Core;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Flow;
using UnityEngine;
using UnityEngine.UI;

namespace QL.FX.Alert
{
    /// <summary>
    /// HP 위기 비네트 (UIRoot InGameScreen/CriticalVignette — HUD 보다 뒤, 화면 전체 RawImage 한 장).
    /// HP ≤ hatchCriticalHp 가 되는 순간 (HatchHpChanged, 시작값은 Flow.Hatch.IsCritical) 켜지고 vignetteAlphaMin ↔ vignetteAlphaMax 로 느리게 점멸.
    /// 가운데는 투명 · 가장자리만 붉게 — 텍스처는 Awake 에 한 번 만든다 (가로세로 비율은 화면에 늘어나 타원이 된다).
    /// 알파 상한 vignetteAlphaMax (시작 0.25) 로 게임 영역을 가리지 않는다. 결과 (GameEnded · Result) 때 끈다. 게임 시간 (일시정지 중 멈춤).
    /// raycastTarget 끔 — 입력을 막지 않는다.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public sealed class CriticalVignetteFx : MonoBehaviour
    {
        private const int TextureSize = 128;

        [SerializeField] private FxConfig fx;

        private RawImage _image;
        private Texture2D _texture;
        private GameLoop _loop;
        private int _criticalHp = -1;
        private bool _critical;
        private float _clock;

        public FxConfig Config => fx;
        public RawImage Image => _image;
        public bool IsCritical => _critical;
        public bool IsVisible => _image != null && _image.enabled;
        public float Alpha => _image != null ? _image.color.a : 0f;

        private void Awake()
        {
            _image = GetComponent<RawImage>();
            _image.raycastTarget = false;
            _texture = BuildTexture(fx.vignetteInner, fx.vignetteFeather);
            _image.texture = _texture;
            _image.enabled = false;
        }

        private void OnEnable()
        {
            GameEvents.HatchHpChanged += OnHatchHp;
            GameEvents.StateChanged += OnStateChanged;
            GameEvents.GameEnded += OnGameEnded;
        }

        private void OnDisable()
        {
            GameEvents.HatchHpChanged -= OnHatchHp;
            GameEvents.StateChanged -= OnStateChanged;
            GameEvents.GameEnded -= OnGameEnded;
        }

        private void Start()
        {
            _loop = FindAnyObjectByType<GameLoop>();
            GameBootstrap b = FindAnyObjectByType<GameBootstrap>();
            if (b != null && b.Data != null && b.Data.Config != null) _criticalHp = b.Data.Config.HatchCriticalHp;
            ReadInitial();
        }

        private void OnDestroy()
        {
            if (_texture != null) Destroy(_texture);
        }

        /// <summary>끈다 (결과 화면).</summary>
        public void Stop()
        {
            _critical = false;
            _image.enabled = false;
        }

        private void ReadInitial()
        {
            if (_loop != null && _loop.IsReady) SetCritical(_loop.Flow.Hatch.IsCritical);
        }

        private void OnHatchHp(int now, int max) => SetCritical(now > 0 && now <= _criticalHp);

        private void OnStateChanged(GameState state)
        {
            if (state == GameState.Result) Stop();
            else if (state == GameState.Prep) ReadInitial();
        }

        private void OnGameEnded(ResultSummary summary) => Stop();

        private void SetCritical(bool critical)
        {
            if (critical && !_critical) _clock = 0f;
            _critical = critical;
            if (!critical) _image.enabled = false;
        }

        private void LateUpdate()
        {
            if (!_critical) return;
            _clock += Time.deltaTime;
            float pulse = 0.5f - 0.5f * Mathf.Cos(_clock * (Mathf.PI * 2f) / fx.vignettePeriod);
            Color c = fx.vignetteColor;
            c.a = Mathf.Lerp(fx.vignetteAlphaMin, fx.vignetteAlphaMax, pulse);
            _image.color = c;
            if (!_image.enabled) _image.enabled = true;
        }

        /// <summary>흰색 · 알파 = 가운데에서 거리 (화면 반 = 1) 가 inner 를 넘으면 feather 폭 동안 0 → 1 (smoothstep).</summary>
        private static Texture2D BuildTexture(float inner, float feather)
        {
            var tex = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = "CriticalVignette",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            var px = new Color32[TextureSize * TextureSize];
            float half = (TextureSize - 1) * 0.5f;
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - inner) / feather));
                    px[y * TextureSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }
    }
}
