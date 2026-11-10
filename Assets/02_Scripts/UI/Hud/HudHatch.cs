using DG.Tweening;
using QL.Core;
using QL.FX;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Flow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QL.UI.Hud
{
    /// <summary>
    /// 위 가운데 — 「격리 해치 무결성」 칸 나뉜 게이지 + 「14 / 20」.
    /// 칸 수 = GameConfig.hatchMaxHp, 시작 때 한 번 만든다 (풀처럼 고정). 칸 색 = FxConfig.hatchGaugeGradient (왼쪽 → 오른쪽).
    /// 시작값은 첫 StateChanged(Prep) 또는 Start 때 Flow.Hatch 에서 (HatchHpChanged 는 피해 때만 — V02), 그 뒤는 HatchHpChanged.
    /// 켜진 칸 = 왼쪽부터 HP 만큼. 꺼지는 칸만 짧게 Fade (칸마다 트윈 하나를 SetAutoKill(false) 로 들고 Restart).
    /// UI_Gauge_Segment 는 사방 3px 투명 여백이 있어 칸을 간격 없이 붙여도 칸 사이가 벌어져 보인다.
    /// </summary>
    public sealed class HudHatch : MonoBehaviour
    {
        public const string HpFormat = "{0}<size=70%> / {1}</size>";

        [SerializeField] private HudContext context;
        [SerializeField] private TMP_Text hpText;
        [Tooltip("칸을 깔 영역 — 칸은 이 너비를 칸 수로 똑같이 나눈다 (간격 0, 그림의 투명 여백이 간격)")]
        [SerializeField] private RectTransform segmentRoot;
        [SerializeField] private Sprite segmentSprite;

        private Image[] _segments;
        private Tween[] _fades;
        private bool[] _lit;
        private int _hp = -1;
        private int _max = -1;
        private bool _hasInitial;

        public TMP_Text HpText => hpText;
        public float FadeDuration => context.Fx.hatchSegmentFadeDuration;
        public float OffAlpha => context.Fx.hatchSegmentOffAlpha;
        public int SegmentCount => _segments != null ? _segments.Length : 0;

        public int LitCount
        {
            get
            {
                int n = 0;
                if (_lit != null) for (int i = 0; i < _lit.Length; i++) if (_lit[i]) n++;
                return n;
            }
        }

        public Image GetSegment(int index) => _segments[index];
        public bool IsLit(int index) => _lit[index];

        private void OnEnable()
        {
            GameEvents.StateChanged += OnStateChanged;
            GameEvents.HatchHpChanged += OnHatchHpChanged;
        }

        private void OnDisable()
        {
            GameEvents.StateChanged -= OnStateChanged;
            GameEvents.HatchHpChanged -= OnHatchHpChanged;
        }

        private void Start()
        {
            EnsureBuilt();
            ShowInitial();
        }

        private void OnDestroy()
        {
            if (_fades == null) return;
            for (int i = 0; i < _fades.Length; i++) _fades[i].Kill();
        }

        private void OnStateChanged(GameState state)
        {
            if (state == GameState.Prep) ShowInitial();
        }

        private void OnHatchHpChanged(int now, int max)
        {
            _hasInitial = true;
            Show(now, max);
        }

        private void ShowInitial()
        {
            if (_hasInitial) return;
            GameLoop loop = context.Loop;
            if (loop == null || !loop.IsReady) return;
            _hasInitial = true;
            Show(loop.Flow.Hatch.Hp, loop.Flow.Hatch.MaxHp);
        }

        private void Show(int hp, int max)
        {
            if (!EnsureBuilt()) return;
            if (hp == _hp && max == _max) return;
            _hp = hp;
            _max = max;
            hpText.SetText(HpFormat, hp, max);

            int n = _segments.Length;
            int lit = max > 0 ? Mathf.Clamp(hp * n / max, 0, n) : 0;
            for (int i = 0; i < n; i++)
            {
                bool on = i < lit;
                if (on == _lit[i]) continue;
                _lit[i] = on;
                if (on) _fades[i].Rewind(); // 처음 알파로 (지금은 회복이 없어 쓰이지 않음)
                else _fades[i].Restart();
            }
        }

        /// <summary>칸을 한 번 만든다 (GameBootstrap 은 Awake 에서 준비 — 실행 순서 -100).</summary>
        private bool EnsureBuilt()
        {
            if (_segments != null) return true;
            GameBootstrap bootstrap = context.Bootstrap;
            if (bootstrap == null || !bootstrap.IsReady) return false;

            FxConfig fx = context.Fx;
            int n = bootstrap.Data.Config.HatchMaxHp;
            _segments = new Image[n];
            _fades = new Tween[n];
            _lit = new bool[n];
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("Segment", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(segmentRoot, false);
                rt.anchorMin = new Vector2((float)i / n, 0f);
                rt.anchorMax = new Vector2((float)(i + 1) / n, 1f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                Image img = go.GetComponent<Image>();
                img.sprite = segmentSprite;
                img.raycastTarget = false;
                img.color = fx.hatchGaugeGradient.Evaluate(n > 1 ? (float)i / (n - 1) : 0f);

                _segments[i] = img;
                _fades[i] = img.DOFade(fx.hatchSegmentOffAlpha, fx.hatchSegmentFadeDuration).SetAutoKill(false).Pause();
                _lit[i] = true;
            }
            return true;
        }
    }
}
