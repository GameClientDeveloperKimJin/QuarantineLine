using System.Collections.Generic;
using QL.FX.Motion;
using QL.Game;
using QL.Game.Slimes;
using UnityEngine;

namespace QL.FX.Combat
{
    /// <summary>
    /// 처치 「납작 터짐」 (CombatFx.prefab). SlimeKilled(view) — 직접 · 잔열 처치 모두 — 순간 켜진 Model_A/B 의 렌더러를 읽어
    /// 풀의 대역(<see cref="SlimeDouble"/>)에 메시 · 재질 · 월드 위치 · 회전 · 크기를 넘긴다. 점액 루트 · 자식은 읽기만 (U04 규칙) —
    /// SlimeRegistry.Despawn 이 같은 프레임에 반납하므로 죽은 점액 자체는 트윈하지 않는다.
    /// 바닥에 FX_Slime_Splat 퍼짐 → (puddleDelay 뒤) FX_Slime_Puddle 사라짐 · 초록 FX_Spark 몇 점 (FxPool 재사용).
    /// 점액 뷰마다 켜진 Model 렌더러는 처음 한 번 찾아 둔다 (변형은 풀 생성 때 정해져 고정).
    /// </summary>
    public sealed class SlimeDeathFx : MonoBehaviour
    {
        private struct ModelRef
        {
            public MeshFilter Filter;
            public MeshRenderer Renderer;
        }

        [SerializeField] private FxConfig fx;
        [Tooltip("비어 있으면 Awake 에 한 번 찾는다 (FxPool 은 DefaultExecutionOrder -40 이라 먼저 준비됨)")]
        [SerializeField] private FxPool pool;
        [SerializeField] private SlimeDouble prefab;
        [Tooltip("켜진 변형을 찾을 자식 이름 (SlimeView.variantChildNames 와 같게)")]
        [SerializeField] private string[] modelChildNames = { "Model_A", "Model_B" };

        private SlimeDouble[] _items;
        private int _next;
        private readonly Dictionary<SlimeView, ModelRef> _models = new Dictionary<SlimeView, ModelRef>(128);

        public int PoolSize => _items != null ? _items.Length : 0;
        public int CreatedCount { get; private set; }
        public int PlayCount { get; private set; }
        /// <summary>마지막으로 쓴 대역 (없으면 null).</summary>
        public SlimeDouble Last { get; private set; }

        private void Awake()
        {
            if (pool == null) pool = FindAnyObjectByType<FxPool>(); // 프리팹은 씬의 [FX]/FxPool 을 직렬화할 수 없다 (MergeFx 와 같음)
            int size = Mathf.Max(1, fx.deathPoolSize);
            _items = new SlimeDouble[size];
            for (int i = 0; i < size; i++)
            {
                SlimeDouble d = Instantiate(prefab, transform);
                d.name = prefab.name + "_" + i.ToString("00");
                d.Init(fx, pool);
                _items[i] = d;
                CreatedCount++;
            }
        }

        private void OnEnable()
        {
            GameEvents.SlimeSpawned += OnSpawned;
            GameEvents.SlimeKilled += OnKilled;
        }

        private void OnDisable()
        {
            GameEvents.SlimeSpawned -= OnSpawned;
            GameEvents.SlimeKilled -= OnKilled;
        }

        public SlimeDouble ItemAt(int i) => _items[i];

        public int ActiveCount()
        {
            int n = 0;
            for (int i = 0; i < _items.Length; i++)
                if (_items[i].Active) n++;
            return n;
        }

        public void StopAll()
        {
            for (int i = 0; i < _items.Length; i++) _items[i].Stop();
        }

        /// <summary>스폰 때 렌더러를 찾아 둔다 (처치 프레임엔 찾지 않게).</summary>
        private void OnSpawned(SlimeView view)
        {
            if (view != null) Resolve(view);
        }

        private void OnKilled(SlimeView view)
        {
            if (view == null) return;
            ModelRef m = Resolve(view);
            if (m.Filter == null || m.Renderer == null) return;

            Transform mt = m.Filter.transform;
            Vector3 pos = mt.position;
            float k = view.transform.localScale.x; // 점액 크기 (EnemyData.scale × cellSize)

            var puddle = new FxSpriteParams
            {
                Position = new Vector3(pos.x, fx.worldFxHeight, pos.z),
                Travel = Vector3.zero,
                SizeFrom = fx.puddleSize * k,
                SizeTo = fx.puddleSize * k,
                Color = new Color(1f, 1f, 1f, fx.puddleAlpha),
                Duration = fx.puddleDuration,
            };

            SlimeDouble d = _items[_next];
            _next = (_next + 1) % _items.Length;
            d.Play(m.Filter.sharedMesh, m.Renderer.sharedMaterial, pos, mt.rotation, mt.lossyScale, in puddle);
            Last = d;
            PlayCount++;

            if (pool == null) return;

            // 바닥 Splat 퍼짐 (Puddle 과 같은 높이 — 재질 렌더 큐가 Splat 3001 > Puddle 3000 이라 늘 위에 그려진다)
            var p = new FxSpriteParams
            {
                Position = new Vector3(pos.x, fx.worldFxHeight, pos.z),
                Travel = Vector3.zero,
                SizeFrom = fx.splatSizeFrom * k,
                SizeTo = fx.splatSizeTo * k,
                Color = new Color(1f, 1f, 1f, fx.splatAlpha),
                Duration = fx.splatDuration,
            };
            pool.Play(FxKind.Splat, in p);

            // 초록 반짝임 — 원 둘레로 고르게, 위로 튀며 흩어짐
            int count = fx.deathSparkCount;
            Vector3 center = pos + Vector3.up * (0.2f * k);
            p.SizeFrom = fx.deathSparkSize * k;
            p.SizeTo = fx.deathSparkSizeEnd * k;
            p.Color = fx.deathSparkColor;
            p.Duration = fx.deathSparkDuration;
            p.Position = center;
            for (int i = 0; i < count; i++)
            {
                float a = (i + 0.5f) * (Mathf.PI * 2f / count);
                p.Travel = new Vector3(Mathf.Cos(a) * fx.deathSparkDistance * k, fx.deathSparkRise * k, Mathf.Sin(a) * fx.deathSparkDistance * k);
                pool.Play(FxKind.Spark, in p);
            }
        }

        private ModelRef Resolve(SlimeView view)
        {
            if (_models.TryGetValue(view, out ModelRef m) && m.Filter != null && m.Filter.gameObject.activeInHierarchy) return m;

            m = default;
            Transform root = view.transform;
            for (int i = 0; i < modelChildNames.Length; i++)
            {
                Transform c = string.IsNullOrEmpty(modelChildNames[i]) ? null : root.Find(modelChildNames[i]);
                if (c == null || !c.gameObject.activeSelf) continue;
                m.Filter = c.GetComponentInChildren<MeshFilter>();
                m.Renderer = c.GetComponentInChildren<MeshRenderer>();
                break;
            }
            if (m.Filter == null) // 변형 자식이 없는 임시 도형
            {
                m.Filter = root.GetComponentInChildren<MeshFilter>();
                m.Renderer = root.GetComponentInChildren<MeshRenderer>();
            }
            _models[view] = m;
            return m;
        }
    }
}
