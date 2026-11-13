using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using QL.Game;
using QL.UI.Data;
using QL.UI.Screens;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace QL.Tests.PlayMode.UI
{
    /// <summary>
    /// U20 메인 화면 (Main.unity) — 흐름 Title(시작) → Main(출근하기) → Game. 메뉴 4개 · 카드 문구 = SO, ↑↓ · 호버 선택, 근무 기록 = 토스트만,
    /// 설정 열기 · ESC 닫기, ESC = 퇴근 (테스트에선 Play 를 끄지 않게 <see cref="MainScreen.EditorQuitSuppressed"/>), 출근하기 → Game,
    /// 1920×1080 · 16:10 · 21:9 · 1280×720 에서 화면 밖 0 · 겹침 0 · 글자 24 이상, 반복 연출 (Glow · 주사선 · REC) 할당 0.
    /// </summary>
    public sealed class MainScreenTests
    {
        private const string MainSceneName = "Main";
        private const string TitleSceneName = "Title";
        private const string GameSceneName = "Game";
        private const int MaxWaitFrames = 600;
        private const float SafeEdge = 24f;   // 캔버스 단위 (1920×1080 기준 px)
        private const float MinFontPx = 24f;  // S3 규칙 8

        // 서로 겹치면 안 되는 덩어리 (MainScreen 아래 경로)
        private static readonly string[] Blocks =
        {
            "Logo", "StartGroup/StartButton", "Menu/Records", "Menu/Settings", "Menu/Quit", "Card", "Version", "Hints", "Toast",
        };

        private MainScreen _screen;
        private GuidePrefsGuard _guidePrefs;
        private bool _hadIntroSeen;   // U23 — QL.IntroSeen 원래 있었나
        private int _introSeenValue;  // U23 — 원래 값

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _guidePrefs = GuidePrefsGuard.Save(); // F03 — QL.GuideDone 원복
            _hadIntroSeen = PlayerPrefs.HasKey(IntroScreen.IntroSeenKey); // U23 — 인트로 본 상태로 고정 (출근하기 = Game)
            _introSeenValue = PlayerPrefs.GetInt(IntroScreen.IntroSeenKey, 0);
            PlayerPrefs.SetInt(IntroScreen.IntroSeenKey, 1);
#if UNITY_EDITOR
            MainScreen.EditorQuitSuppressed = true;
#endif
            GameEvents.ClearAll();
            yield return LoadMain();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _guidePrefs.Restore();
            if (_hadIntroSeen) PlayerPrefs.SetInt(IntroScreen.IntroSeenKey, _introSeenValue); // U23 — 원래 값으로
            else PlayerPrefs.DeleteKey(IntroScreen.IntroSeenKey);
            PlayerPrefs.Save();
#if UNITY_EDITOR
            MainScreen.EditorQuitSuppressed = false;
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1920, 1080, "U20 MainScreenTests");
#endif
            Time.timeScale = 1f;
            GameEvents.ClearAll();
            yield return null;
        }

        // ── 도우미 ───────────────────────────────────────────

        private IEnumerator LoadMain()
        {
            SceneManager.LoadScene(MainSceneName, LoadSceneMode.Single);
            _screen = null;
            for (int i = 0; i < MaxWaitFrames; i++)
            {
                yield return null;
                _screen = Object.FindAnyObjectByType<MainScreen>();
                if (_screen != null && _screen.Selected >= 0 && _screen.IsIntroDone) break;
            }
            Assert.IsNotNull(_screen, "Main 씬에 MainScreen 이 없다");
            Assert.IsTrue(_screen.IsIntroDone, "들어오기 연출이 끝나지 않았다");
        }

        private static IEnumerator WaitForScene(string name)
        {
            for (int i = 0; i < MaxWaitFrames && SceneManager.GetActiveScene().name != name; i++) yield return null;
            Assert.AreEqual(name, SceneManager.GetActiveScene().name, name + " 씬으로 가지 않았다");
        }

        private static IEnumerator WaitReal(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        private TMP_Text FindText(string path)
        {
            Transform t = _screen.transform.Find(path);
            Assert.IsNotNull(t, path + " 이 없다");
            return t.GetComponent<TMP_Text>();
        }

        private static Rect ScreenRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        private static IEnumerator SetResolution(int w, int h)
        {
#if UNITY_EDITOR
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution((uint)w, (uint)h, "U20 MainScreenTests");
#endif
            for (int i = 0; i < MaxWaitFrames && (Screen.width != w || Screen.height != h); i++) yield return null;
            Assert.AreEqual(new Vector2Int(w, h), new Vector2Int(Screen.width, Screen.height), "Game View 해상도가 바뀌지 않았다");
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        // ── 테스트 ───────────────────────────────────────────

        [UnityTest]
        public IEnumerator Load_FourMenusAndCardTextsFromSO()
        {
            MainScreenText text = _screen.Text;
            Assert.IsNotNull(text, "MainScreenText SO 가 연결되지 않았다");
            Assert.AreEqual(4, _screen.Items.Length, "메뉴 4개 (출근하기 · 근무 기록 · 설정 · 퇴근하기)");
            Assert.AreEqual(MainScreen.StartIndex, _screen.Selected, "처음 선택 = 출근하기");
            Assert.IsFalse(_screen.Items[MainScreen.RecordsIndex].Available, "근무 기록 = 비활성 (준비 중)");

            Assert.AreEqual(text.Start, FindText("StartGroup/StartButton/Content/Label").text);
            Assert.AreEqual(text.Records, _screen.Items[1].Label.text);
            Assert.AreEqual(text.Settings, _screen.Items[2].Label.text);
            Assert.AreEqual(text.Quit, _screen.Items[3].Label.text);
            Assert.AreEqual(text.Rank, FindText("Card/Badge/Rank").text);
            Assert.AreEqual(text.Team, FindText("Card/Team").text);
            Assert.AreEqual(text.EmployeeId, FindText("Card/Id").text);
            Assert.AreEqual(text.BestLabel, FindText("Card/BestLabel").text);
            Assert.AreEqual(text.BestGrade, FindText("Card/BestGrade").text);
            Assert.AreEqual(text.PromoLabel, FindText("Card/PromoLabel").text);
            Assert.AreEqual(text.PromoNow + " / " + text.PromoGoal, FindText("Card/PromoValue").GetParsedText());
            Assert.AreEqual(text.PromoRatio, _screen.transform.Find("Card/PromoBar/Fill").GetComponent<Image>().fillAmount, 1e-4f, "진행 바");
            Assert.AreEqual(string.Format(text.VersionFormat, Application.version), FindText("Version").text);
            Assert.AreEqual(GameSceneName, _screen.NextSceneName(), "M01 전까지 출근하기 = Game");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ArrowsAndHover_MoveSelection()
        {
            _screen.Move(1);
            Assert.AreEqual(MainScreen.RecordsIndex, _screen.Selected, "↓ → 근무 기록");
            Assert.IsTrue(_screen.Items[1].IsSelected);
            Assert.IsFalse(_screen.Items[0].IsSelected);
            _screen.Move(1);
            Assert.AreEqual(MainScreen.SettingsIndex, _screen.Selected, "↓ → 설정");
            Color selectedBar = _screen.Items[2].Bar.color;
            Color idleBar = _screen.Items[3].Bar.color;
            Assert.Greater(selectedBar.r + selectedBar.g + selectedBar.b, idleBar.r + idleBar.g + idleBar.b, "선택 막대가 더 밝다");
            Assert.AreEqual(Color.white, _screen.Items[2].Label.color, "선택 글자 = 흰색");
            _screen.Move(1);
            _screen.Move(1);
            Assert.AreEqual(MainScreen.StartIndex, _screen.Selected, "퇴근하기에서 ↓ → 출근하기 (돈다)");
            _screen.Move(-1);
            Assert.AreEqual(MainScreen.QuitIndex, _screen.Selected, "출근하기에서 ↑ → 퇴근하기");

            // 마우스 호버 = 선택
            var data = new PointerEventData(EventSystem.current);
            _screen.Items[2].OnPointerEnter(data);
            Assert.AreEqual(MainScreen.SettingsIndex, _screen.Selected, "설정에 호버 → 선택");
            _screen.Items[0].OnPointerEnter(data);
            Assert.AreEqual(MainScreen.StartIndex, _screen.Selected, "출근하기에 호버 → 선택");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Records_ShowsToastOnly()
        {
            _screen.Select(MainScreen.RecordsIndex);
            _screen.Activate(MainScreen.RecordsIndex);
            yield return WaitReal(0.3f);
            Assert.IsTrue(_screen.IsToastShowing, "준비 중 토스트가 보인다");
            Assert.AreEqual(_screen.Text.ComingSoon, _screen.ToastLabel.text);
            Assert.IsFalse(_screen.Settings.IsOpen, "설정은 안 열림");
            Assert.IsFalse(_screen.IsLeaving, "씬 이동 없음");
            Assert.IsFalse(_screen.QuitRequested, "퇴근 없음");
            yield return WaitReal(2f);
            Assert.IsFalse(_screen.IsToastShowing, "토스트가 사라진다");
            Assert.AreEqual(MainSceneName, SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator Settings_OpenAndCloseWithEscAndButton()
        {
            _screen.Select(MainScreen.SettingsIndex);
            _screen.Activate(MainScreen.SettingsIndex);
            Assert.IsTrue(_screen.Settings.IsOpen, "설정 패널 열림");
            Assert.Greater(_screen.Settings.ResolutionCount, 0, "해상도 항목");
            Assert.IsNotNull(_screen.Settings.DisplayMode, "U10 창 / 전체 화면 버튼");
            _screen.Move(1);
            Assert.AreEqual(MainScreen.SettingsIndex, _screen.Selected, "열려 있는 동안 ↑↓ 무시");
            yield return null;

            _screen.HandleEscape();
            Assert.IsFalse(_screen.Settings.IsOpen, "ESC = 패널 닫기");
            Assert.IsFalse(_screen.QuitRequested, "패널이 열려 있을 땐 ESC 가 퇴근이 아니다");

            _screen.Activate(MainScreen.SettingsIndex);
            Assert.IsTrue(_screen.Settings.IsOpen);
            _screen.Settings.CloseButton.onClick.Invoke();
            Assert.IsFalse(_screen.Settings.IsOpen, "닫기 버튼");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Esc_Quits()
        {
            _screen.HandleEscape();
            Assert.IsTrue(_screen.QuitRequested, "ESC = 퇴근하기");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Start_LoadsGame()
        {
            _screen.Activate(MainScreen.StartIndex);
            Assert.IsTrue(_screen.IsLeaving, "출근하기 → 나가는 중");
            _screen.Move(1);
            Assert.AreEqual(MainScreen.StartIndex, _screen.Selected, "나가는 중엔 입력 무시");
            yield return WaitForScene(GameSceneName);
        }

        [UnityTest]
        public IEnumerator TitleStart_GoesToMain()
        {
            SceneManager.LoadScene(TitleSceneName, LoadSceneMode.Single);
            yield return WaitForScene(TitleSceneName);
            yield return null;
            Button start = null;
            Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
            for (int i = 0; i < buttons.Length; i++) if (buttons[i].name == "StartButton") start = buttons[i];
            Assert.IsNotNull(start, "Title 에 StartButton 이 없다");
            start.onClick.Invoke();
            yield return WaitForScene(MainSceneName);
        }

        [UnityTest]
        public IEnumerator Aspects_InsideAndNoOverlap([Values(0, 1, 2, 3)] int which)
        {
            int[] ws = { 1920, 1920, 2560, 1280 };
            int[] hs = { 1080, 1200, 1080, 720 };
            int w = ws[which], h = hs[which];
            yield return SetResolution(w, h);
            float scale = _screen.GetComponentInParent<Canvas>().scaleFactor;
            float edge = SafeEdge * scale;

            var rects = new List<Rect>();
            for (int i = 0; i < Blocks.Length; i++)
            {
                Transform t = _screen.transform.Find(Blocks[i]);
                Assert.IsNotNull(t, Blocks[i] + " 이 없다");
                Rect r = ScreenRect((RectTransform)t);
                string what = Blocks[i] + " @" + w + "×" + h + " " + r;
                Assert.GreaterOrEqual(r.xMin, edge - 0.5f, what + " 왼쪽");
                Assert.GreaterOrEqual(r.yMin, edge - 0.5f, what + " 아래");
                Assert.LessOrEqual(r.xMax, w - edge + 0.5f, what + " 오른쪽");
                Assert.LessOrEqual(r.yMax, h - edge + 0.5f, what + " 위");
                rects.Add(r);
            }
            for (int i = 0; i < rects.Count; i++)
                for (int j = i + 1; j < rects.Count; j++)
                    Assert.IsFalse(rects[i].Overlaps(rects[j]), Blocks[i] + " ↔ " + Blocks[j] + " 겹침 @" + w + "×" + h);

            // 배경이 화면을 덮는다 (EnvelopeParent)
            Rect bg = ScreenRect((RectTransform)_screen.transform.Find("Background/Frame"));
            Assert.IsTrue(bg.xMin <= 0.5f && bg.yMin <= 0.5f && bg.xMax >= w - 0.5f && bg.yMax >= h - 0.5f, "배경이 화면을 다 덮는다 " + bg);

            // 글자 — 캔버스 단위 24 이상 (1080 높이에서 화면 px 24 이상)
            TMP_Text[] texts = _screen.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
                Assert.GreaterOrEqual(texts[i].fontSize, MinFontPx, texts[i].name + " 글자 크기");
        }

        [UnityTest]
        public IEnumerator Loops_RunAndAllocateNothing()
        {
            // Glow 숨쉬기 · 주사선 · REC 가 실제로 움직인다
            float g0 = _screen.Glow.color.a;
            float s0 = _screen.ScanProgress;
            bool sawOn = false, sawOff = false;
            float end = Time.realtimeSinceStartup + 1.6f;
            while (Time.realtimeSinceStartup < end)
            {
                if (_screen.IsRecOn) sawOn = true; else sawOff = true;
                yield return null;
            }
            Assert.AreNotEqual(g0, _screen.Glow.color.a, "Glow 알파가 변한다");
            Assert.AreNotEqual(s0, _screen.ScanProgress, "주사선이 흐른다");
            Assert.IsTrue(sawOn && sawOff, "REC 가 깜빡인다");

            // 할당 0 — 반복 트윈을 직접 앞으로 돌린다 (데우기 뒤)
            float t = 0f;
            for (int k = 0; k < 10; k++) { t += 0.05f; _screen.GotoLoops(t); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int k = 0; k < 200; k++)
            {
                t += 0.05f;
                _screen.GotoLoops(t);
            }
            Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before, "Glow · 주사선 · REC 할당 0");
        }
    }
}
