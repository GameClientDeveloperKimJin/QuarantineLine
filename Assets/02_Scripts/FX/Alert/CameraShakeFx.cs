using QL.Core;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Flow;
using QL.Game.Stage;
using MoreMountains.Tools;
using UnityEngine;

namespace QL.FX.Alert
{
    /// <summary>
    /// 해치 피격 카메라 흔들림 (AlertFx.prefab). SlimeReachedHatch 마다 shakeDuration 동안 <see cref="CameraRig.SetShakeOffset"/> 로
    /// 카메라 로컬 x · y 를 Perlin 노이즈로 흔든다 (남은 시간 비율로 잦아듦). 위기(IsCritical)면 세기 × shakeCriticalMultiplier.
    /// 연속 피격은 세기에 shakeStackRatio 몫만 더하되 shakeStrengthCap 을 넘지 않고, 시간은 처음부터 다시 — 멀미 나게 쌓이지 않게.
    /// 게임 시간(Time.deltaTime) — timeScale 0 이면 멈춘다. 일시정지 · 결과(StateChanged Paused/Result, GameEnded)는 즉시 원위치.
    /// 끝나면 반드시 SetShakeOffset(Vector3.zero). 트윈 없음 (할당 없음).
    /// U15: <see cref="Kick"/> — 피격과 따로 도는 작은 흔들림 (작업 지시서 도장 충격). 피격 세기 · 횟수(HitCount · PeakStrength · IsShaking)에 섞이지 않고
    /// 오프셋만 더해진다. 같은 게임 시간 · 같은 멈춤 규칙.
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
        private float _kickStrength;
        private float _kickDuration;
        private float _kickLeft;

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
        /// <summary>U15 — <see cref="Kick"/> 흔들림이 도는 중.</summary>
        public bool IsKicking => _kickLeft > 0f;
        /// <summary>U15 — 지금 Kick 세기 (잦아들기 전 값).</summary>
        public float KickStrength => IsKicking ? _kickStrength : 0f;
        public int KickCount { get; private set; }

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
            _kickLeft = 0f;
            _kickStrength = 0f;
            _offset = Vector3.zero;
            if (rig != null) rig.SetShakeOffset(Vector3.zero);
        }

        /// <summary>
        /// U15 — 작은 흔들림 한 번 (strength 카메라 로컬 유닛 · duration 초, 남은 시간 비율로 잦아듦). 도는 중이면 큰 세기 · 시간 처음부터.
        /// 결과 상태 · 세기/시간 0 이하는 무시. 피격 흔들림과 오프셋만 더한다.
        /// </summary>
        public void Kick(float strength, float duration)
        {
            if (!(strength > 0f) || !(duration > 0f)) return;
            if (_loop != null && _loop.IsReady && _loop.Flow.State == GameState.Result) return;
            _kickStrength = IsKicking ? Mathf.Max(_kickStrength, strength) : strength;
            _kickDuration = duration;
            _kickLeft = duration;
            KickCount++;
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
            if (!IsShaking && !IsKicking) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return; // timeScale 0 — 멈춤
            float amp = 0f;
            if (IsShaking)
            {
                _timeLeft -= dt;
                if (_timeLeft <= 0f)
                {
                    _timeLeft = 0f;
                    _strength = 0f;
                }
                else amp += _strength * (1f - MMTween.Evaluate(1f - _timeLeft / fx.shakeDuration, fx.shakeDecayCurve)); // U17 Feel 곡선 (Linear = U06)
            }
            if (IsKicking)
            {
                _kickLeft -= dt;
                if (_kickLeft <= 0f)
                {
                    _kickLeft = 0f;
                    _kickStrength = 0f;
                }
                else amp += _kickStrength * (_kickLeft / _kickDuration);
            }
            if (!IsShaking && !IsKicking)
            {
                Stop();
                return;
            }
            _clock += dt * fx.shakeFrequency;
            float x = (Mathf.PerlinNoise(_clock, 0.37f) - 0.5f) * 2f;
            float y = (Mathf.PerlinNoise(0.71f, _clock) - 0.5f) * 2f;
            _offset = new Vector3(x, y, 0f) * amp;
            if (rig != null) rig.SetShakeOffset(_offset);
        }
    }
}
