using DG.Tweening;
using QL.Core;
using QL.FX;
using QL.Game;
using QL.Game.Flow;
using QL.UI.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QL.UI.Screens
{
    /// <summary>
    /// 일시정지 = 「휴게 시간」 카드 (U12 · 원본 WO-UI-05 · 시안 <c>Reference/ui_fx_1-5_휴게시간.png</c>).
    /// StateChanged 가 Paused 면 열고 아니면 닫는다 — Esc · HUD 일시정지 버튼 · Alt+Tab(GameLoop.OnApplicationFocus) 세 길 모두 같은 이벤트.
    /// 위에서 아래로 커피잔 + 김 3줄 · 제목 · 상태 줄(근무 시간 · 웨이브 · 해치, 열 때 1회) · 업무 복귀 / 처음부터 다시 / 퇴근하기 · 팁 줄(TipTable, 6초 교체).
    /// 버튼: 업무 복귀 = GameLoop.HandleEscape (Esc 와 같은 길, 키는 GameLoop 만 읽는다) · 처음부터 다시 = Restart · 퇴근하기 = GoToTitle.
    /// 트윈은 전부 처음 열 때 한 번 만들어 SetAutoKill(false) + Restart / Pause 로 재사용, SetUpdate(true) — timeScale 0 에서 움직인다.
    /// 닫힘 연출(0.15초)은 재개를 기다리게 하지 않는다 — 재개는 GameLoop 가 즉시 (S3 규칙 3). 닫힘이 끝나면 루프 트윈을 멈춰 대기 프레임 할당 0.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class PauseScreen : MonoBehaviour
    {
        private const int SteamCount = 3;

        [SerializeField] private UIRoot root;
        [SerializeField] private FxConfig fx;
        [SerializeField] private TipTable tips;

        [Header("어둡게 · 카드")]
        [SerializeField] private Image dim;
        [SerializeField] private RectTransform card;
        [SerializeField] private CanvasGroup cardGroup;

        [Header("커피잔 김 (3줄)")]
        [SerializeField] private Image[] steam = new Image[SteamCount];

        [Header("상태 줄 · 버튼")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button titleButton;
        [SerializeField] private Image[] secondaryButtonImages = new Image[0];
        [SerializeField] private TMP_Text[] secondaryButtonLabels = new TMP_Text[0];

        [Header("팁 줄 (두 글자를 번갈아 크로스페이드)")]
        [SerializeField] private Image tipFrame;
        [SerializeField] private Image tipFill;
        [SerializeField] private TMP_Text tipLabel;
        [SerializeField] private TMP_Text tipTextA;
        [SerializeField] private TMP_Text tipTextB;

        private CanvasGroup _group;
        private bool _shown;
        private bool _built;
        private string _statusFormat;

        // 연출 진행값 — 트윈은 이 float 만 움직이고 Apply 가 그림에 옮긴다 (시작값이 늘 상수라 Restart 가 안전)
        private float _dimOpen;
        private float _cardOpen;
        private float _close = 1f;
        private float _cross;

        private Sequence _open;
        private Tween _closeTween;
        private readonly Tween[] _steamTweens = new Tween[SteamCount];
        private readonly float[] _steamBaseY = new float[SteamCount];
        private Tween _tipTimer;
        private Tween _tipFade;

        private TMP_Text _tipFront;
        private TMP_Text _tipBack;
        private int _tipIndex = -1;
        private int _workSeconds;

        public bool IsShown => _shown;
        public CanvasGroup Group => _group != null ? _group : _group = GetComponent<CanvasGroup>();
        public float DimAlpha => dim.color.a;
        public float CardAlpha => cardGroup.alpha;
        public float CardScale => card.localScale.x;
        public Button ResumeButton => resumeButton;
        public Button RestartButton => restartButton;
        public Button TitleButton => titleButton;
        public TMP_Text StatusText => statusText;
        /// <summary>FxConfig 형식에서 &lt;n&gt; 을 강조색 태그로 바꾼 상태 줄 형식 (테스트 비교용).</summary>
        public string StatusFormat => _statusFormat;
        /// <summary>마지막으로 열 때 읽은 근무 시간 (초, 버림).</summary>
        public int WorkSeconds => _workSeconds;
        /// <summary>지금 앞에 보이는 팁 번호 (TipTable) · 글자.</summary>
        public int TipIndex => _tipIndex;
        public TMP_Text TipFront => _tipFront;
        public TipTable Tips => tips;
        public int SteamLines => steam.Length;
        public Image SteamLine(int i) => steam[i];
        /// <summary>김 루프 · 팁 타이머가 돌고 있는지 (닫힌 뒤 false).</summary>
        public bool LoopsPlaying => _tipTimer != null && _tipTimer.IsPlaying();

        private void Awake()
        {
            string number = "<color=#" + ColorUtility.ToHtmlStringRGBA(fx.breakStatusNumberColor) + ">";
            _statusFormat = fx.breakStatusFormat.Replace("<n>", number).Replace("</n>", "</color>");
            statusText.color = fx.breakStatusLabelColor;

            for (int i = 0; i < secondaryButtonImages.Length; i++) secondaryButtonImages[i].color = fx.breakSecondaryTint;
            for (int i = 0; i < secondaryButtonLabels.Length; i++) secondaryButtonLabels[i].color = fx.breakSecondaryTextColor;
            tipFrame.color = fx.breakTipFrameColor;
            tipFill.color = fx.breakTipFillColor;
            tipLabel.color = fx.breakTipLabelColor;
            tipTextA.color = fx.breakTipTextColor;
            tipTextB.color = fx.breakTipTextColor;
            _tipFront = tipTextA;
            _tipBack = tipTextB;
            for (int i = 0; i < steam.Length; i++)
            {
                steam[i].color = fx.breakSteamColor;
                _steamBaseY[i] = steam[i].rectTransform.anchoredPosition.y;
            }

            resumeButton.onClick.AddListener(OnResumeClicked);
            restartButton.onClick.AddListener(OnRestartClicked);
            titleButton.onClick.AddListener(OnTitleClicked);

            _dimOpen = 0f;
            _cardOpen = 0f;
            Apply();
            ApplyCross(0f);
            SetInteractive(false);
        }

        private void OnEnable()
        {
            GameEvents.StateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            GameEvents.StateChanged -= OnStateChanged;
        }

        private void Start()
        {
            var loop = root.Loop;
            if (loop != null && loop.IsReady) OnStateChanged(loop.Flow.State); // 시작값 (V02)
        }

        private void OnDestroy()
        {
            _open?.Kill();
            _closeTween?.Kill();
            for (int i = 0; i < _steamTweens.Length; i++) _steamTweens[i]?.Kill();
            _tipTimer?.Kill();
            _tipFade?.Kill();
        }

        private void OnStateChanged(GameState state)
        {
            if (state == GameState.Paused) Open();
            else Close();
        }

        // ── 열기 · 닫기 ───────────────────────────────────────

        private void Open()
        {
            if (_shown) return;
            _shown = true;
            EnsureTweens();
            FillStatus();

            SetInteractive(true);
            _closeTween.Pause();
            _close = 1f;
            _open.Restart();

            for (int i = 0; i < _steamTweens.Length; i++)
            {
                _steamTweens[i].Restart();
                _steamTweens[i].Goto(fx.breakSteamStagger * i, true); // 0.3초씩 어긋난 위상에서 바로 이어 돈다
            }

            StartTips();
        }

        private void Close()
        {
            if (!_shown) return;
            _shown = false;
            SetInteractive(false);
            _open.Pause();
            _closeTween.Restart();
        }

        /// <summary>닫힘 연출이 끝나면 루프를 멈추고 원래 자리로 — 닫혀 있는 동안 도는 트윈 0.</summary>
        private void OnClosed()
        {
            for (int i = 0; i < _steamTweens.Length; i++)
            {
                _steamTweens[i].Pause();
                ResetSteam(i);
            }
            _tipTimer.Pause();
            _tipFade.Pause();
        }

        // ── 값 (열 때 1회) ───────────────────────────────────

        private void FillStatus()
        {
            // 근무 시간 = 판 시작부터 게임 시간 (timeScale 0 동안 안 늘어남 · 다시 하기 = 씬 재로드로 0)
            _workSeconds = Mathf.FloorToInt(Time.timeSinceLevelLoad);
            int wave = 1, waveCount = 1, hp = 0, maxHp = 0;
            GameLoop loop = root.Loop;
            if (loop != null && loop.IsReady)
            {
                var flow = loop.Flow;
                wave = flow.WaveIndex + 1;
                waveCount = flow.WaveCount;
                hp = flow.Hatch.Hp;
                maxHp = flow.Hatch.MaxHp;
            }
            statusText.SetText(_statusFormat, _workSeconds / 60, _workSeconds % 60, wave, waveCount, hp, maxHp);
        }

        private void StartTips()
        {
            int count = tips != null ? tips.Count : 0;
            if (count == 0)
            {
                _tipIndex = -1;
                _tipFront.SetText(string.Empty);
                return;
            }

            _tipIndex = Random.Range(0, count); // 처음 열 때마다 무작위 시작
            _tipFront.SetText(tips.Get(_tipIndex));
            _tipFade.Pause();
            ApplyCross(0f);
            if (count > 1) _tipTimer.Restart();
        }

        private void OnTipTimer()
        {
            int count = tips.Count;
            _tipIndex = (_tipIndex + 1) % count;
            _tipBack.SetText(tips.Get(_tipIndex));
            _tipFade.Restart();
        }

        /// <summary>크로스페이드가 끝나면 앞 · 뒤 글자를 바꿔 다음 교체에 대비 (진행값 0 = 새 앞 글자가 보임).</summary>
        private void OnTipFaded()
        {
            TMP_Text t = _tipFront;
            _tipFront = _tipBack;
            _tipBack = t;
            ApplyCross(0f);
        }

        // ── 트윈 (한 번 만든다) ──────────────────────────────

        private void EnsureTweens()
        {
            if (_built) return;
            _built = true;

            _open = DOTween.Sequence()
                .Insert(0f, DOTween.To(() => _dimOpen, v => { _dimOpen = v; Apply(); }, 1f, fx.breakDimDuration).SetEase(Ease.Linear))
                .Insert(fx.breakCardDelay, DOTween.To(() => _cardOpen, v => { _cardOpen = v; Apply(); }, 1f, fx.breakCardDuration).SetEase(fx.breakCardEase))
                .SetUpdate(true)
                .SetAutoKill(false)
                .Pause();

            _closeTween = DOTween.To(() => _close, v => { _close = v; Apply(); }, 0f, fx.breakCloseDuration)
                .From(1f)
                .SetEase(fx.breakCloseEase)
                .SetUpdate(true)
                .SetAutoKill(false)
                .OnComplete(OnClosed)
                .Pause();

            for (int i = 0; i < steam.Length; i++)
            {
                Image img = steam[i];
                ResetSteam(i);
                _steamTweens[i] = DOTween.Sequence()
                    .Append(img.rectTransform.DOLocalMoveY(img.rectTransform.localPosition.y + fx.breakSteamRise, fx.breakSteamDuration).SetEase(Ease.InOutSine))
                    .Join(img.DOFade(0f, fx.breakSteamDuration).SetEase(Ease.InSine))
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(true)
                    .SetAutoKill(false)
                    .Pause();
            }

            _tipTimer = DOVirtual.DelayedCall(fx.breakTipInterval, OnTipTimer, true) // OnStepComplete — 루프마다 불린다
                .SetLoops(-1)
                .SetAutoKill(false)
                .Pause();

            _tipFade = DOTween.To(() => _cross, ApplyCross, 1f, fx.breakTipFadeDuration)
                .From(0f)
                .SetEase(Ease.Linear)
                .SetUpdate(true)
                .SetAutoKill(false)
                .OnComplete(OnTipFaded)
                .Pause();
        }

        private void ResetSteam(int i)
        {
            RectTransform rt = steam[i].rectTransform;
            Vector2 p = rt.anchoredPosition;
            p.y = _steamBaseY[i];
            rt.anchoredPosition = p;
            steam[i].color = fx.breakSteamColor;
        }

        // ── 그림에 옮기기 ────────────────────────────────────

        private void Apply()
        {
            Color c = fx.breakDimColor;
            c.a *= _dimOpen * _close;
            dim.color = c;

            float p = _cardOpen * _close;
            cardGroup.alpha = Mathf.Clamp01(p);
            card.localScale = Vector3.one * Mathf.LerpUnclamped(fx.breakCardScaleFrom, 1f, p);
        }

        private void ApplyCross(float p)
        {
            _cross = p;
            SetAlpha(_tipFront, 1f - p);
            SetAlpha(_tipBack, p);
        }

        private static void SetAlpha(TMP_Text t, float a)
        {
            if (t != null) t.alpha = a;
        }

        private void SetInteractive(bool on)
        {
            Group.interactable = on;
            Group.blocksRaycasts = on;
        }

        // ── 버튼 ─────────────────────────────────────────────

        private void OnResumeClicked()
        {
            var loop = root.Loop;
            if (loop != null && loop.IsReady && loop.Flow.State == GameState.Paused) loop.HandleEscape();
        }

        private void OnRestartClicked()
        {
            var loop = root.Loop;
            if (loop != null) loop.Restart();
        }

        private void OnTitleClicked()
        {
            var loop = root.Loop;
            if (loop != null) loop.GoToTitle();
        }
    }
}
