using DG.Tweening;
using MoreMountains.Feedbacks;
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
    /// U06: 피해 때 패널 흔들림 (DOShakeAnchorPos, 게임 시간) + 깎인 칸은 hatchHitFlashColor 에서 꺼진 칸 색으로 (번쩍 → Fade).
    /// HP 위기 (≤ hatchCriticalHp) 면 패널 위 붉은 테(<see cref="criticalGlow"/>) 알파 Yoyo 점멸. 일시정지 · 결과 때 흔들림 원위치, 결과 때 점멸 끔.
    /// </summary>
    public sealed class HudHatch : MonoBehaviour
    {
        public const string HpFormat = "{0}<size=70%> / {1}</size>";

        [SerializeField] private HudContext context;
        [SerializeField] private TMP_Text hpText;
        [Tooltip("칸을 깔 영역 — 칸은 이 너비를 칸 수로 똑같이 나눈다 (간격 0, 그림의 투명 여백이 간격)")]
        [SerializeField] private RectTransform segmentRoot;
        [SerializeField] private Sprite segmentSprite;
        [Tooltip("U06 위기 점멸 — 패널을 덮는 붉은 테 (raycastTarget 끔, 평소 꺼짐)")]
        [SerializeField] private Image criticalGlow;
        [Tooltip("U18b 피격 Feel (HatchPanel/HitFeel) — 숫자 스프링 펀치 · 색 번쩍. 수치는 그 MMF_Player 인스펙터. 게임 시간")]
        [SerializeField] private MMF_Player hitFeel;

        private Image[] _segments;
        private Tween[] _fades;
        private Color[] _litColor;
        private Tween _shake;
        private Tween _criticalBlink;
        private bool _critical;
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

        /// <summary>U06 — 패널 흔들림 중.</summary>
        public bool IsShaking => _shake != null && _shake.IsActive() && _shake.IsPlaying();
        /// <summary>U06 — 위기 점멸 중.</summary>
        public bool IsCriticalBlinking => _criticalBlink != null && _criticalBlink.IsActive() && _criticalBlink.IsPlaying();
        public Image CriticalGlow => criticalGlow;
        /// <summary>U18b — 피격 Feel (MMF_Player).</summary>
        public MMF_Player HitFeel => hitFeel;
        public bool HasHitFeel => hitFeel != null;
        public int HitFeelPlays { get; private set; }
        public Color HitFlashColor => context.Fx.hatchHitFlashColor;

        public Image GetSegment(int index) => _segments[index];
        public bool IsLit(int index) => _lit[index];

        private void Awake()
        {
            FxConfig fx = context.Fx;
            var rt = (RectTransform)transform;
            _shake = rt.DOShakeAnchorPos(fx.hudShakeDuration, fx.hudShakeStrength, fx.hudShakeVibrato, 90f, false, true)
                .SetAutoKill(false)
                .Pause(); // 게임 시간 — timeScale 0 이면 멈춘다 (U06 4장)
            if (criticalGlow != null)
            {
                criticalGlow.raycastTarget = false;
                Color c = fx.hatchCriticalColor;
                c.a = 0f;
                criticalGlow.color = c;
                criticalGlow.enabled = false;
                _criticalBlink = criticalGlow.DOFade(fx.hatchCriticalAlpha, fx.hatchCriticalPeriod * 0.5f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetAutoKill(false)
                    .Pause();
            }
        }

        private void OnEnable()
        {
            GameEvents.StateChanged += OnStateChanged;
            GameEvents.HatchHpChanged += OnHatchHpChanged;
            GameEvents.GameEnded += OnGameEnded;
        }

        private void OnDisable()
        {
            GameEvents.StateChanged -= OnStateChanged;
            GameEvents.HatchHpChanged -= OnHatchHpChanged;
            GameEvents.GameEnded -= OnGameEnded;
        }

        private void Start()
        {
            EnsureBuilt();
            ShowInitial();
        }

        private void OnDestroy()
        {
            _shake?.Kill();
            _criticalBlink?.Kill();
            if (_fades == null) return;
            for (int i = 0; i < _fades.Length; i++) _fades[i].Kill();
        }

        private void OnStateChanged(GameState state)
        {
            if (state == GameState.Prep) ShowInitial();
            if (state == GameState.Paused || state == GameState.Result) StopShake();
            if (state == GameState.Result) SetCritical(false);
        }

        private void OnGameEnded(ResultSummary summary)
        {
            StopShake();
            SetCritical(false);
        }

        private void OnHatchHpChanged(int now, int max)
        {
            bool damaged = _hp >= 0 && now < _hp;
            _hasInitial = true;
            Show(now, max);
            if (damaged)
            {
                _shake.Restart();
                if (hitFeel != null)
                {
                    hitFeel.PlayFeedbacks();
                    HitFeelPlays++;
                }
            }
            GameBootstrap bootstrap = context.Bootstrap;
            int criticalHp = bootstrap != null && bootstrap.Data != null ? bootstrap.Data.Config.HatchCriticalHp : 0;
            SetCritical(now > 0 && now <= criticalHp); // = HatchModel.IsCritical
        }

        /// <summary>흔들림을 끊고 패널을 원위치 (Rewind = 처음 위치).</summary>
        private void StopShake()
        {
            if (_shake != null && _shake.IsActive()) _shake.Rewind();
        }

        private void SetCritical(bool critical)
        {
            if (critical == _critical || _criticalBlink == null) return;
            _critical = critical;
            if (critical)
            {
                criticalGlow.enabled = true;
                _criticalBlink.Restart();
            }
            else
            {
                _criticalBlink.Rewind(); // 알파 0
                criticalGlow.enabled = false;
            }
        }

        private void ShowInitial()
        {
            if (_hasInitial) return;
            GameLoop loop = context.Loop;
            if (loop == null || !loop.IsReady) return;
            _hasInitial = true;
            Show(loop.Flow.Hatch.Hp, loop.Flow.Hatch.MaxHp);
            SetCritical(loop.Flow.Hatch.IsCritical); // 시작값 (이벤트는 피해 때만)
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
                if (on)
                {
                    _fades[i].Pause(); // 다시 켜진 칸 (지금은 회복이 없어 쓰이지 않음)
                    _segments[i].color = _litColor[i];
                }
                else _fades[i].Restart(); // 빨강 번쩍 → 꺼진 칸 (U06)
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
            _litColor = new Color[n];
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
                Color lit = fx.hatchGaugeGradient.Evaluate(n > 1 ? (float)i / (n - 1) : 0f);
                img.color = lit;

                _segments[i] = img;
                _litColor[i] = lit;
                // 깎인 칸: hatchHitFlashColor(빨강 번쩍) → 칸 색 · 꺼진 알파 (진행값 0→1 하나, 시작 때 한 번 만든 클로저)
                Color flash = fx.hatchHitFlashColor;
                Color off = lit;
                off.a = fx.hatchSegmentOffAlpha;
                _fades[i] = DOVirtual.Float(0f, 1f, fx.hatchSegmentFadeDuration, t => img.color = Color.Lerp(flash, off, t))
                    .SetEase(Ease.OutQuad)
                    .SetAutoKill(false)
                    .Pause();
                _lit[i] = true;
            }
            return true;
        }
    }
}
