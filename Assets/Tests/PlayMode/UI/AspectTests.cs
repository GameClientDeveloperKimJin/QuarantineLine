using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using QL.Core;
using QL.FX;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Drones;
using QL.Game.Flow;
using QL.Game.Input;
using QL.Game.Slimes;
using QL.Game.Stage;
using QL.UI.Hud;
using QL.UI.Screens;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace QL.Tests.PlayMode.UI
{
    /// <summary>
    /// U10 화면비 · 창 모드. Game.unity 를 열고 GameLoop 를 멈춘 채 (준비 상태 그대로) Game View 해상도를 바꿔 가며
    /// HUD 패널 화면 사각형 (서로 · 그리드와 겹침 0, 화면 가장자리 24 이상) · 칸 클릭 → 맞는 칸 · 스폰 점액이 해치 패널 아래 ·
    /// 일시정지 카드 · 결과 패널이 화면 안인지 본다. 해상도는 에디터 전용 <c>PlayModeWindow.SetCustomRenderingResolution</c> —
    /// 끝나면 1920×1080 으로 돌려 둔다.
    /// </summary>
    public sealed class AspectTests
    {
        private const string SceneName = "Game";
        private const int MaxWaitFrames = 300;
        private const float SafeEdge = 24f;     // 캔버스 단위 (1920×1080 기준 px)
        private const float MinFontPx = 24f;    // S3 규칙 8
        private const float Epsilon = 0.5f;     // px

        private static readonly string[] HudPanels =
        {
            "Hud/WavePanel", "Hud/HatchPanel", "Hud/CoinPanel", "PauseButton", "Hud/OpsMessenger", "Hud/DeployButton",
        };

        private GameLoop _loop;
        private GameBootstrap _bootstrap;
        private SlimeRegistry _slimes;
        private CameraRig _rig;
        private Canvas _uiCanvas;
        private Transform _inGame;
        private FxConfig _fx;

        private GridSpace Space => _bootstrap.Space;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameEvents.ClearAll();
            SceneManager.sceneLoaded += DisableSeeder;
            SceneManager.LoadScene(SceneName, LoadSceneMode.Single);

            GameLoop found = null;
            for (int i = 0; i < MaxWaitFrames; i++)
            {
                yield return null;
                found = Object.FindAnyObjectByType<GameLoop>();
                StageBuilder stage = Object.FindAnyObjectByType<StageBuilder>();
                if (found != null && found.IsReady && stage != null && stage.IsBuilt) break;
            }
            SceneManager.sceneLoaded -= DisableSeeder;
            Assert.IsTrue(found != null && found.IsReady, "GameLoop 가 준비되지 않았다");

            _loop = found;
            _loop.enabled = false; // 준비 시간이 흘러 웨이브가 시작되지 않게
            _bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            _slimes = Object.FindAnyObjectByType<SlimeRegistry>();
            _rig = Object.FindAnyObjectByType<CameraRig>();
            Object.FindAnyObjectByType<DragController>().enabled = false; // 실제 마우스가 끼어들지 않게
            Assert.IsNotNull(_rig);
            Assert.IsNotNull(_slimes);
            UIRoot ui = Object.FindAnyObjectByType<UIRoot>();
            Assert.IsNotNull(ui);
            _uiCanvas = ui.GetComponent<Canvas>();
            _inGame = ui.transform.Find("InGameScreen");
            Assert.IsNotNull(_inGame, "UIRoot/InGameScreen");
            _fx = Object.FindAnyObjectByType<HudContext>().Fx;
            Assert.IsNotNull(_fx);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SceneManager.sceneLoaded -= DisableSeeder;
            yield return SetResolution(1920, 1080);
            Time.timeScale = 1f;
            GameEvents.ClearAll();
        }

        // ── 도우미 ───────────────────────────────────────────

        private static void DisableSeeder(Scene scene, LoadSceneMode mode)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                DroneSeeder[] seeders = roots[i].GetComponentsInChildren<DroneSeeder>(true);
                for (int j = 0; j < seeders.Length; j++) seeders[j].enabled = false;
            }
        }

        private static IEnumerator SetResolution(int w, int h)
        {
#if UNITY_EDITOR
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution((uint)w, (uint)h, "U10 AspectTests");
#endif
            for (int i = 0; i < MaxWaitFrames && (Screen.width != w || Screen.height != h); i++) yield return null;
            Assert.AreEqual(new Vector2Int(w, h), new Vector2Int(Screen.width, Screen.height), "Game View 해상도가 바뀌지 않았다");
            yield return null; // CameraRig LateUpdate · Canvas Scaler 가 새 크기로 한 번 돈다
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>RectTransform 의 화면 사각형 (Overlay 캔버스 = 월드 좌표가 화면 px).</summary>
        private static Rect ScreenRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        /// <summary>localScale 을 빼고 (열림 연출 중간값과 무관하게) 크기 그대로의 화면 사각형.</summary>
        private static Rect UnscaledScreenRect(RectTransform rt, float scaleFactor)
        {
            Vector2 size = rt.rect.size * scaleFactor;
            Vector2 center = rt.TransformPoint(rt.rect.center);
            return new Rect(center - size * 0.5f, size);
        }

        private static bool Overlaps(Rect a, Rect b)
        {
            return a.xMin < b.xMax - Epsilon && b.xMin < a.xMax - Epsilon && a.yMin < b.yMax - Epsilon && b.yMin < a.yMax - Epsilon;
        }

        private Rect GridRect()
        {
            Camera cam = _rig.Camera;
            float h = Space.CellSize * 0.5f;
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            for (int i = 0; i < Space.CellCount; i++)
            {
                Vector3 c = Space.CellToWorld(i);
                for (int k = 0; k < 4; k++)
                {
                    Vector3 s = cam.WorldToScreenPoint(c + new Vector3((k & 1) == 0 ? -h : h, 0f, (k & 2) == 0 ? -h : h));
                    x0 = Mathf.Min(x0, s.x); x1 = Mathf.Max(x1, s.x);
                    y0 = Mathf.Min(y0, s.y); y1 = Mathf.Max(y1, s.y);
                }
            }
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }

        private void AssertInsideSafe(string name, Rect r, float scale)
        {
            float edge = SafeEdge * scale - Epsilon;
            Assert.GreaterOrEqual(r.xMin, edge, name + " 왼쪽 여백 " + Screen.width + "x" + Screen.height + " " + r);
            Assert.GreaterOrEqual(r.yMin, edge, name + " 아래 여백 " + Screen.width + "x" + Screen.height + " " + r);
            Assert.LessOrEqual(r.xMax, Screen.width - edge, name + " 오른쪽 여백 " + Screen.width + "x" + Screen.height + " " + r);
            Assert.LessOrEqual(r.yMax, Screen.height - edge, name + " 위 여백 " + Screen.width + "x" + Screen.height + " " + r);
        }

        private IEnumerator CheckLayout(int w, int h)
        {
            yield return SetResolution(w, h);
            string res = " @" + w + "x" + h;
            float scale = _uiCanvas.scaleFactor;
            Assert.AreEqual(h / 1080f, scale, 1e-3f, "높이 기준 스케일 (match 1)" + res);

            // 1. HUD 패널 — 화면 안 24 이상 · 서로 겹침 0 · 그리드와 겹침 0
            var rects = new List<Rect>();
            for (int i = 0; i < HudPanels.Length; i++)
            {
                Transform t = _inGame.Find(HudPanels[i]);
                Assert.IsNotNull(t, HudPanels[i]);
                Rect r = ScreenRect((RectTransform)t);
                AssertInsideSafe(HudPanels[i], r, scale);
                rects.Add(r);
            }
            Rect grid = GridRect();
            AssertInsideSafe("그리드", grid, scale);
            for (int i = 0; i < rects.Count; i++)
            {
                for (int j = i + 1; j < rects.Count; j++)
                    Assert.IsFalse(Overlaps(rects[i], rects[j]), HudPanels[i] + " ↔ " + HudPanels[j] + " 겹침" + res + " " + rects[i] + " " + rects[j]);
                Assert.IsFalse(Overlaps(rects[i], grid), HudPanels[i] + " ↔ 그리드 겹침" + res + " " + rects[i] + " " + grid);
            }

            // 2. 스폰 직후 점액 (레인 0 · 2 · 4, 두 종류) 이 해치 패널 아래
            float hatchBottom = rects[1].yMin;
            Camera cam = _rig.Camera;
            SlimeType[] types = { SlimeType.Blob, SlimeType.Mutant };
            for (int ti = 0; ti < types.Length; ti++)
            {
                for (int lane = 0; lane < Space.LaneCount; lane += 2)
                {
                    SlimeView s = _slimes.Spawn(types[ti], lane);
                    Assert.IsNotNull(s);
                    Renderer[] rs = s.GetComponentsInChildren<Renderer>();
                    Assert.Greater(rs.Length, 0);
                    Bounds b = rs[0].bounds;
                    for (int k = 1; k < rs.Length; k++) b.Encapsulate(rs[k].bounds);
                    float top = float.MinValue;
                    for (int k = 0; k < 8; k++)
                    {
                        Vector3 p = new Vector3((k & 1) == 0 ? b.min.x : b.max.x, (k & 2) == 0 ? b.min.y : b.max.y, (k & 4) == 0 ? b.min.z : b.max.z);
                        top = Mathf.Max(top, cam.WorldToScreenPoint(p).y);
                    }
                    _slimes.Despawn(s, DespawnReason.Killed);
                    Assert.Less(top, hatchBottom, types[ti] + " 레인 " + lane + " 스폰 위끝이 해치 패널에 가린다" + res);
                }
            }

            // 3. 칸 클릭 → 맞는 칸 (중심 + 네 모서리 안쪽 40%) — DragController.Update 와 같은 길 (레이 → 바닥 → 칸)
            float inset = Space.CellSize * 0.4f;
            for (int i = 0; i < Space.CellCount; i++)
            {
                Vector3 c = Space.CellToWorld(i);
                for (int k = 0; k < 5; k++)
                {
                    Vector3 world = k == 4 ? c : c + new Vector3((k & 1) == 0 ? -inset : inset, 0f, (k & 2) == 0 ? -inset : inset);
                    Vector3 screen = cam.WorldToScreenPoint(world);
                    Assert.IsTrue(Space.TryRaycastFloor(cam.ScreenPointToRay(screen), out Vector3 hit), "바닥 레이" + res);
                    Assert.AreEqual(i, Space.WorldToCell(hit), "칸 " + i + " 점 " + k + " 클릭" + res);
                }
            }

            // 4. 일시정지 카드 · 결과 패널 — 크기 그대로 (열림 연출 Scale 제외) 화면 안 24 이상
            Transform root = _uiCanvas.transform;
            AssertInsideSafe("PauseScreen/Card", UnscaledScreenRect((RectTransform)root.Find("PauseScreen/Card"), scale), scale);
            AssertInsideSafe("ResultScreen/Panel", UnscaledScreenRect((RectTransform)root.Find("ResultScreen/Panel"), scale), scale);

            // 5. 글자 — 1080 이상 높이에서 화면 px 24 이상
            if (h >= 1080)
            {
                TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
                for (int i = 0; i < texts.Length; i++)
                {
                    float size = texts[i].enableAutoSizing ? texts[i].fontSizeMin : texts[i].fontSize;
                    Assert.GreaterOrEqual(size * scale, MinFontPx - 0.01f, texts[i].name + " 글자 px" + res);
                }
            }
        }

        // ── 테스트 ────────────────────────────────────────────

        [Test]
        public void Scalers_AllScreenCanvasesShareReference()
        {
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int roots = 0;
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas c = canvases[i];
                if (!c.isRootCanvas) continue;
                Assert.AreEqual(RenderMode.ScreenSpaceOverlay, c.renderMode, c.name);
                CanvasScaler s = c.GetComponent<CanvasScaler>();
                Assert.IsNotNull(s, c.name + " 에 Canvas Scaler 가 없다");
                Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, s.uiScaleMode, c.name);
                Assert.AreEqual(new Vector2(1920f, 1080f), s.referenceResolution, c.name);
                Assert.AreEqual(CanvasScaler.ScreenMatchMode.MatchWidthOrHeight, s.screenMatchMode, c.name);
                Assert.AreEqual(_fx.canvasMatchWidthOrHeight, s.matchWidthOrHeight, 1e-4f, c.name + " match");
                roots++;
            }
            Assert.GreaterOrEqual(roots, 3, "UIRoot · CoinCanvas · DamageCanvas");
            Assert.AreEqual(1f, _fx.canvasMatchWidthOrHeight, 1e-4f, "U10 결정 = 높이 기준");
        }

        [UnityTest]
        public IEnumerator Layout_16x9() { yield return CheckLayout(1920, 1080); }

        [UnityTest]
        public IEnumerator Layout_16x10() { yield return CheckLayout(1920, 1200); }

        [UnityTest]
        public IEnumerator Layout_21x9() { yield return CheckLayout(2560, 1080); }

        [UnityTest]
        public IEnumerator Layout_Window1280x720() { yield return CheckLayout(1280, 720); }

        [UnityTest]
        public IEnumerator DisplayToggle_FlipsModeAndLabel()
        {
            DisplayModeToggle toggle = _uiCanvas.GetComponentInChildren<DisplayModeToggle>(true);
            Assert.IsNotNull(toggle, "PauseScreen/Card 에 DisplayModeToggle 버튼이 없다");
            Assert.AreEqual(FullScreenMode.FullScreenWindow, DisplayModeToggle.Next(FullScreenMode.Windowed));
            Assert.AreEqual(FullScreenMode.Windowed, DisplayModeToggle.Next(FullScreenMode.FullScreenWindow));
            Assert.AreEqual(FullScreenMode.Windowed, DisplayModeToggle.Next(FullScreenMode.ExclusiveFullScreen));

            FullScreenMode start = toggle.RequestedMode;
            string Expected(FullScreenMode m) => m == FullScreenMode.Windowed ? _fx.displayToFullscreenLabel : _fx.displayToWindowLabel;
            Assert.AreEqual(Expected(start), toggle.Label.text, "시작 글자");

            toggle.Button.onClick.Invoke();
            yield return null;
            Assert.AreEqual(DisplayModeToggle.Next(start), toggle.RequestedMode, "누르면 모드가 바뀐다");
            Assert.AreEqual(Expected(toggle.RequestedMode), toggle.Label.text, "글자가 따라간다");
            Assert.GreaterOrEqual(toggle.Label.fontSize, MinFontPx);

            toggle.Button.onClick.Invoke();
            yield return null;
            Assert.AreEqual(start, toggle.RequestedMode, "두 번 누르면 원래 모드");
            Assert.AreEqual(Expected(start), toggle.Label.text);
        }
    }
}
