using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace QL.UI.Screens
{
    /// <summary>
    /// 타이틀 (Title.unity) — 시작 → 게임 씬. 해상도 드롭다운 = Screen.resolutions 의 크기(가로 × 세로) 중복 제거 · 큰 순,
    /// 창 모드 토글 = Windowed / FullScreenWindow → Screen.SetResolution. 저장은 Unity 기본(플레이어 설정 기억)에 맡긴다.
    /// </summary>
    public sealed class TitleScreen : MonoBehaviour
    {
        [SerializeField] private string gameSceneName = "Game";
        [SerializeField] private Button startButton;
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private Toggle windowedToggle;
        [Tooltip("드롭다운 항목 문구 — {0} 가로, {1} 세로")]
        [SerializeField] private string resolutionFormat = "{0} × {1}";

        private readonly List<Vector2Int> _sizes = new List<Vector2Int>();

        /// <summary>드롭다운 항목 수 (검증 측정값).</summary>
        public int ResolutionCount => _sizes.Count;

        private void Start()
        {
            BuildResolutions();
            windowedToggle.SetIsOnWithoutNotify(Screen.fullScreenMode == FullScreenMode.Windowed);

            startButton.onClick.AddListener(OnStartClicked);
            resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
            windowedToggle.onValueChanged.AddListener(OnWindowedChanged);
        }

        private void BuildResolutions()
        {
            _sizes.Clear();
            Resolution[] all = Screen.resolutions;
            for (int i = 0; i < all.Length; i++)
            {
                var size = new Vector2Int(all[i].width, all[i].height);
                if (!_sizes.Contains(size)) _sizes.Add(size);
            }

            var current = new Vector2Int(Screen.width, Screen.height);
            if (!_sizes.Contains(current)) _sizes.Add(current);
            _sizes.Sort((a, b) => a.x != b.x ? b.x.CompareTo(a.x) : b.y.CompareTo(a.y));

            var options = new List<TMP_Dropdown.OptionData>(_sizes.Count);
            for (int i = 0; i < _sizes.Count; i++)
                options.Add(new TMP_Dropdown.OptionData(string.Format(resolutionFormat, _sizes[i].x, _sizes[i].y)));

            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(options);
            resolutionDropdown.SetValueWithoutNotify(_sizes.IndexOf(current));
            resolutionDropdown.RefreshShownValue();
        }

        private FullScreenMode SelectedMode => windowedToggle.isOn ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;

        private void OnResolutionChanged(int index)
        {
            if (index < 0 || index >= _sizes.Count) return;
            Screen.SetResolution(_sizes[index].x, _sizes[index].y, SelectedMode);
        }

        private void OnWindowedChanged(bool windowed)
        {
            OnResolutionChanged(resolutionDropdown.value);
        }

        private void OnStartClicked()
        {
            SceneManager.LoadScene(gameSceneName);
        }
    }
}
