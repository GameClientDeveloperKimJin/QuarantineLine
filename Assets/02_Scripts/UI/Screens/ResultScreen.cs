using DG.Tweening;
using QL.Core.Flow;
using QL.FX;
using QL.Game;
using QL.Game.Flow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QL.UI.Screens
{
    /// <summary>
    /// 결과 화면 (U11 와이어프레임 — <c>Reference/result_wireframe.png</c>). 승리 · 패배가 같은 틀이고 위에서 아래로
    /// ① 헤더(강조색 띠 · 엠블럼 · 문구 · 부제) ② 웨이브 진행 막대 ③ 지표 2칸 ④ 정보 줄 ⑤ 다시 하기 · 타이틀로 ⑥ 키 안내.
    /// 바뀌는 것은 헤더 색 · 문구, 막대 값, 첫 지표 칸, 정보 줄뿐. GameEnded(ResultSummary) 를 받아 판 끝 1회 채운다 (V01 6장 — SetText).
    /// GameEnded 바로 뒤 timeScale 0 이므로 등장은 ScreenFader, 막대 칸 차오름도 SetUpdate(true).
    /// 버튼: 다시 하기 = GameLoop.Restart · 타이틀로 = GameLoop.GoToTitle. 키(R · Esc)는 GameLoop 가 읽는다 — 여기는 키를 읽지 않는다.
    /// </summary>
    public sealed class ResultScreen : MonoBehaviour
    {
        [SerializeField] private UIRoot root;
        [SerializeField] private FxConfig fx;
        [SerializeField] private ScreenFader fader;
        [SerializeField] private Image dim;

        [Header("① 헤더")]
        [SerializeField] private Image headerBand;
        [SerializeField] private Image emblem;
        [SerializeField] private Sprite victoryEmblem;
        [SerializeField] private Sprite defeatEmblem;
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

        private string _victorySubtitle;
        private Image[] _segments = new Image[0];
        private RectTransform[] _fills = new RectTransform[0];
        private Image[] _fillImages = new Image[0];
        private Tween[] _fillTweens = new Tween[0];
        private int _filled;

        public ScreenFader Fader => fader;
        public Image HeaderBand => headerBand;
        public Image Emblem => emblem;
        public Sprite VictoryEmblem => victoryEmblem;
        public Sprite DefeatEmblem => defeatEmblem;
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
            dim.color = fx.resultDimColor;
            emblem.rectTransform.sizeDelta = new Vector2(fx.resultHeaderIconSize, fx.resultHeaderIconSize);
            keyHintText.color = fx.resultHintColor;
            subtitleText.color = fx.resultHintColor;
            _victorySubtitle = stageName + " · " + victorySubtitleFormat; // 판 끝 SetText 에 쓸 형식 — 한 번만 만든다
            restartButton.onClick.AddListener(OnRestartClicked);
            titleButton.onClick.AddListener(OnTitleClicked);
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
            for (int i = 0; i < _fillTweens.Length; i++) _fillTweens[i]?.Kill();
        }

        private void OnGameEnded(ResultSummary r)
        {
            bool victory = r.Outcome == GameOutcome.Victory;
            Color accent = victory ? fx.resultVictoryAccent : fx.resultDefeatAccent;

            // ① 헤더
            headerBand.color = WithAlpha(accent, fx.resultHeaderBandAlpha);
            emblem.sprite = victory ? victoryEmblem : defeatEmblem;
            titleText.SetText(victory ? victoryTitle : defeatTitle);
            titleText.color = victory ? fx.victoryTitleColor : fx.defeatTitleColor;
            if (victory) subtitleText.SetText(_victorySubtitle, r.WaveCount);
            else subtitleText.SetText(defeatSubtitleFormat, r.WaveReached, r.HatchHp);

            // ② 진행 — 승리면 전부, 패배면 도달한 웨이브까지 (와이어프레임: 웨이브 2 패배 = 2칸 빨강)
            progressValueText.SetText(progressFormat, r.WaveReached, r.WaveCount);
            EnsureSegments(r.WaveCount);
            _filled = Mathf.Clamp(victory ? r.WaveCount : r.WaveReached, 0, _segments.Length);

            // ③ 지표 — 첫 칸만 바뀐다
            if (victory)
            {
                primaryValueText.SetText(hatchFormat, r.HatchHp, r.HatchMaxHp);
                primaryLabelText.SetText(hatchLabel);
            }
            else
            {
                primaryValueText.SetText(reachedFormat, r.WaveReached, r.WaveCount);
                primaryLabelText.SetText(reachedLabel);
            }
            primaryOutline.color = accent;
            primaryFill.color = WithAlpha(accent, fx.resultAccentFillAlpha);
            killedText.SetText(killedFormat, r.SlimesKilled);

            // ④ 정보 줄
            if (victory)
            {
                infoLabelText.SetText(bonusLabel);
                infoValueText.SetText(bonusFormat, r.BonusEarned);
            }
            else
            {
                infoLabelText.SetText(laneLabel);
                if (r.MostBreachedLane > 0) infoValueText.SetText(laneFormat, r.MostBreachedLane);
                else infoValueText.SetText(noLaneText);
            }
            infoOutline.color = accent;
            infoFill.color = WithAlpha(accent, fx.resultAccentFillAlpha);

            fader.Show();
            PlaySegmentFill(accent);
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
            for (int i = 0; i < _fillTweens.Length; i++) _fillTweens[i]?.Kill();
            _fillTweens = new Tween[count];
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

        /// <summary>채울 칸이 차례로 차오른다 (칸당 resultSegmentFillInterval, 실제 시간). 나머지는 빈 칸.</summary>
        private void PlaySegmentFill(Color accent)
        {
            for (int i = 0; i < _segments.Length; i++)
            {
                _segments[i].color = fx.resultSegmentEmptyColor;
                _fillImages[i].color = accent;
                _fillTweens[i]?.Kill();
                _fills[i].localScale = new Vector3(0f, 1f, 1f);
                if (i >= _filled) continue;

                _fillTweens[i] = _fills[i].DOScaleX(1f, fx.resultSegmentFillDuration)
                    .SetDelay(fx.resultSegmentFillDelay + fx.resultSegmentFillInterval * i)
                    .SetEase(fx.resultSegmentFillEase)
                    .SetUpdate(true);
            }
        }

        private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

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
