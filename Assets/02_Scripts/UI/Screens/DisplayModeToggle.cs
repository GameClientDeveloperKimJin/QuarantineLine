using QL.FX;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QL.UI.Screens
{
    /// <summary>
    /// U10 일시정지 카드의 「창 / 전체 화면」 버튼 하나. 누르면 창 ↔ 전체 화면(FullScreenWindow)을 바꾼다.
    /// 전체 화면 = 모니터 해상도, 창 = <see cref="FxConfig.displayWindowedSize"/> (모니터보다 크면 모니터 크기).
    /// 저장은 Unity 기본(플레이어 설정 기억)에 맡긴다 — 타이틀(U01) 해상도 · 창 모드 설정과 같은 방식.
    /// 글자는 「지금 누르면 무엇이 되나」 — Alt+Enter 로 바뀌어도 다음 프레임에 따라간다 (모드가 바뀔 때만 SetText).
    /// 에디터 Play 에서는 Screen 모드가 실제로 바뀌지 않으므로 마지막 요청을 <see cref="RequestedMode"/> 로 기억해 글자와 테스트가 그것을 본다.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class DisplayModeToggle : MonoBehaviour
    {
        [SerializeField] private FxConfig fx;
        [SerializeField] private TMP_Text label;

        private Button _button;
        private FullScreenMode _requested;
        private FullScreenMode _shown = (FullScreenMode)(-1);
        private FullScreenMode _lastScreen;

        /// <summary>지금 기준이 되는 모드 — 마지막 요청, 또는 그 뒤 Alt+Enter 등으로 바뀐 실제 모드.</summary>
        public FullScreenMode RequestedMode => _requested;
        public TMP_Text Label => label;
        public Button Button => _button != null ? _button : _button = GetComponent<Button>();

        /// <summary>창이면 전체 화면(FullScreenWindow), 그 밖(전체 화면 계열)이면 창.</summary>
        public static FullScreenMode Next(FullScreenMode current)
        {
            return current == FullScreenMode.Windowed ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        }

        private void Awake()
        {
            _requested = Screen.fullScreenMode;
            _lastScreen = _requested;
            Button.onClick.AddListener(Toggle);
            RefreshLabel();
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(Toggle);
        }

        private void Update()
        {
            // Alt+Enter (Player 설정 allowFullscreenSwitch) 처럼 밖에서 바뀐 모드를 따라간다
            FullScreenMode now = Screen.fullScreenMode;
            if (now != _lastScreen)
            {
                _lastScreen = now;
                _requested = now;
            }
            RefreshLabel();
        }

        /// <summary>창 ↔ 전체 화면.</summary>
        public void Toggle()
        {
            FullScreenMode next = Next(_requested);
            Resolution display = Screen.currentResolution;
            int w, h;
            if (next == FullScreenMode.Windowed)
            {
                w = Mathf.Min(fx.displayWindowedSize.x, display.width);
                h = Mathf.Min(fx.displayWindowedSize.y, display.height);
            }
            else
            {
                w = display.width;
                h = display.height;
            }
            _requested = next;
            Screen.SetResolution(w, h, next);
            RefreshLabel();
        }

        private void RefreshLabel()
        {
            if (_shown == _requested || label == null) return;
            _shown = _requested;
            label.SetText(_requested == FullScreenMode.Windowed ? fx.displayToFullscreenLabel : fx.displayToWindowLabel);
        }
    }
}
