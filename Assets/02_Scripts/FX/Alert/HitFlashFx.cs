using System.Collections.Generic;
using QL.Core;
using QL.Data;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Flow;
using QL.Game.Slimes;
using UnityEngine;

namespace QL.FX.Alert
{
    /// <summary>
    /// 점액 히트 플래시 (AlertFx.prefab). SlimeDamaged(view) — 드론 직접 공격만 (V02) — 때 켜진 Model_A/B 렌더러에
    /// MaterialPropertyBlock 으로 <c>_BaseMap</c> = 흰 텍스처 · <c>_BaseColor</c> = (흰색 ↔ 드론 종류색) × 밝기 를 hitFlashDuration 동안 건다
    /// (초록 텍스처에 색을 곱하기만 하면 노랗게 보일 뿐 하얘지지 않는다 — 점액 재질은 _EMISSION 이 꺼져 있어 발광도 못 씀). 트랜스폼 · 재질 에셋은 건드리지 않는다
    /// (U04 출렁임 크기 트윈과 독립). 같은 점액이 또 맞으면 남은 시간만 다시 센다 (트윈 없음 — 게임 시간 타이머).
    /// 추적은 미리 만든 배열 (swap-remove, 할당 없음). SlimeKilled · SlimeReachedHatch 는 풀 반납 직전에 오므로 그때 즉시 원래 블록으로 되돌린다
    /// — 재사용 점액이 흰색으로 남지 않게. 원래 블록 = 플래시 전 렌더러 상태 (아트 점액은 없음 = null, 임시 도형 변이는 mutantTint).
    /// 일시정지 · 결과 (timeScale 0) 때도 즉시 되돌린다. U05 처치 대역(SlimeDouble)은 sharedMaterial 을 복사해 자기 블록을 쓰므로 이 플래시와 무관하다.
    /// </summary>
    public sealed class HitFlashFx : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        [SerializeField] private FxConfig fx;
        [Tooltip("비어 있으면 Start 에 한 번 찾는다 (종류색 = UnitData 1등급 tintColor)")]
        [SerializeField] private GameBootstrap bootstrap;
        [Tooltip("켜진 변형을 찾을 자식 이름 (SlimeView.variantChildNames 와 같게)")]
        [SerializeField] private string[] modelChildNames = { "Model_A", "Model_B" };

        private SlimeView[] _views;
        private Renderer[] _renderers;
        private float[] _timeLeft;
        private bool[] _hadBlock;
        private Color[] _restoreColor;
        private int _count;

        private MaterialPropertyBlock _block;
        private readonly Dictionary<SlimeView, Renderer> _models = new Dictionary<SlimeView, Renderer>(128);
        private readonly Color[] _typeColor = new Color[8];
        private bool _colorsReady;

        public FxConfig Config => fx;
        /// <summary>지금 번쩍이는 점액 수.</summary>
        public int ActiveCount => _count;
        public int Capacity => _views != null ? _views.Length : 0;
        /// <summary>번쩍 시작 총수 (연속 타격의 시간 재시작은 세지 않음).</summary>
        public int FlashCount { get; private set; }

        private void Awake()
        {
            int n = Mathf.Max(1, fx.hitFlashCapacity);
            _views = new SlimeView[n];
            _renderers = new Renderer[n];
            _timeLeft = new float[n];
            _hadBlock = new bool[n];
            _restoreColor = new Color[n];
            _block = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            GameEvents.SlimeSpawned += OnSpawned;
            GameEvents.SlimeDamaged += OnDamaged;
            GameEvents.SlimeKilled += OnGone;
            GameEvents.SlimeReachedHatch += OnGone;
            GameEvents.StateChanged += OnStateChanged;
            GameEvents.GameEnded += OnGameEnded;
        }

        private void OnDisable()
        {
            GameEvents.SlimeSpawned -= OnSpawned;
            GameEvents.SlimeDamaged -= OnDamaged;
            GameEvents.SlimeKilled -= OnGone;
            GameEvents.SlimeReachedHatch -= OnGone;
            GameEvents.StateChanged -= OnStateChanged;
            GameEvents.GameEnded -= OnGameEnded;
            ClearAll();
        }

        private void Start()
        {
            if (!_colorsReady) LoadColors(); // 웨이브 중 첫 타격에 찾지 않게
        }

        /// <summary>이 점액이 지금 번쩍이는 중인가.</summary>
        public bool IsFlashing(SlimeView view) => IndexOf(view) >= 0;

        /// <summary>점액 켜진 Model 의 렌더러 (플래시 대상). 없으면 null.</summary>
        public Renderer RendererOf(SlimeView view) => view != null ? Resolve(view) : null;

        /// <summary>모든 플래시를 즉시 원래대로.</summary>
        public void ClearAll()
        {
            while (_count > 0) RemoveAt(_count - 1);
        }

        private void OnSpawned(SlimeView view)
        {
            if (view != null) Resolve(view); // 첫 타격 프레임에 찾지 않게
        }

