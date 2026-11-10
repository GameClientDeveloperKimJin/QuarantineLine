using QL.Core;
using QL.Game;
using UnityEngine;
using UnityEngine.UI;

namespace QL.UI.Screens
{
    /// <summary>
    /// 인게임 화면 — 이번(U01)엔 오른쪽 위 일시정지 버튼만 (HUD 는 U02).
    /// 버튼은 GameLoop.CanAct 일 때만 누를 수 있고(결과 · 일시정지 중 잠금), 누르면 Esc 와 같은 길 GameLoop.HandleEscape.
    /// </summary>
    public sealed class InGameScreen : MonoBehaviour
    {
        [SerializeField] private UIRoot root;
        [SerializeField] private Button pauseButton;

        public Button PauseButton => pauseButton;

        private void Awake()
        {
            pauseButton.onClick.AddListener(OnPauseClicked);
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
            Refresh();
        }

        private void OnStateChanged(GameState state) => Refresh();

        private void Refresh()
        {
            var loop = root.Loop;
            pauseButton.interactable = loop != null && loop.CanAct;
        }

        private void OnPauseClicked()
        {
            var loop = root.Loop;
            if (loop != null && loop.CanAct) loop.HandleEscape();
        }
    }
}
