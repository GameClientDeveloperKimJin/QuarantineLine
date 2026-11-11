using QL.Core.Waves;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Flow;
using QL.Game.Stage;
using TMPro;
using UnityEngine;

namespace QL.FX.Alert
{
    /// <summary>
    /// U17 (보류함 9 · 33) 스폰 게이트 표기판 글자 (AlertFx.prefab). R3 <c>Stage_SpawnGate</c> 의 빈 Transform <c>GateSign</c>
    /// (G09 — 게이트 왼쪽 기둥 앞, 화면에서 해치 패널 바로 아래) 자리에 월드 TextMeshPro 「C-1」 을 띄운다.
    /// R3 프리팹 · 무대 계층은 고치지 않는다 — 글자 오브젝트는 이 컴포넌트 아래에 두고 위치만 GateSign 월드 위치를 따른다 (무대는 움직이지 않음).
    /// 문구 = 그 웨이브 WaveData bannerText 의 첫 낱말 (「C-1 화물칸, …」 → 「C-1」). <c>WavePrepStarted(index)</c> 마다 교체,
    /// 처음 값은 무대가 지어진 뒤 <c>GameLoop.Flow.WaveIndex</c> (V02 시작값). 문자열은 처음 한 번 잘라 캐시 — 교체 때 할당 없음.
    /// 글자 면은 카메라 회전을 그대로 따른다 (고정 쿼터뷰 — 처음 한 번). 수치는 FxConfig 「U17 게이트 표기판」.
    /// </summary>
    public sealed class GateSignFx : MonoBehaviour
    {
        [SerializeField] private FxConfig fx;
        [Tooltip("QL_BagelFatOne SDF (UI 와 같은 글꼴 하나)")]
        [SerializeField] private TMP_FontAsset font;
        [Tooltip("StageBuilder 아래 스폰 게이트 모듈 이름")]
        [SerializeField] private string gateName = "SpawnGate";
        [Tooltip("게이트 모듈 아래 표기판 Transform 이름 (R3)")]
        [SerializeField] private string signName = "GateSign";

        private StageBuilder _stage;
        private GameLoop _loop;
        private GameBootstrap _bootstrap;
        private TextMeshPro _text;
        private Transform _sign;
        private string[] _codes;
        private int _index = -1;
        private bool _resolved;

        /// <summary>글자 (무대가 지어지기 전엔 null).</summary>
        public TextMeshPro Text => _text;
        /// <summary>찾은 R3 표기판 자리 (없으면 null).</summary>
        public Transform Sign => _sign;
        /// <summary>지금 보이는 웨이브 (0부터, 없으면 −1).</summary>
        public int ShownIndex => _index;

        private void OnEnable() => GameEvents.WavePrepStarted += OnWavePrepStarted;
        private void OnDisable() => GameEvents.WavePrepStarted -= OnWavePrepStarted;

        private void Start()
        {
            _stage = FindAnyObjectByType<StageBuilder>();
            _loop = FindAnyObjectByType<GameLoop>();
            _bootstrap = FindAnyObjectByType<GameBootstrap>();
        }

        private void LateUpdate()
        {
            if (!_resolved) TryResolve(); // 끄지 않는다 — 끄면 OnDisable 로 WavePrepStarted 구독도 풀림
        }

        /// <summary>코드 = bannerText 첫 낱말 (공백 · 쉼표 앞).</summary>
        public static string CodeOf(string banner)
        {
            if (string.IsNullOrEmpty(banner)) return string.Empty;
            int cut = banner.Length;
            int space = banner.IndexOf(' ');
            int comma = banner.IndexOf(',');
            if (space >= 0 && space < cut) cut = space;
            if (comma >= 0 && comma < cut) cut = comma;
            return banner.Substring(0, cut).Trim();
        }

        /// <summary>그 웨이브 (0부터) 코드 — 범위 밖이면 빈 문자열.</summary>
        public string CodeAt(int index)
        {
            if (_codes == null) BuildCodes();
            return _codes != null && index >= 0 && index < _codes.Length ? _codes[index] : string.Empty;
        }

        private void OnWavePrepStarted(int index, WaveForecast forecast) => Show(index);

        private void Show(int index)
        {
            _index = index;
            if (_text == null) return; // 무대가 지어지면 TryResolve 가 지금 값으로 채운다
            _text.SetText(CodeAt(index));
        }

        private void BuildCodes()
        {
            if (_bootstrap == null) _bootstrap = FindAnyObjectByType<GameBootstrap>();
            var waves = _bootstrap != null && _bootstrap.Data != null && _bootstrap.Data.Waves != null ? _bootstrap.Data.Waves.Waves : null;
            if (waves == null) return;
            _codes = new string[waves.Count];
            for (int i = 0; i < _codes.Length; i++) _codes[i] = CodeOf(waves[i].BannerText);
        }

        private void TryResolve()
        {
            if (fx == null || font == null) { _resolved = true; return; }
            if (_stage == null || !_stage.IsBuilt) return;
            Transform gate = _stage.transform.Find(gateName);
            _sign = gate != null ? FindDeep(gate, signName) : null;
            _resolved = true;
            if (_sign == null)
            {
                Debug.LogWarning("[GateSignFx] 무대에 '" + gateName + "/" + signName + "' 가 없다 — 표기판 글자 없음", this);
                return;
            }

            var go = new GameObject("GateSignText", typeof(TextMeshPro));
            Transform t = go.transform;
            t.SetParent(transform, false);
            Camera cam = Camera.main;
            float cell = _bootstrap != null && _bootstrap.Data != null && _bootstrap.Data.Config != null ? _bootstrap.Data.Config.CellSize : 1f;
            t.position = _sign.position + fx.gateSignOffset * cell;
            t.rotation = cam != null ? cam.transform.rotation : Quaternion.identity; // 글자 면 = 카메라 면 (고정 쿼터뷰)

            _text = go.GetComponent<TextMeshPro>();
            _text.font = font;
            _text.fontSize = fx.gateSignFontSize;
            _text.color = fx.gateSignColor;
            _text.alignment = TextAlignmentOptions.Center;
            _text.textWrappingMode = TextWrappingModes.NoWrap;
            _text.overflowMode = TextOverflowModes.Overflow;
            _text.raycastTarget = false;
            _text.rectTransform.sizeDelta = fx.gateSignBox;
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            int index = _index;
            if (index < 0 && _loop != null && _loop.IsReady) index = _loop.Flow.WaveIndex; // 시작값 (첫 WavePrepStarted 가 먼저 지나갔을 때)
            Show(index < 0 ? 0 : index);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
