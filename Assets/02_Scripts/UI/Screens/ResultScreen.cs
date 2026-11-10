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
    /// 결과 화면 — GameEnded(ResultSummary) 를 받아 엠블럼 · 승패 문구 · 남은 해치 HP · 제거한 점액 · 최고 등급 드론 수를 채우고 보인다.
    /// GameEnded 바로 뒤 timeScale 0 이므로 등장 트윈은 ScreenFader(SetUpdate(true)). 채우기는 판 끝 1회 (V01 6장 — SetText).
    /// 다시 하기 = GameLoop.Restart.
    /// </summary>
    public sealed class ResultScreen : MonoBehaviour
    {
        [SerializeField] private UIRoot root;
        [SerializeField] private FxConfig fx;
        [SerializeField] private ScreenFader fader;
        [SerializeField] private Image dim;

        [Header("엠블럼")]
        [SerializeField] private Image emblem;
        [SerializeField] private Sprite victoryEmblem;
        [SerializeField] private Sprite defeatEmblem;

        [Header("문구")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private string victoryTitle = "격리 성공";
        [SerializeField] private string defeatTitle = "격리 실패";
        [SerializeField] private TMP_Text hatchText;
        [SerializeField] private string hatchFormat = "남은 해치 HP {0} / {1}";
        [SerializeField] private TMP_Text killedText;
        [SerializeField] private string killedFormat = "제거한 점액 {0}";
        [SerializeField] private TMP_Text gradeText;
        [SerializeField] private string gradeFormat = "최고 등급 드론 {0}";

        [SerializeField] private Button restartButton;

        public ScreenFader Fader => fader;
        public Image Emblem => emblem;
        public Sprite VictoryEmblem => victoryEmblem;
        public Sprite DefeatEmblem => defeatEmblem;
        public TMP_Text TitleText => titleText;
        public TMP_Text HatchText => hatchText;
        public TMP_Text KilledText => killedText;
        public TMP_Text GradeText => gradeText;
        public string VictoryTitle => victoryTitle;
        public string DefeatTitle => defeatTitle;
        public string HatchFormat => hatchFormat;
        public string KilledFormat => killedFormat;
        public string GradeFormat => gradeFormat;
        public Button RestartButton => restartButton;

        private void Awake()
        {
            dim.color = fx.resultDimColor;
            emblem.rectTransform.sizeDelta = new Vector2(fx.emblemSize, fx.emblemSize);
            restartButton.onClick.AddListener(OnRestartClicked);
        }

        private void OnEnable()
        {
            GameEvents.GameEnded += OnGameEnded;
        }

        private void OnDisable()
        {
            GameEvents.GameEnded -= OnGameEnded;
        }

        private void OnGameEnded(ResultSummary r)
        {
            bool victory = r.Outcome == GameOutcome.Victory;
            emblem.sprite = victory ? victoryEmblem : defeatEmblem;
            titleText.SetText(victory ? victoryTitle : defeatTitle);
            titleText.color = victory ? fx.victoryTitleColor : fx.defeatTitleColor;
            hatchText.SetText(hatchFormat, r.HatchHp, r.HatchMaxHp);
            killedText.SetText(killedFormat, r.SlimesKilled);
            gradeText.SetText(gradeFormat, r.MaxGradeDrones);
            fader.Show();
        }

        private void OnRestartClicked()
        {
            var loop = root.Loop;
            if (loop != null) loop.Restart();
        }
    }
}
