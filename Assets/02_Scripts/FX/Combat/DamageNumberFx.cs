using System.Collections.Generic;
using QL.Core;
using QL.Data;
using QL.FX.Motion;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Slimes;
using UnityEngine;

namespace QL.FX.Combat
{
    /// <summary>
    /// 데미지 숫자 (CombatFx.prefab). SlimeDamaged(view, damage, source) → 점액 머리 위 월드 위치를 화면 좌표로 바꿔
    /// 오버레이 캔버스 한 장(<see cref="canvasRect"/>)의 TMP 숫자 풀에서 하나 띄운다. 숫자는 그 화면 자리에서 튀어 오르며 사라진다.
    /// 겹침 방지: 같은 점액이 <see cref="FxConfig.damageMergeWindow"/> 안에 또 맞으면 떠 있는 숫자에 합산 + 톡 튐.
    /// 서로 다른 점액은 x 무작위 ± <see cref="FxConfig.damageSpreadX"/>. 합이 <see cref="FxConfig.damageBigThreshold"/> 이상이면
    /// 크게 + 종류색(UnitData.tintColor, 1등급). 잔열 피해는 이벤트가 없어 숫자도 없다 (보류함 6).
    /// 풀은 Awake 에 미리 만들고 모자라면 가장 오래 전에 띄운 것을 다시 쓴다.
    /// </summary>
    public sealed class DamageNumberFx : MonoBehaviour
    {
        [SerializeField] private FxConfig fx;
        [Tooltip("비어 있으면 Awake 에 한 번 찾는다 (FxPool 은 DefaultExecutionOrder -40 이라 먼저 준비됨)")]
        [SerializeField] private FxPool pool;
        [Tooltip("씬 인스턴스에서 지정. 비어 있으면 처음 한 번 찾는다")]
        [SerializeField] private GameBootstrap bootstrap;
        [Tooltip("숫자를 놓을 오버레이 캔버스의 RectTransform (가운데 피벗)")]
        [SerializeField] private RectTransform canvasRect;
        [SerializeField] private DamageNumber prefab;

        private DamageNumber[] _items;
        private int _next;
        private int _serial;
        private readonly Color[] _typeColor = { Color.white, Color.white, Color.white };
        private bool _colorsReady;

        public int PoolSize => _items != null ? _items.Length : 0;
        public int CreatedCount { get; private set; }
        /// <summary>새로 띄운 수 (합산 제외).</summary>
        public int SpawnCount => _serial;
        /// <summary>합산된 수.</summary>
        public int MergeCount { get; private set; }

        private void Awake()
        {
            if (pool == null) pool = FindAnyObjectByType<FxPool>(); // 프리팹은 씬의 [FX]/FxPool 을 직렬화할 수 없다 (MergeFx 와 같음)
            int size = Mathf.Max(1, fx.damagePoolSize);
            _items = new DamageNumber[size];
            for (int i = 0; i < size; i++)
            {
                DamageNumber n = Instantiate(prefab, canvasRect);
                n.name = prefab.name + "_" + i.ToString("00");
                n.Init(fx);
                _items[i] = n;
                CreatedCount++;
            }
        }

        private void OnEnable()
        {
            GameEvents.SlimeDamaged += OnDamaged;
            GameEvents.GameEnded += OnGameEnded;
        }

        private void OnDisable()
        {
            GameEvents.SlimeDamaged -= OnDamaged;
            GameEvents.GameEnded -= OnGameEnded;
        }

        /// <summary>판 끝 — 떠 있는 숫자를 즉시 전부 끈다 (timeScale 0 으로 결과 화면 뒤에 얼어붙지 않게, U13). 휴게(Paused)는 그대로 멈춰 둔다.</summary>
        private void OnGameEnded(QL.Game.Flow.ResultSummary r) => StopAll();

        /// <summary>지금 보이는 숫자 수.</summary>
        public int ActiveCount()
        {
            int n = 0;
            for (int i = 0; i < _items.Length; i++)
                if (_items[i].Active) n++;
            return n;
        }

        /// <summary>그 점액에 떠 있는 숫자 (없으면 null).</summary>
        public DamageNumber ActiveFor(SlimeView view)
        {
            for (int i = 0; i < _items.Length; i++)
                if (_items[i].Active && _items[i].View == view) return _items[i];
            return null;
        }

        public DamageNumber ItemAt(int i) => _items[i];

        /// <summary>전부 끄기.</summary>
        public void StopAll()
        {
            for (int i = 0; i < _items.Length; i++) _items[i].Stop();
        }

        private void OnDamaged(SlimeView view, int damage, DroneType source)
        {
            if (view == null || damage <= 0) return;
            float now = Time.time;
            Color typeColor = TypeColor(source);

            DamageNumber same = ActiveFor(view);
            if (same != null && now - same.LastHitTime <= fx.damageMergeWindow)
            {
                int total = same.Value + damage;
                bool bigMerged = total >= fx.damageBigThreshold;
                same.Add(damage, now, bigMerged ? typeColor : fx.damageColor, bigMerged ? fx.damageBigFontSize : fx.damageFontSize);
                MergeCount++;
                return;
            }

            Camera cam = pool != null ? pool.ViewCamera : Camera.main;
            if (cam == null) return;
            Transform t = view.transform;
            Vector3 world = t.position + Vector3.up * (fx.damageHeadHeight * t.localScale.y);
            Vector3 screen = cam.WorldToScreenPoint(world);
            if (screen.z <= 0f) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local)) return;
            local.x += Random.Range(-fx.damageSpreadX, fx.damageSpreadX);

            DamageNumber n = _items[_next];
            Separate(ref local, n);
            _next = (_next + 1) % _items.Length;
            n.Serial = ++_serial;
            bool big = damage >= fx.damageBigThreshold;
            n.Play(view, damage, local, now, big ? typeColor : fx.damageColor, big ? fx.damageBigFontSize : fx.damageFontSize);
        }

        /// <summary>
        /// 떠 있는 다른 숫자와 시작 높이가 비슷하고(± damageSeparationY) 가로로 붙어 있으면(&lt; damageSeparationX) 옆으로 민다.
        /// 숫자는 위로만 움직이므로 가로로 떨어뜨리면 수명 내내 겹치지 않는다 (같은 레인에 점액이 줄지어 맞을 때).
        /// </summary>
        private void Separate(ref Vector2 local, DamageNumber reuse)
        {
            float sx = fx.damageSeparationX;
            float sy = fx.damageSeparationY;
            for (int pass = 0; pass < _items.Length; pass++)
            {
                bool moved = false;
                for (int i = 0; i < _items.Length; i++)
                {
                    DamageNumber m = _items[i];
                    if (m == reuse || !m.Active) continue;
                    Vector2 o = m.Origin;
                    float dx = local.x - o.x;
                    if (Mathf.Abs(dx) >= sx || Mathf.Abs(local.y - o.y) >= sy) continue;
                    local.x = o.x + (dx >= 0f ? sx : -sx);
                    moved = true;
                }
                if (!moved) return;
            }
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

        private void Start()
        {
            if (!_colorsReady) LoadColors(); // 웨이브 중 첫 타격에 찾지 않게 시작 때
        }
    }
}
