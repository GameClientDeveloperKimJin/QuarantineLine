using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QL.UI.Screens
{
    /// <summary>
    /// U20 메인 화면 「설정」 패널 — U01 해상도 드롭다운 (TitleScreen 과 같은 규칙: Screen.resolutions 크기 중복 제거 · 큰 순)
    /// + U10 <see cref="DisplayModeToggle"/> (창 ↔ 전체 화면) + 닫기. 저장은 Unity 기본(플레이어 설정 기억).
    /// </summary>
    public sealed class MainSettingsPanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private DisplayModeToggle displayMode;
        [SerializeField] private Button closeButton;
        [Tooltip("드롭다운 항목 문구 — {0} 가로, {1} 세로")]
        [SerializeField] private string resolutionFormat = "{0} × {1}";

        private readonly List<Vector2Int> _sizes = new List<Vector2Int>();

        public bool IsOpen => root.activeSelf;
        public GameObject Root => root;
        public TMP_Dropdown ResolutionDropdown => resolutionDropdown;
        public DisplayModeToggle DisplayMode => displayMode;
        public Button CloseButton => closeButton;
        public int ResolutionCount => _sizes.Count;

        private void Awake()
        {
            closeButton.onClick.AddListener(Close);
            resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        }

        private void OnDestroy()
        {
            closeButton.onClick.RemoveListener(Close);
            resolutionDropdown.onValueChanged.RemoveListener(OnResolutionChanged);
        }

        public void Open()
        {
            BuildResolutions();
            root.SetActive(true);
        }

        public void Close()
        {
            if (resolutionDropdown.IsExpanded) resolutionDropdown.Hide();
            root.SetActive(false);
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

        private void OnResolutionChanged(int index)
        {
            if (index < 0 || index >= _sizes.Count) return;
            Screen.SetResolution(_sizes[index].x, _sizes[index].y, displayMode.RequestedMode);
        }
    }
}
