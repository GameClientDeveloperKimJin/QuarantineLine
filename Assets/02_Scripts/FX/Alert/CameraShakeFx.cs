using QL.Core;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Flow;
using QL.Game.Stage;
using UnityEngine;

namespace QL.FX.Alert
{
    /// <summary>
    /// 해치 피격 카메라 흔들림 (AlertFx.prefab). SlimeReachedHatch 마다 shakeDuration 동안 <see cref="CameraRig.SetShakeOffset"/> 로
    /// 카메라 로컬 x · y 를 Perlin 노이즈로 흔든다 (남은 시간 비율로 잦아듦). 위기(IsCritical)면 세기 × shakeCriticalMultiplier.
    /// 연속 피격은 세기에 shakeStackRatio 몫만 더하되 shakeStrengthCap 을 넘지 않고, 시간은 처음부터 다시 — 멀미 나게 쌓이지 않게.
    /// 게임 시간(Time.deltaTime) — timeScale 0 이면 멈춘다. 일시정지 · 결과(StateChanged Paused/Result, GameEnded)는 즉시 원위치.
    /// 끝나면 반드시 SetShakeOffset(Vector3.zero). 트윈 없음 (할당 없음).
    /// </summary>
    public sealed class CameraShakeFx : MonoBehaviour
    {
        [SerializeField] private FxConfig fx;
        [Tooltip("비어 있으면 Start 에 한 번 찾는다 (프리팹은 씬의 [Camera] 를 직렬화할 수 없다)")]
        [SerializeField] private CameraRig rig;

        private GameLoop _loop;
        private int _criticalHp = -1;
        private bool _critical;
        private float _strength;
        private float _timeLeft;
        private float _clock;
        private Vector3 _offset;

        public FxConfig Config => fx;
        public CameraRig Rig => rig;
        public bool IsShaking => _timeLeft > 0f;
        /// <summary>지금 세기 (잦아들기 전 값).</summary>
        public float Strength => IsShaking ? _strength : 0f;
        /// <summary>이번 판에서 가장 컸던 세기.</summary>
        public float PeakStrength { get; private set; }
        public float TimeLeft => _timeLeft;
        /// <summary>마지막으로 카메라에 준 오프셋 (카메라 로컬).</summary>
        public Vector3 Offset => _offset;
        public bool IsCritical => _critical;
        public int HitCount { get; private set; }

        private void OnEnable()
        {
            GameEvents.SlimeReachedHatch += OnReached;
            GameEvents.HatchHpChanged += OnHatchHp;
            GameEvents.StateChanged += OnStateChanged;
            GameEvents.GameEnded += OnGameEnded;
        }

        private void OnDisable()
        {
            GameEvents.SlimeReachedHatch -= OnReached;
            GameEvents.HatchHpChanged -= OnHatchHp;
            GameEvents.StateChanged -= OnStateChanged;
            GameEvents.GameEnded -= OnGameEnded;
            Stop();
        }

        private void Start()
        {
            if (rig == null) rig = FindAnyObjectByType<CameraRig>();
            ReadInitial();
        }

        /// <summary>흔들림을 끊고 카메라를 원위치.</summary>
        public void Stop()
        {
            _timeLeft = 0f;
            _strength = 0f;
            _offset = Vector3.zero;
            if (rig != null) rig.SetShakeOffset(Vector3.zero);
        }

        private void ReadInitial()
        {
            if (_loop == null) _loop = FindAnyObjectByType<GameLoop>();
            if (_criticalHp < 0)
            {
                GameBootstrap b = FindAnyObjectByType<GameBootstrap>();
                if (b != null && b.Data != null && b.Data.Config != null) _criticalHp = b.Data.Config.HatchCriticalHp;
            }
            if (_loop != null && _loop.IsReady) _critical = _loop.Flow.Hatch.IsCritical; // 시작값 (이벤트는 피해 때만)
        }

        private void OnReached(Game.Slimes.SlimeView view)
        {
            if (_loop != null && _loop.IsReady && _loop.Flow.State == GameState.Result) return;
            HitCount++;
            float hit = fx.shakeStrength * (_critical ? fx.shakeCriticalMultiplier : 1f);
            _strength = IsShaking ? Mathf.Max(_strength, hit) + fx.shakeStrength * fx.shakeStackRatio : hit;
            _strength = Mathf.Min(_strength, fx.shakeStrengthCap);
            _timeLeft = fx.shakeDuration; // 시간만 연장
            if (_strength > PeakStrength) PeakStrength = _strength;
        }

        private void OnHatchHp(int now, int max)
        {
            _critical = now > 0 && now <= _criticalHp; // = HatchModel.IsCritical
            if (_critical && IsShaking)
            {
                // 도달(SlimeReachedHatch) 뒤 같은 프레임에 피해가 온다 — 이번 피해로 위기가 되면 그 흔들림부터 배율
                _strength = Mathf.Min(Mathf.Max(_strength, fx.shakeStrength * fx.shakeCriticalMultiplier), fx.shakeStrengthCap);
                if (_strength > PeakStrength) PeakStrength = _strength;
            }
        }

        private void OnStateChanged(GameState state)
        {
            if (state == GameState.Paused || state == GameState.Result) Stop();
            if (state == GameState.Prep) ReadInitial();
        }

        private void OnGameEnded(ResultSummary summary)
        {
            _critical = false;
            Stop();
        }

        private void LateUpdate()
        {
            if (!IsShaking) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return; // timeScale 0 — 멈춤
            _timeLeft -= dt;
            if (_timeLeft <= 0f)
            {
                Stop();
                return;
            }
            _clock += dt * fx.shakeFrequency;
            float k = _timeLeft / fx.shakeDuration;
            float x = (Mathf.PerlinNoise(_clock, 0.37f) - 0.5f) * 2f;
            float y = (Mathf.PerlinNoise(0.71f, _clock) - 0.5f) * 2f;
            _offset = new Vector3(x, y, 0f) * (_strength * k);
            if (rig != null) rig.SetShakeOffset(_offset);
        }
    }
}
