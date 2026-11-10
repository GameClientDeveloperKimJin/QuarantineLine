using QL.Core;
using QL.FX;
using QL.Game;
using UnityEngine;
using UnityEngine.UI;

namespace QL.UI.Screens
{
    /// <summary>
    /// 일시정지 화면 — StateChanged 가 Paused 면 보이고 아니면 숨긴다. 뒤 게임 화면은 <see cref="FxConfig.pauseDimColor"/> 로 어둡게.
    /// 계속하기 = GameLoop.HandleEscape (Esc 와 같은 길, Esc 키는 GameLoop 만 읽는다) · 다시 하기 = GameLoop.Restart.
    /// </summary>
    public sealed class PauseScreen : MonoBehaviour
    {
        [SerializeField] private UIRoot root;
        [SerializeField] private FxConfig fx;
        [SerializeField] private ScreenFader fader;
        [SerializeField] private Image dim;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;

        public ScreenFader Fader => fader;
        public Button ResumeButton => resumeButton;
        public Button RestartButton => restartButton;

        private void Awake()
        {
            dim.color = fx.pauseDimColor;
            resumeButton.onClick.AddListener(OnResumeClicked);
            restartButton.onClick.AddListener(OnRestartClicked);
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

        private void OnStateChanged(GameState state)
        {
            if (state == GameState.Paused) fader.Show();
            else fader.Hide();
        }

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
    }
}
