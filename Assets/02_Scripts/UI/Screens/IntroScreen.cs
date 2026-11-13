using DG.Tweening;
using QL.FX;
using QL.UI.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace QL.UI.Screens
{
    /// <summary>
    /// U23 인트로 (Intro.unity — 흐름 Title → Main → (처음이면) Intro → Game). 그림 14컷 + 자막을 넘겨 보여 주고 Game 으로.
    /// 그림 = 두 장을 번갈아 크로스페이드 · 컷마다 느린 줌인, 자막 = 한 글자씩 타자 → 다 나오면 잠깐 뒤 자동 넘김.
    /// 클릭 · Space · Enter = 타자 중이면 바로 다 보이기, 다 보였으면 다음 컷. 건너뛰기 버튼 · ESC = 바로 Game.
    /// 끝 (또는 건너뛰기) 에서 PlayerPrefs <see cref="IntroSeenKey"/> = 1 → 다음부터 Main 이 Game 으로 바로 간다 (<see cref="MainScreen.NextSceneName"/>).
    /// 그림 · 자막 = <see cref="IntroCutData"/> SO, 연출 수치 = <see cref="FxConfig"/> 「U23」. 트윈은 시작 때 한 번 (SetAutoKill(false) · Restart), 실제 시간 SetUpdate(true).
    /// </summary>
    public sealed class IntroScreen : MonoBehaviour
    {
        public const string IntroSeenKey = "QL.IntroSeen";
        public const string SceneName = "Intro";

        [SerializeField] private string gameSceneName = "Game";
        [SerializeField] private FxConfig fx;
        [SerializeField] private IntroCutData cuts;
        [SerializeField] private CanvasScaler scaler;

        [Header("그림 — 두 장 번갈아 (각자 AspectRatioFitter EnvelopeParent = 꽉 채움 · 잘림 허용)")]
        [SerializeField] private Image layerA;
        [SerializeField] private Image layerB;

        [Header("자막 · 진행")]
        [SerializeField] private Image captionBand;
        [SerializeField] private TMP_Text caption;
        [SerializeField] private TMP_Text progress;

        [Header("입력 · 덮개")]
        [Tooltip("화면 전체 투명 버튼 — 클릭 = 다음 (건너뛰기 버튼이 위에 있어 그 클릭은 막힘)")]
        [SerializeField] private Button advanceButton;
        [SerializeField] private Button skipButton;
        [SerializeField] private TMP_Text skipLabel;
        [Tooltip("검은 덮개 — 시작 노이즈 · 끝 페이드")]
        [SerializeField] private Image cover;
        [Tooltip("컷 효과음 자리 (U08 전 — 비어 있으면 조용)")]
        [SerializeField] private AudioSource sfxSource;

        private enum Phase { Typing, Shown, Leaving }

        private Phase _phase;
        private int _index = -1;
        private bool _onA;
        private float _timer;
        private int _length;
        private Tween _fadeA;
        private Tween _fadeB;
        private Tween _zoomA;
        private Tween _zoomB;
        private Sequence _noise;
        private Tween _endFade;
        private Tween _skipFade;
        private TweenCallback _loadGame;

        public int Index => _index;
        public int CutCount => cuts.Count;
        public bool IsTyping => _phase == Phase.Typing;
        public bool IsLeaving => _phase == Phase.Leaving;

        /// <summary>다음에 Main 에서 출근하기를 누르면 인트로를 다시 본다.</summary>
        public static void ClearSeen()
        {
            PlayerPrefs.DeleteKey(IntroSeenKey);
            PlayerPrefs.Save();
        }

        private void Awake()
        {
            if (scaler != null) scaler.matchWidthOrHeight = fx.canvasMatchWidthOrHeight; // U10 — 모든 캔버스 같은 기준
            advanceButton.onClick.AddListener(Advance);
            skipButton.onClick.AddListener(Skip);
        }

        private void Start()
        {
            skipLabel.SetText(cuts.SkipLabel);
            Color band = captionBand.color;
            band.a = fx.introCaptionBandAlpha;
            captionBand.color = band;
            caption.fontSize = fx.introCaptionFontSize;
            caption.color = fx.introCaptionColor;
            progress.fontSize = fx.introProgressFontSize;
            progress.gameObject.SetActive(fx.introShowProgress);
            cover.raycastTarget = false;
            BuildTweens();
            if (cuts.Count == 0)
            {
                Finish(_skipFade);
                return;
            }
            ShowCut(0);
            _noise.Restart();
        }

        private void OnDestroy()
        {
            KillTween(_fadeA);
            KillTween(_fadeB);
            KillTween(_zoomA);
            KillTween(_zoomB);
            KillTween(_noise);
            KillTween(_endFade);
            KillTween(_skipFade);
        }

        private static void KillTween(Tween t)
        {
            if (t != null) t.Kill();
        }

        private void BuildTweens()
        {
            _fadeA = BuildFade(layerA);
            _fadeB = BuildFade(layerB);
            _zoomA = BuildZoom(layerA.rectTransform);
            _zoomB = BuildZoom(layerB.rectTransform);

            // 1번 컷 시작 — 검은 덮개가 깜빡이며 걷힘 (화면 노이즈 느낌)
            _noise = DOTween.Sequence().SetUpdate(true).SetAutoKill(false).Pause();
            int flickers = Mathf.Max(1, fx.introNoiseFlickers);
            float step = fx.introNoiseDuration / (flickers * 2f);
            for (int i = 0; i < flickers; i++)
            {
                float low = Mathf.Lerp(fx.introNoiseAlpha, 0f, (i + 1f) / flickers);
                _noise.Append(cover.DOFade(low, step).SetEase(Ease.Linear));
                if (i < flickers - 1) _noise.Append(cover.DOFade(fx.introNoiseAlpha, step).SetEase(Ease.Linear));
                else _noise.Append(cover.DOFade(0f, step).SetEase(Ease.Linear));
            }

            _loadGame = LoadGame;
            _endFade = cover.DOFade(1f, fx.introEndFadeDuration).From(0f).SetEase(Ease.InQuad)
                .SetUpdate(true).SetAutoKill(false).OnComplete(_loadGame).Pause();
            _skipFade = cover.DOFade(1f, fx.introSkipFadeDuration).From(0f).SetEase(Ease.Linear)
                .SetUpdate(true).SetAutoKill(false).OnComplete(_loadGame).Pause();
            SetCoverAlpha(1f); // 위 From(0) 이 덮개를 0 으로 놓으므로 마지막에 — 노이즈는 검은 화면에서 시작
        }

        private Tween BuildFade(Image layer)
        {
            Tween t = layer.DOFade(1f, fx.introCrossfadeDuration).From(0f).SetEase(Ease.Linear)
                .SetUpdate(true).SetAutoKill(false).Pause();
            Color c = layer.color;
            c.a = 0f;
            layer.color = c;
            return t;
        }

        private Tween BuildZoom(RectTransform rt)
        {
            return rt.DOScale(fx.introZoomTo, fx.introZoomDuration).From(Vector3.one * fx.introZoomFrom)
                .SetEase(Ease.OutSine).SetUpdate(true).SetAutoKill(false).Pause();
        }

        private void SetCoverAlpha(float a)
        {
            Color c = cover.color;
            c.a = a;
            cover.color = c;
        }

        private void ShowCut(int index)
        {
            // 앞 컷 크로스페이드가 아직이면 끝내고 넘어감 (빠르게 연타해도 그림이 비지 않게)
            _fadeA.Complete();
            _fadeB.Complete();

            _index = index;
            _onA = !_onA;
            Image front = _onA ? layerA : layerB;
            IntroCutData.Cut cut = cuts[index];
            front.sprite = cut.image;
            front.rectTransform.SetAsLastSibling();
            (_onA ? _fadeA : _fadeB).Restart();
            (_onA ? _zoomA : _zoomB).Restart();

            caption.SetText(cut.caption);
            caption.maxVisibleCharacters = 0;
            caption.ForceMeshUpdate();
            _length = caption.textInfo.characterCount;
            progress.SetText(cuts.ProgressFormat, index + 1, cuts.Count);
            _phase = Phase.Typing;
            _timer = 0f;

            if (sfxSource != null && cut.sfx != null) sfxSource.PlayOneShot(cut.sfx);
        }

        private void Update()
        {
            if (_phase == Phase.Leaving) return;

            // 버튼 클릭이 EventSystem 선택을 남기면 Space · Enter 가 그 버튼을 한 번 더 누른다 — 선택은 이 화면이 직접 관리
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
                EventSystem.current.SetSelectedGameObject(null);

            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.escapeKey.wasPressedThisFrame)
                {
                    Skip();
                    return;
                }
                if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                {
                    Advance();
                    if (_phase == Phase.Leaving) return;
                }
            }

            _timer += Time.unscaledDeltaTime;
            if (_phase == Phase.Typing)
            {
                float dur = fx.introTypeDuration;
                if (dur <= 0f || _timer >= dur) RevealAll();
                else caption.maxVisibleCharacters = Mathf.CeilToInt(_length * (_timer / dur));
            }
            else if (_timer >= fx.introAutoAdvanceDelay)
            {
                Next();
            }
        }

        private void RevealAll()
        {
            caption.maxVisibleCharacters = _length;
            _phase = Phase.Shown;
            _timer = 0f;
        }

        /// <summary>클릭 · Space · Enter — 타자 중이면 바로 다 보이기, 다 보였으면 다음 컷.</summary>
        public void Advance()
        {
            if (_phase == Phase.Leaving) return;
            if (_phase == Phase.Typing) RevealAll();
            else Next();
        }

        /// <summary>다음 컷 — 마지막이면 검게 페이드 → Game.</summary>
        public void Next()
        {
            if (_phase == Phase.Leaving) return;
            if (_index + 1 >= cuts.Count) Finish(_endFade);
            else ShowCut(_index + 1);
        }

        /// <summary>건너뛰기 버튼 · ESC — 바로 Game.</summary>
        public void Skip()
        {
            if (_phase == Phase.Leaving) return;
            Finish(_skipFade);
        }

        private void Finish(Tween fade)
        {
            _phase = Phase.Leaving;
            PlayerPrefs.SetInt(IntroSeenKey, 1);
            PlayerPrefs.Save();
            _noise.Complete();
            cover.raycastTarget = true;
            fade.Restart();
        }

        private void LoadGame()
        {
            SceneManager.LoadScene(gameSceneName);
        }

        [ContextMenu("QL.IntroSeen 지우기 (인트로 다시 보기)")]
        private void ClearSeenFromInspector()
        {
            ClearSeen();
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("QL/인트로 다시 보기 (QL.IntroSeen 지우기)")]
        private static void ClearSeenMenu()
        {
            ClearSeen();
            Debug.Log("[U23] QL.IntroSeen 지움 — 다음 출근하기 때 인트로가 나온다.");
        }
#endif
    }
}
