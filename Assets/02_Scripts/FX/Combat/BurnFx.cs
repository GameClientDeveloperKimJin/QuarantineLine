using QL.FX.Motion;
using QL.Game;
using QL.Game.Slimes;
using UnityEngine;

namespace QL.FX.Combat
{
    /// <summary>
    /// 잔열 불꽃 (CombatFx.prefab, 보류함 6 — 잔열 숫자 대신). 대상 목록 = 살아 있는 점액, 이벤트로 관리
    /// (SlimeSpawned 넣기 · SlimeKilled / SlimeReachedHatch 빼기 — 미리 만든 배열, swap-remove, 할당 없음).
    /// LateUpdate 에서 <c>SlimeCombat.Burning</c> 인 점액마다 풀의 불꽃 하나를 그 위치로 옮긴다 (부모 변경 없음). 꺼지면 그 프레임에 사라짐.
    /// 불꽃 풀 크기 = <see cref="FxConfig.burnPoolSize"/>, 넘치면 먼저 나온 점액부터.
    /// </summary>
    public sealed class BurnFx : MonoBehaviour
    {
        [SerializeField] private FxConfig fx;
        [Tooltip("비어 있으면 Awake 에 한 번 찾는다 (FxPool 은 DefaultExecutionOrder -40 이라 먼저 준비됨)")]
        [SerializeField] private FxPool pool;
        [SerializeField] private BurnFlame prefab;

        private BurnFlame[] _flames;
        private SlimeView[] _targets;
        private int _targetCount;

        public int PoolSize => _flames != null ? _flames.Length : 0;
        public int CreatedCount { get; private set; }
        /// <summary>추적 중인 점액 수.</summary>
        public int TrackedCount => _targetCount;
        /// <summary>지난 LateUpdate 에 켠 불꽃 수.</summary>
        public int ActiveCount { get; private set; }

        private void Awake()
        {
            if (pool == null) pool = FindAnyObjectByType<FxPool>(); // 프리팹은 씬의 [FX]/FxPool 을 직렬화할 수 없다 (MergeFx 와 같음)
            int size = Mathf.Max(1, fx.burnPoolSize);
            _flames = new BurnFlame[size];
            for (int i = 0; i < size; i++)
            {
                BurnFlame f = Instantiate(prefab, transform);
                f.name = prefab.name + "_" + i.ToString("00");
                f.Init();
                _flames[i] = f;
                CreatedCount++;
            }
            _targets = new SlimeView[Mathf.Max(1, fx.burnTrackCapacity)];
        }

        private void OnEnable()
        {
            GameEvents.SlimeSpawned += OnSpawned;
            GameEvents.SlimeKilled += OnGone;
            GameEvents.SlimeReachedHatch += OnGone;
        }

        private void OnDisable()
        {
            GameEvents.SlimeSpawned -= OnSpawned;
            GameEvents.SlimeKilled -= OnGone;
            GameEvents.SlimeReachedHatch -= OnGone;
        }

        public BurnFlame FlameAt(int i) => _flames[i];

        private void OnSpawned(SlimeView view)
        {
            if (view == null || IndexOf(view) >= 0 || _targetCount >= _targets.Length) return;
            _targets[_targetCount++] = view;
        }

        private void OnGone(SlimeView view)
        {
            int i = IndexOf(view);
            if (i < 0) return;
            _targetCount--;
            _targets[i] = _targets[_targetCount];
            _targets[_targetCount] = null;
        }

        private int IndexOf(SlimeView view)
        {
            for (int i = 0; i < _targetCount; i++)
                if (_targets[i] == view) return i;
            return -1;
        }

        private void LateUpdate()
        {
            Camera cam = pool != null ? pool.ViewCamera : Camera.main;
            Quaternion rot = cam != null ? cam.transform.rotation : Quaternion.identity;
            Vector3 camPos = cam != null ? cam.transform.position : Vector3.zero;
            float time = Time.time;

            int used = 0;
            for (int i = 0; i < _targetCount && used < _flames.Length; i++)
            {
                SlimeView v = _targets[i];
                if (v == null || v.Combat == null || !v.Combat.Burning || !v.gameObject.activeInHierarchy) continue;
                Transform t = v.transform;
                Vector3 foot = t.position;
                Vector3 toCam = cam != null ? (camPos - foot).normalized : Vector3.zero;
                _flames[used++].Show(v.Id, foot, t.localScale.y, rot, toCam, time, fx);
            }
            for (int i = used; i < _flames.Length; i++) _flames[i].Hide();
            ActiveCount = used;
        }
    }
}