        private void OnDamaged(SlimeView view, int damage, DroneType source)
        {
            if (view == null) return;
            int i = IndexOf(view);
            if (i >= 0)
            {
                _timeLeft[i] = fx.hitFlashDuration; // 트윈을 쌓지 않고 시간만 다시
                Paint(i, source);
                return;
            }

            Renderer r = Resolve(view);
            if (r == null) return;
            if (_count >= _views.Length) RemoveAt(0); // 가득 — 가장 먼저 번쩍인 것부터 끝낸다

            i = _count++;
            _views[i] = view;
            _renderers[i] = r;
            _timeLeft[i] = fx.hitFlashDuration;

            // 원래 상태 기억 (플래시 전 블록 · 색)
            _hadBlock[i] = r.HasPropertyBlock();
            Color baseColor = Color.white;
            Material m = r.sharedMaterial;
            if (m != null && m.HasProperty(BaseColorId)) baseColor = m.GetColor(BaseColorId);
            if (_hadBlock[i])
            {
                r.GetPropertyBlock(_block);
                if (_block.HasColor(BaseColorId)) baseColor = _block.GetColor(BaseColorId);
            }
            _restoreColor[i] = baseColor;
            FlashCount++;
            Paint(i, source);
        }

        private void OnGone(SlimeView view)
        {
            int i = IndexOf(view);
            if (i >= 0) RemoveAt(i);
        }

        // 일시정지 · 결과는 timeScale 0 — 타이머가 멈춰 흰 점액이 그대로 남지 않게 바로 되돌린다
        private void OnStateChanged(GameState state)
        {
            if (state == GameState.Paused || state == GameState.Result) ClearAll();
        }

        private void OnGameEnded(ResultSummary summary) => ClearAll();

        private void Update()
        {
            float dt = Time.deltaTime; // 게임 시간 — 일시정지(timeScale 0) 중엔 그대로 멈춘다
            for (int i = _count - 1; i >= 0; i--)
            {
                SlimeView v = _views[i];
                if (v == null || !v.gameObject.activeInHierarchy)
                {
                    RemoveAt(i); // 이벤트 없이 사라진 점액 (씬 정리 등)
                    continue;
                }
                _timeLeft[i] -= dt;
                if (_timeLeft[i] <= 0f) RemoveAt(i);
            }
        }

        private void Paint(int i, DroneType source)
        {
            Color c = Color.Lerp(fx.hitFlashColor, TypeColor(source), fx.hitFlashTypeMix) * fx.hitFlashIntensity;
            c.a = _restoreColor[i].a; // 투명 점액 알파는 그대로
            _block.Clear();
            _block.SetTexture(BaseMapId, Texture2D.whiteTexture);
            _block.SetColor(BaseColorId, c);
            _renderers[i].SetPropertyBlock(_block);
        }

        private void RemoveAt(int i)
        {
            Renderer r = _renderers[i];
            if (r != null)
            {
                if (_hadBlock[i])
                {
                    _block.Clear();
                    _block.SetColor(BaseColorId, _restoreColor[i]);
                    r.SetPropertyBlock(_block);
                }
                else
                {
                    r.SetPropertyBlock(null);
                }
            }

            int last = --_count;
            _views[i] = _views[last];
            _renderers[i] = _renderers[last];
            _timeLeft[i] = _timeLeft[last];
            _hadBlock[i] = _hadBlock[last];
            _restoreColor[i] = _restoreColor[last];
            _views[last] = null;
            _renderers[last] = null;
        }

        private int IndexOf(SlimeView view)
        {
            for (int i = 0; i < _count; i++)
                if (_views[i] == view) return i;
            return -1;
        }

        private Renderer Resolve(SlimeView view)
        {
            if (_models.TryGetValue(view, out Renderer r) && r != null && r.gameObject.activeInHierarchy) return r;

            r = null;
            Transform root = view.transform;
            for (int i = 0; i < modelChildNames.Length; i++)
            {
                Transform c = string.IsNullOrEmpty(modelChildNames[i]) ? null : root.Find(modelChildNames[i]);
                if (c == null || !c.gameObject.activeSelf) continue;
                r = c.GetComponentInChildren<Renderer>();
                break;
            }
            if (r == null) r = root.GetComponentInChildren<Renderer>(); // 변형 자식이 없는 임시 도형
            _models[view] = r;
            return r;
        }

        private Color TypeColor(DroneType type)
        {
            if (!_colorsReady) LoadColors();
            int k = (int)type;
            return k >= 0 && k < _typeColor.Length ? _typeColor[k] : Color.white;
        }

        /// <summary>종류색 = 그 종류 1등급 UnitData.tintColor (처음 한 번).</summary>
        private void LoadColors()
        {
            _colorsReady = true;
            for (int i = 0; i < _typeColor.Length; i++) _typeColor[i] = Color.white;
            if (bootstrap == null) bootstrap = FindAnyObjectByType<GameBootstrap>();
            if (bootstrap == null || bootstrap.Data == null) return;
            IReadOnlyList<UnitData> units = bootstrap.Data.Units;
            for (int i = 0; i < units.Count; i++)
            {
                UnitData u = units[i];
                if (u == null || u.Grade != 1) continue;
                int k = (int)u.DroneType;
                if (k >= 0 && k < _typeColor.Length) _typeColor[k] = u.TintColor;
            }
        }
    }
}
