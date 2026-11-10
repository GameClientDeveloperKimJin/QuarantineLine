using DG.Tweening;
using QL.Core.Flow;
using QL.FX;
using QL.Game;
using QL.Game.Flow;
using QL.UI.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace QL.UI.Screens
{
    /// <summary>
    /// 결과 화면 (U11 와이어프레임 <c>Reference/result_wireframe.png</c> + U13 결재 도장 <c>Reference/ui_fx_1-2_결재도장결과.png</c>).
    /// 승리 · 패배가 같은 틀이고 위에서 아래로 ① 헤더(강조색 띠 · 문구 · 부제) ② 웨이브 진행 막대 ③ 지표 2칸 ④ 정보 줄 ⑤ 다시 하기 · 타이틀로 ⑥ 키 안내,
    /// 그 위 오른쪽 위에 결재 도장(승리 「청소 완료」 · 패배 「재작업 요청」, <see cref="StampData"/>)이 걸친다.
    /// GameEnded(ResultSummary) 를 받아 판 끝 1회 값을 정하고 시퀀스 하나(처음 한 번 만들어 SetAutoKill(false) + Restart, SetUpdate(true) — GameEnded 바로 뒤 timeScale 0)를 튼다:
    /// 0.00 어둡게 → 0.30 패널 부팅(Scale · Fade · 스캔라인 띠) → 0.40 숫자 0 → 값 · 칸 차오름 → 0.90 도장 → 1.10 패널 Punch → 1.20 버튼 잠금 해제 (시점은 FxConfig 「U13」).
    /// 잠금 해제 전 화면 클릭 = 끝 상태로 건너뜀 — 맨 위 클릭 받이(<see cref="SkipCatcher"/>)가 그 클릭을 먹어 버튼은 다음 클릭부터 눌린다.
    /// 버튼: 다시 하기 = GameLoop.Restart · 타이틀로 = GameLoop.GoToTitle. 키(R · Esc)는 GameLoop 가 읽고 잠그지 않는다 — 여기는 키를 읽지 않는다.
    /// 전용 Canvas(override 정렬 30, 휴게 카드와 같음)라 어둡게가 HUD · 멈춘 코인 비행(CoinCanvas 20)까지 덮는다.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ResultScreen : MonoBehaviour, IPointerDownHandler
    {
        [SerializeField] private UIRoot root;
        [SerializeField] private FxConfig fx;
        [SerializeField] private Image dim;
        [SerializeField] private RectTransform panel;
        [SerializeField] private CanvasGroup panelGroup;
        [Tooltip("부팅 때 패널 위 → 아래로 훑는 스캔라인 띠 (UI_FX_Scanline 타일) · 띠 아래 끝 밝은 선")]
        [SerializeField] private RectTransform scanSweep;
        [SerializeField] private Image scanBand;
        [SerializeField] private Image scanEdge;
        [Tooltip("잠금 해제 전 클릭을 먹는 투명 그림 (화면 전체 · 맨 위). 클릭은 이 화면(IPointerDownHandler)으로 올라온다")]
        [SerializeField] private Image skipCatcher;

        [Header("결재 도장")]
        [SerializeField] private Image stamp;
        [SerializeField] private StampData victoryStamp;
        [SerializeField] private StampData defeatStamp;

        [Header("① 헤더")]
        [SerializeField] private Image headerBand;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private string victoryTitle = "격리 성공";
        [SerializeField] private string defeatTitle = "격리 실패";
        [SerializeField] private TMP_Text subtitleText;
        [Tooltip("스테이지 이름 — 승리 부제 앞에 붙는다 (나중에 데이터로)")]
        [SerializeField] private string stageName = "C-3 화물칸";
        [Tooltip("승리 부제 — 「스테이지 이름 · 」 뒤. {0} = 웨이브 수")]
        [SerializeField] private string victorySubtitleFormat = "{0}웨이브 모두 방어";
        [Tooltip("패배 부제 — {0} = 끝난 웨이브(1부터) · {1} = 해치 HP")]
        [SerializeField] private string defeatSubtitleFormat = "웨이브 {0}에서 해치 HP {1}";

        [Header("② 웨이브 진행")]
        [SerializeField] private TMP_Text progressValueText;
        [SerializeField] private string progressFormat = "{0} / {1}";
        [Tooltip("칸들의 부모 (HorizontalLayoutGroup). 첫 자식이 칸 본보기 — 웨이브 수만큼 복제한다")]
        [SerializeField] private RectTransform segmentRoot;
        [Tooltip("칸 안 채움 그림의 자식 이름")]
        [SerializeField] private string segmentFillName = "Fill";

        [Header("③ 지표 2칸")]
        [SerializeField] private TMP_Text primaryValueText;
        [SerializeField] private TMP_Text primaryLabelText;
        [SerializeField] private Image primaryOutline;
        [SerializeField] private Image primaryFill;
        [SerializeField] private string hatchFormat = "{0}/{1}";
        [SerializeField] private string hatchLabel = "남은 해치 HP";
        [SerializeField] private string reachedFormat = "{0}/{1}";
        [SerializeField] private string reachedLabel = "도달한 웨이브";
        [SerializeField] private TMP_Text killedText;
        [SerializeField] private string killedFormat = "{0}";

        [Header("④ 정보 줄")]
        [SerializeField] private Image infoOutline;
        [SerializeField] private Image infoFill;
        [SerializeField] private TMP_Text infoLabelText;
        [SerializeField] private TMP_Text infoValueText;
        [SerializeField] private string bonusLabel = "웨이브 클리어 보너스";
        [SerializeField] private string bonusFormat = "+{0} 코인";
        [SerializeField] private string laneLabel = "많이 뚫린 레인";
        [SerializeField] private string laneFormat = "{0}레인";
        [SerializeField] private string noLaneText = "없음";

        [Header("⑤ 버튼 · ⑥ 키 안내")]
        [SerializeField] private Button restartButton;
        [SerializeField] private Button titleButton;
        [SerializeField] private TMP_Text keyHintText;

        private CanvasGroup _group;
        private bool _shown;
        private bool _unlocked;
        private string _victorySubtitle;
        private Image[] _segments = new Image[0];
        private RectTransform[] _fills = new RectTransform[0];
        private Image[] _fillImages = new Image[0];
        private int _filled;

        // 판 끝 1회 정하는 숫자 목표 — 시퀀스가 0 → 목표로 올린다
        private bool _victory;
        private string _primaryFormat;
        private int _primaryTarget;
        private int _primarySecond;
        private int _progressTarget;
        private int _waveCount;
        private int _killedTarget;
        private int _bonusTarget;
        private int _shownPrimary = -1, _shownProgress = -1, _shownKilled = -1, _shownBonus = -1;

        // 연출 진행값 — 트윈은 이 값만 움직이고 Apply… 가 그림에 옮긴다 (시작값이 늘 상수라 Restart 가 안전)
        private float _dimP;
        private float _panelP;
        private float _scanP;
        private float _countP;
        private float _stampP;
        private Vector3 _punch;
        private Sequence _intro;
        private int _introBuilds;

        public bool IsShown => _shown;
        public CanvasGroup Group => _group != null ? _group : _group = GetComponent<CanvasGroup>();
        /// <summary>패널 알파 (부팅 끝 = 1).</summary>
        public float Alpha => panelGroup.alpha;
        public float DimAlpha => dim.color.a;
        public float PanelScale => panel.localScale.x;
        /// <summary>버튼 잠금이 풀렸는지 (시퀀스 1.2초 또는 건너뜀).</summary>
        public bool Unlocked => _unlocked;
        /// <summary>시퀀스가 아직 도는지.</summary>
        public bool IntroPlaying => _intro != null && _intro.IsPlaying();
        /// <summary>시퀀스를 만든 횟수 (재사용 확인 — 판 끝이 몇 번이어도 1).</summary>
        public int IntroBuilds => _introBuilds;
        public GameObject SkipCatcher => skipCatcher.gameObject;
        public Image Stamp => stamp;
        public float StampAngle => Mathf.DeltaAngle(0f, stamp.rectTransform.localEulerAngles.z);
        public float StampScale => stamp.rectTransform.localScale.x;
        public float StampAlpha => stamp.color.a;
        public StampData VictoryStamp => victoryStamp;
        public StampData DefeatStamp => defeatStamp;
        public Image HeaderBand => headerBand;
        public TMP_Text TitleText => titleText;
        public TMP_Text SubtitleText => subtitleText;
        public TMP_Text ProgressValueText => progressValueText;
        public TMP_Text PrimaryValueText => primaryValueText;
        public TMP_Text PrimaryLabelText => primaryLabelText;
        public TMP_Text KilledText => killedText;
        public TMP_Text InfoLabelText => infoLabelText;
        public TMP_Text InfoValueText => infoValueText;
        public TMP_Text KeyHintText => keyHintText;
        public string VictoryTitle => victoryTitle;
        public string DefeatTitle => defeatTitle;
        public string StageName => stageName;
        public string VictorySubtitleFormat => victorySubtitleFormat;
        public string DefeatSubtitleFormat => defeatSubtitleFormat;
        public string ProgressFormat => progressFormat;
        public string HatchFormat => hatchFormat;
        public string HatchLabel => hatchLabel;
        public string ReachedFormat => reachedFormat;
        public string ReachedLabel => reachedLabel;
        public string KilledFormat => killedFormat;
        public string BonusLabel => bonusLabel;
        public string BonusFormat => bonusFormat;
        public string LaneLabel => laneLabel;
        public string LaneFormat => laneFormat;
        public string NoLaneText => noLaneText;
        public Button RestartButton => restartButton;
        public Button TitleButton => titleButton;

        /// <summary>보이는 진행 막대 칸 수 (= 웨이브 수).</summary>
        public int SegmentCount => _segments.Length;
        /// <summary>채워질 칸 수 (승리 = 웨이브 수, 패배 = 도달 웨이브까지).</summary>
        public int FilledSegments => _filled;

        /// <summary>i 번째 칸 채움 정도 (0 = 빈 칸, 1 = 다 참). 칸 차오름 연출 중이면 그 사이 값.</summary>
        public float SegmentFill(int i) => _fills[i].localScale.x;

        /// <summary>i 번째 칸 채움 색.</summary>
        public Color SegmentColor(int i) => _fillImages[i].color;

        private void Awake()
        {
            keyHintText.color = fx.resultHintColor;
            subtitleText.color = fx.resultHintColor;
            scanSweep.sizeDelta = new Vector2(scanSweep.sizeDelta.x, fx.resultScanHeight);
            skipCatcher.color = Color.clear;
            _victorySubtitle = stageName + " · " + victorySubtitleFormat; // 판 끝 SetText 에 쓸 형식 — 한 번만 만든다
            restartButton.onClick.AddListener(OnRestartClicked);
            titleButton.onClick.AddListener(OnTitleClicked);

            ResetVisuals();
            SetButtons(false);
            SetShown(false);
        }

        private void OnEnable()
        {
            GameEvents.GameEnded += OnGameEnded;
        }

        private void OnDisable()
        {
            GameEvents.GameEnded -= OnGameEnded;
        }

        private void OnDestroy()
        {
            _intro?.Kill();
        }

        // ── 판 끝 ────────────────────────────────────────────

        private void OnGameEnded(ResultSummary r)
        {
            bool victory = r.Outcome == GameOutcome.Victory;
            _victory = victory;
            Color accent = victory ? fx.resultVictoryAccent : fx.resultDefeatAccent;

            // ① 헤더
            headerBand.color = WithAlpha(accent, fx.resultHeaderBandAlpha);
            titleText.SetText(victory ? victoryTitle : defeatTitle);
            titleText.color = victory ? fx.victoryTitleColor : fx.defeatTitleColor;
            if (victory) subtitleText.SetText(_victorySubtitle, r.WaveCount);
            else subtitleText.SetText(defeatSubtitleFormat, r.WaveReached, r.HatchHp);

            // ② 진행 — 승리면 전부, 패배면 도달한 웨이브까지 (와이어프레임: 웨이브 2 패배 = 2칸 빨강)
            _progressTarget = r.WaveReached;
            _waveCount = r.WaveCount;
            EnsureSegments(r.WaveCount);
            _filled = Mathf.Clamp(victory ? r.WaveCount : r.WaveReached, 0, _segments.Length);
            for (int i = 0; i < _segments.Length; i++)
            {
                _segments[i].color = fx.resultSegmentEmptyColor;
                _fillImages[i].color = accent;
            }

            // ③ 지표 — 첫 칸만 바뀐다. 앞 숫자가 0 → 값으로 올라간다
            if (victory)
            {
                _primaryFormat = hatchFormat;
                _primaryTarget = r.HatchHp;
                _primarySecond = r.HatchMaxHp;
                primaryLabelText.SetText(hatchLabel);
            }
            else
            {
                _primaryFormat = reachedFormat;
                _primaryTarget = r.WaveReached;
                _primarySecond = r.WaveCount;
                primaryLabelText.SetText(reachedLabel);
            }
            primaryOutline.color = accent;
            primaryFill.color = WithAlpha(accent, fx.resultAccentFillAlpha);
            _killedTarget = r.SlimesKilled;

            // ④ 정보 줄 — 보너스는 올라가고, 레인 번호는 이름이라 그대로
            _bonusTarget = r.BonusEarned;
            if (victory)
            {
                infoLabelText.SetText(bonusLabel);
            }
            else
            {
                infoLabelText.SetText(laneLabel);
                if (r.MostBreachedLane > 0) infoValueText.SetText(laneFormat, r.MostBreachedLane);
                else infoValueText.SetText(noLaneText);
            }
            infoOutline.color = accent;
            infoFill.color = WithAlpha(accent, fx.resultAccentFillAlpha);

            // 결재 도장 — 그림 · 크기 · 무작위 기울기
            StampData data = victory ? victoryStamp : defeatStamp;
            stamp.sprite = data.Sprite;
            RectTransform srt = stamp.rectTransform;
            srt.sizeDelta = data.Size;
            srt.localEulerAngles = new Vector3(0f, 0f, Random.Range(data.AngleMin, data.AngleMax));

            _shownPrimary = _shownProgress = _shownKilled = _shownBonus = -1;
            SetShown(true);
            SetButtons(false);
            EnsureIntro();
            ResetVisuals();
            _intro.Restart();
        }

        /// <summary>칸 수를 웨이브 수에 맞춘다 — 본보기(첫 자식)를 판 끝 1회 복제. 남는 칸은 끈다.</summary>
        private void EnsureSegments(int count)
        {
            if (count < 1) count = 1;
            if (_segments.Length == count) return;

            Transform template = segmentRoot.GetChild(0);
            while (segmentRoot.childCount < count) Instantiate(template, segmentRoot, false);

            _segments = new Image[count];
            _fills = new RectTransform[count];
            _fillImages = new Image[count];
            for (int i = 0; i < segmentRoot.childCount; i++)
            {
                Transform seg = segmentRoot.GetChild(i);
                bool on = i < count;
                seg.gameObject.SetActive(on);
                if (!on) continue;
                _segments[i] = seg.GetComponent<Image>();
                _fills[i] = (RectTransform)seg.Find(segmentFillName);
                _fillImages[i] = _fills[i].GetComponent<Image>();
            }
        }

        // ── 시퀀스 (한 번 만든다) ────────────────────────────

        private void EnsureIntro()
        {
            if (_intro != null) return;
            _introBuilds++;

            _intro = DOTween.Sequence()
                .Insert(0f, DOTween.To(() => _dimP, ApplyDim, 1f, fx.resultIntroDimDuration).From(0f).SetEase(Ease.Linear))
                .Insert(fx.resultIntroPanelAt, DOTween.To(() => _panelP, ApplyPanel, 1f, fx.resultIntroPanelDuration).From(0f).SetEase(fx.resultIntroPanelEase))
                .Insert(fx.resultIntroPanelAt, DOTween.To(() => _scanP, ApplyScan, 1f, fx.resultScanDuration).From(0f).SetEase(Ease.Linear))
                .Insert(fx.resultIntroCountAt, DOTween.To(() => _countP, ApplyCount, 1f, fx.resultIntroCountDuration).From(0f).SetEase(Ease.Linear))
                .Insert(fx.resultIntroStampAt, DOTween.To(() => _stampP, ApplyStamp, 1f, fx.resultIntroStampDuration).From(0f).SetEase(Ease.Linear))
                .Insert(fx.resultIntroPunchAt, DOTween.Punch(() => _punch, ApplyPunch, Vector3.one * fx.resultIntroPunch,
                    fx.resultIntroPunchDuration, fx.resultIntroPunchVibrato, fx.resultIntroPunchElasticity))
                .InsertCallback(fx.resultIntroUnlockAt, Unlock)
                .SetUpdate(true)
                .SetAutoKill(false)
                .Pause();
        }

        /// <summary>잠금 해제 전 클릭 → 끝 상태로 (숫자 = 최종값 · 도장 크기 1 · 버튼 풀림). 이 클릭은 이 화면이 먹어 버튼에 가지 않는다.</summary>
        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_shown || _unlocked || _intro == null) return;
            _intro.Complete(true);
            Unlock();
        }

        /// <summary>검증 · 캡처용 — 시퀀스를 seconds 시점으로 옮겨 멈춘다 (트윈이 매 프레임 부르는 같은 setter 경로). 판 끝 전이면 아무것도 안 한다.</summary>
        public void SeekIntro(float seconds)
        {
            if (_intro == null) return;
            _intro.Goto(seconds, false);
        }

        private void Unlock()
        {
            if (_unlocked) return;
            SetButtons(true);
        }

        // ── 그림에 옮기기 ────────────────────────────────────

        private void ResetVisuals()
        {
            ApplyDim(0f);
            ApplyPanel(0f);
            ApplyScan(0f);
            ApplyCount(0f);
            ApplyStamp(0f);
            ApplyPunch(Vector3.zero);
        }

        private void ApplyDim(float p)
        {
            _dimP = p;
            Color c = fx.resultDimColor;
            c.a = fx.resultIntroDimAlpha * p;
            dim.color = c;
        }

        private void ApplyPanel(float p)
        {
            _panelP = p;
            panelGroup.alpha = Mathf.Clamp01(p);
            ApplyPanelScale();
        }

        private void ApplyPunch(Vector3 v)
        {
            _punch = v;
            ApplyPanelScale();
        }

        private void ApplyPanelScale()
        {
            float s = Mathf.LerpUnclamped(fx.resultIntroPanelScaleFrom, 1f, _panelP);
            panel.localScale = new Vector3(s + _punch.x, s + _punch.y, 1f);
        }

        /// <summary>
        /// 스캔라인 띠가 패널 위 끝 → 아래 끝으로 — 패널(페이드 중) 바깥 형제라 흐려지지 않고 또렷이 훑는다.
        /// 띠는 패널과 같은 가운데 앵커 · 위 피벗. 알파는 처음 · 끝 1/4 에서 오르내린다 (끝나면 0).
        /// </summary>
        private void ApplyScan(float p)
        {
            _scanP = p;
            float h = panel.rect.height;
            float travel = h - fx.resultScanHeight;
            Vector2 pos = scanSweep.anchoredPosition;
            pos.y = panel.anchoredPosition.y + h * 0.5f - travel * p;
            scanSweep.anchoredPosition = pos;

            float a = Mathf.Clamp01(p * 4f) * Mathf.Clamp01((1f - p) * 4f);
            Color band = fx.resultScanColor;
            band.a *= a;
            scanBand.color = band;
            Color edge = fx.resultScanEdgeColor;
            edge.a *= a;
            scanEdge.color = edge;
        }

        /// <summary>숫자 0 → 값 (값이 바뀔 때만 SetText) · 진행 막대 칸 차례로 차오름 (칸 간격 · 시간 = U11).</summary>
        private void ApplyCount(float p)
        {
            _countP = p;
            float v = p >= 1f ? 1f : DOVirtual.EasedValue(0f, 1f, p, fx.resultIntroCountEase);

            int primary = Mathf.RoundToInt(_primaryTarget * v);
            if (primary != _shownPrimary)
            {
                _shownPrimary = primary;
                primaryValueText.SetText(_primaryFormat ?? hatchFormat, primary, _primarySecond);
            }

            int progress = Mathf.RoundToInt(_progressTarget * v);
            if (progress != _shownProgress)
            {
                _shownProgress = progress;
                progressValueText.SetText(progressFormat, progress, _waveCount);
            }

            int killed = Mathf.RoundToInt(_killedTarget * v);
            if (killed != _shownKilled)
            {
                _shownKilled = killed;
                killedText.SetText(killedFormat, killed);
            }

            if (_victory)
            {
                int bonus = Mathf.RoundToInt(_bonusTarget * v);
                if (bonus != _shownBonus)
                {
                    _shownBonus = bonus;
                    infoValueText.SetText(bonusFormat, bonus);
                }
            }

            float t = p * fx.resultIntroCountDuration - fx.resultSegmentFillDelay;
            float dur = Mathf.Max(fx.resultSegmentFillDuration, 0.0001f);
            for (int i = 0; i < _fills.Length; i++)
            {
                float s = 0f;
                if (i < _filled)
                {
                    s = p >= 1f ? 1f : Mathf.Clamp01((t - fx.resultSegmentFillInterval * i) / dur);
                    if (s < 1f) s = DOVirtual.EasedValue(0f, 1f, s, fx.resultSegmentFillEase);
                }
                _fills[i].localScale = new Vector3(s, 1f, 1f);
            }
        }

        /// <summary>도장 내려찍기 — 크기 1.8 → 1 (InBack: 살짝 더 커졌다가 쾅) · 알파 0 → 1.</summary>
        private void ApplyStamp(float p)
        {
            _stampP = p;
            float e = p >= 1f ? 1f : DOVirtual.EasedValue(0f, 1f, p, fx.resultIntroStampEase);
            float s = Mathf.LerpUnclamped(fx.resultIntroStampScaleFrom, 1f, e);
            stamp.rectTransform.localScale = new Vector3(s, s, 1f);
            Color c = stamp.color;
            c.a = Mathf.Clamp01(p);
            stamp.color = c;
        }

        private void SetShown(bool on)
        {
            _shown = on;
            Group.alpha = on ? 1f : 0f;
            Group.interactable = on;
            Group.blocksRaycasts = on;
        }

        private void SetButtons(bool on)
        {
            _unlocked = on;
            restartButton.interactable = on;
            titleButton.interactable = on;
            skipCatcher.raycastTarget = !on;
        }

        private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        // ── 버튼 ─────────────────────────────────────────────

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
