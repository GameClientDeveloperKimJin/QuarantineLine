using DG.Tweening;
using DG.Tweening.Core;
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
    /// U20 메인 화면 (Main.unity — 흐름 Title → Main → Game). 관제실 배경 (A16 이미지) · CCTV 창 · 로고 · 출근하기 · 메뉴 3줄 · 사원 카드 · 단축키 안내.
    /// 조작: ↑↓ = 출근하기 → 근무 기록 → 설정 → 퇴근하기 (끝에서 반대쪽으로 돈다), ENTER = 실행, ESC = 퇴근 (설정이 열려 있으면 닫기). 마우스 호버 = 선택.
    /// 근무 기록 = 준비 중 토스트만. 문구 · 카드 값 = <see cref="MainScreenText"/> SO, 연출 수치 = <see cref="FxConfig"/> 「U20」.
    /// 연출 트윈은 시작 때 한 번 만들고 (SetAutoKill(false) · Restart) 실제 시간 SetUpdate(true) — Glow 숨쉬기 · 주사선 띠 · REC 깜빡임은 매 프레임 할당 0.
    /// </summary>
    public sealed class MainScreen : MonoBehaviour
    {
        public const int StartIndex = 0;
        public const int RecordsIndex = 1;
        public const int SettingsIndex = 2;
        public const int QuitIndex = 3;

        [SerializeField] private string gameSceneName = "Game";
        [SerializeField] private FxConfig fx;
        [SerializeField] private MainScreenText text;

        [Header("메뉴 (순서 = ↑↓ 순서)")]
        [SerializeField] private MainMenuItem[] items;
        [SerializeField] private RectTransform startButton;
        [SerializeField] private MainSettingsPanel settings;
        [SerializeField] private CanvasGroup toast;
        [SerializeField] private TMP_Text toastLabel;

        [Header("연출")]
        [SerializeField] private CanvasGroup background;
        [SerializeField] private Graphic glow;
        [Tooltip("CCTV 창 안 주사선 띠 (부모 = 창 사각형, 앵커로 위 → 아래)")]
        [SerializeField] private RectTransform scanBand;
        [SerializeField] private float scanBandHeight = 0.08f;
        [Tooltip("REC 빨간 점을 덮는 어두운 점 — 보이면 꺼짐")]
        [SerializeField] private Graphic recCover;
        [Tooltip("들어올 때 순서대로 올라오는 덩어리 (로고 · 버튼 · 메뉴 · 카드) — 각자 CanvasGroup")]
        [SerializeField] private CanvasGroup[] introGroups;
        [SerializeField] private Image exitCover;

        [Header("글자")]
        [SerializeField] private TMP_Text startLabel;
        [SerializeField] private TMP_Text recordsLabel;
        [SerializeField] private TMP_Text settingsLabel;
        [SerializeField] private TMP_Text quitLabel;
        [SerializeField] private TMP_Text quitKeyLabel;
        [SerializeField] private TMP_Text rankLabel;
        [SerializeField] private TMP_Text teamLabel;
        [SerializeField] private TMP_Text idLabel;
        [SerializeField] private TMP_Text bestLabel;
        [SerializeField] private TMP_Text bestGradeLabel;
        [SerializeField] private TMP_Text promoLabel;
        [SerializeField] private TMP_Text promoValueLabel;
        [SerializeField] private Image promoFill;
        [SerializeField] private TMP_Text versionLabel;
        [SerializeField] private TMP_Text hintEnterKey;
        [SerializeField] private TMP_Text hintStart;
        [SerializeField] private TMP_Text hintSelect;
        [SerializeField] private TMP_Text hintEscKey;
        [SerializeField] private TMP_Text hintQuit;
        [SerializeField] private TMP_Text settingsTitleLabel;
        [SerializeField] private TMP_Text settingsResolutionLabel;
        [SerializeField] private TMP_Text settingsCloseLabel;

#if UNITY_EDITOR
        /// <summary>테스트용 — true 면 퇴근하기가 Play 를 끄지 않고 <see cref="QuitRequested"/> 만 켠다.</summary>
        public static bool EditorQuitSuppressed;
#endif

        private int _selected = -1;
        private bool _leaving;
        private Sequence _intro;
        private Tween _glow;
        private Tween _scan;
        private Sequence _rec;
        private Sequence _toast;
        private Sequence _exit;
        private float _scanT;
        private DOGetter<float> _scanGet;
        private DOSetter<float> _scanSet;
        private TweenCallback _recOn;
        private TweenCallback _recOff;
        private TweenCallback _loadNext;
        private Vector2[] _introRest;

        public int Selected => _selected;
        public bool IsLeaving => _leaving;
        public bool QuitRequested { get; private set; }
        public bool IsIntroDone => _intro == null || !_intro.IsActive() || !_intro.IsPlaying();
        public bool IsToastShowing => toast.alpha > 0f;
        public bool IsRecOn => recCover.color.a <= 0f;
        public float ScanProgress => _scanT;
        public MainMenuItem[] Items => items;
        public MainSettingsPanel Settings => settings;
        public MainScreenText Text => text;
        public CanvasGroup Toast => toast;
        public TMP_Text ToastLabel => toastLabel;
        public Graphic Glow => glow;
        public RectTransform StartButton => startButton;

        /// <summary>검증용 — 반복 연출 (Glow · 주사선 · REC) 을 시각 <paramref name="time"/> 로 옮긴다 (할당 측정).</summary>
        public void GotoLoops(float time)
        {
            _glow.Goto(time, true);
            _scan.Goto(time, true);
            _rec.Goto(time, true);
        }

        /// <summary>출근하기 다음 씬 — U23: 처음이면 Intro (QL.IntroSeen), 아니면 Game.</summary>
        public string NextSceneName()
        {
            return PlayerPrefs.GetInt(IntroScreen.IntroSeenKey, 0) == 0 ? IntroScreen.SceneName : gameSceneName;
        }

        private void Start()
        {
            ApplyTexts();
            settings.Root.SetActive(false);
            toast.alpha = 0f;
            exitCover.color = new Color(0f, 0f, 0f, 0f);
            exitCover.raycastTarget = false;
            for (int i = 0; i < items.Length; i++) items[i].SetSelected(false, fx);
            Select(StartIndex);
            BuildTweens();
            _intro.Restart();
            _glow.Restart();
            _scan.Restart();
            _rec.Restart();
        }

        private void OnDestroy()
        {
            KillTween(_intro);
            KillTween(_glow);
            KillTween(_scan);
            KillTween(_rec);
            KillTween(_toast);
            KillTween(_exit);
            if (startButton != null) startButton.DOKill();
        }

        private static void KillTween(Tween t)
        {
            if (t != null) t.Kill();
        }

        private void ApplyTexts()
        {
            startLabel.SetText(text.Start);
            recordsLabel.SetText(text.Records);
            settingsLabel.SetText(text.Settings);
            quitLabel.SetText(text.Quit);
            quitKeyLabel.SetText(text.KeyEsc);
            toastLabel.SetText(text.ComingSoon);
            rankLabel.SetText(text.Rank);
            teamLabel.SetText(text.Team);
            idLabel.SetText(text.EmployeeId);
            bestLabel.SetText(text.BestLabel);
            bestGradeLabel.SetText(text.BestGrade);
            promoLabel.SetText(text.PromoLabel);
            promoValueLabel.SetText("{0} / {1}", text.PromoNow, text.PromoGoal);
            promoFill.fillAmount = text.PromoRatio;
            versionLabel.SetText(string.Format(text.VersionFormat, Application.version));
            hintEnterKey.SetText(text.KeyEnter);
            hintStart.SetText(text.HintStart);
            hintSelect.SetText(text.HintSelect);
            hintEscKey.SetText(text.KeyEsc);
            hintQuit.SetText(text.HintQuit);
            settingsTitleLabel.SetText(text.SettingsTitle);
            settingsResolutionLabel.SetText(text.SettingsResolution);
            settingsCloseLabel.SetText(text.SettingsClose);
        }

        private void BuildTweens()
        {
            // 들어올 때 — 배경 페이드 → 덩어리마다 stagger 씩 늦게 올라옴
            background.alpha = 0f;
            _introRest = new Vector2[introGroups.Length];
            _intro = DOTween.Sequence().SetUpdate(true).SetAutoKill(false).Pause();
            _intro.Append(background.DOFade(1f, fx.mainBgFadeDuration).From(0f));
            float start = fx.mainBgFadeDuration;
            for (int i = 0; i < introGroups.Length; i++)
            {
                var rt = (RectTransform)introGroups[i].transform;
                _introRest[i] = rt.anchoredPosition;
                introGroups[i].alpha = 0f;
                float at = start + fx.mainIntroStagger * i;
                _intro.Insert(at, introGroups[i].DOFade(1f, fx.mainIntroItemDuration).From(0f));
                _intro.Insert(at, rt.DOAnchorPos(_introRest[i], fx.mainIntroItemDuration)
                    .From(_introRest[i] + Vector2.down * fx.mainIntroRise).SetEase(Ease.OutCubic));
            }

            // Glow 숨쉬기 — 알파 min ↔ max, 한 주기 = 두 번 오감
            Color g = glow.color;
            g.a = fx.mainGlowAlphaMin;
            glow.color = g;
            _glow = glow.DOFade(fx.mainGlowAlphaMax, fx.mainGlowPeriod * 0.5f)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetAutoKill(false).Pause();

            // 주사선 띠 — 창 위 → 아래, 앵커로 움직여 해상도가 바뀌어도 창 안
            _scanGet = GetScan;
            _scanSet = SetScan;
            SetScan(0f);
            _scan = DOTween.To(_scanGet, _scanSet, 1f, fx.mainScanBandPeriod)
                .SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart).SetUpdate(true).SetAutoKill(false).Pause();

            // REC 깜빡임 — 반 켜짐 · 반 꺼짐
            _recOn = RecOn;
            _recOff = RecOff;
            float half = fx.mainRecBlinkPeriod * 0.5f;
            _rec = DOTween.Sequence().SetUpdate(true).SetAutoKill(false).SetLoops(-1, LoopType.Restart).Pause();
            _rec.AppendCallback(_recOn).AppendInterval(half).AppendCallback(_recOff).AppendInterval(half);

            // 준비 중 토스트
            _toast = DOTween.Sequence().SetUpdate(true).SetAutoKill(false).Pause();
            _toast.Append(toast.DOFade(1f, fx.mainToastFadeIn).From(0f))
                .AppendInterval(fx.mainToastHold)
                .Append(toast.DOFade(0f, fx.mainToastFadeOut));

            // 출근하기 — 버튼 펀치 → 어두워짐 → 다음 씬
            _loadNext = LoadNext;
            _exit = DOTween.Sequence().SetUpdate(true).SetAutoKill(false).Pause();
            _exit.Append(startButton.DOPunchScale(Vector3.one * fx.mainStartPunch, fx.mainStartPunchDuration, 6, 0.5f))
                .Append(exitCover.DOFade(1f, fx.mainExitFadeDuration).From(0f))
                .AppendCallback(_loadNext);
        }

        private float GetScan()
        {
            return _scanT;
        }

        private void SetScan(float t)
        {
            _scanT = t;
            float top = 1f + scanBandHeight - t * (1f + scanBandHeight);
            scanBand.anchorMin = new Vector2(0f, top - scanBandHeight);
            scanBand.anchorMax = new Vector2(1f, top);
        }

        private void RecOn()
        {
            Color c = recCover.color;
            c.a = 0f;
            recCover.color = c;
        }

        private void RecOff()
        {
            Color c = recCover.color;
            c.a = 1f;
            recCover.color = c;
        }

        private void Update()
        {
            if (_leaving) return;

            // 버튼 클릭이 EventSystem 선택을 남기면 ENTER 가 두 번 눌린다 — 메뉴 선택은 이 화면이 직접 관리
            if (!settings.IsOpen && EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
                EventSystem.current.SetSelectedGameObject(null);

            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame) HandleEscape();
            else if (settings.IsOpen) return;
            else if (kb.upArrowKey.wasPressedThisFrame) Move(-1);
            else if (kb.downArrowKey.wasPressedThisFrame) Move(1);
            else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Activate(_selected);
        }

        /// <summary>↑ = -1 · ↓ = +1. 끝에서 반대쪽으로 돈다. 설정이 열려 있으면 무시.</summary>
        public void Move(int delta)
        {
            if (_leaving || settings.IsOpen) return;
            int n = items.Length;
            Select(((_selected + delta) % n + n) % n);
        }

        public void Select(int index)
        {
            if (_leaving || settings.IsOpen || index < 0 || index >= items.Length || index == _selected) return;
            if (_selected >= 0) items[_selected].SetSelected(false, fx);
            _selected = index;
            items[_selected].SetSelected(true, fx);
        }

        /// <summary>ENTER · 클릭 — 출근하기 · 근무 기록(토스트) · 설정 열기 · 퇴근하기.</summary>
        public void Activate(int index)
        {
            if (_leaving || settings.IsOpen) return;
            switch (index)
            {
                case StartIndex: BeginWork(); break;
                case RecordsIndex: _toast.Restart(); break;
                case SettingsIndex: settings.Open(); break;
                case QuitIndex: Quit(); break;
            }
        }

        /// <summary>ESC — 설정이 열려 있으면 닫기, 아니면 퇴근하기.</summary>
        public void HandleEscape()
        {
            if (_leaving) return;
            if (settings.IsOpen) settings.Close();
            else Quit();
        }

        private void BeginWork()
        {
            _leaving = true;
            exitCover.raycastTarget = true;
            _exit.Restart();
        }

        private void LoadNext()
        {
            SceneManager.LoadScene(NextSceneName());
        }

        private void Quit()
        {
            QuitRequested = true;
#if UNITY_EDITOR
            if (!EditorQuitSuppressed) UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
            SceneManager.LoadScene(0); // B01 — 웹은 종료 불가 → 타이틀로
#else
            Application.Quit();
#endif
        }
    }
}
