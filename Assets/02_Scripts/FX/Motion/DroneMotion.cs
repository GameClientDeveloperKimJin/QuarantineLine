using DG.Tweening;
using MoreMountains.Tools;
using QL.Game.Drones;
using UnityEngine;

namespace QL.FX.Motion
{
    /// <summary>
    /// 드론 뷰 한 대의 연출 상태 (뷰 인스턴스마다 하나, 처음 쓰일 때 한 번 만든다 — 트윈도 그때 한 번).
    /// <b>루트는 건드리지 않는다</b> (R3 DroneView 가 위치 · 크기를 매 순간 정함). 자식 <c>Model</c> 의 localPosition · localScale 만 쓴다.
    /// 세 연출이 같은 Model 을 만지므로 값은 필드에 모으고 <see cref="Apply"/> 한 곳에서 합쳐 쓴다:
    ///   localPosition = 기본 + 끌려옴 오프셋 × pull + 위 × (호버 폭 × bob)
    ///   localScale    = (기본 × scale + 기본 ⊙ punch) ⊙ (1 − squash/2, 1 + squash, 1 − squash/2)
    /// U17: 머지 스쿼시 · 스트레치 — 머지 Sequence 안 진행값 하나 → squash = 세기 × sin(반복) × (1 − Feel MMTween 감쇠 곡선). 늘어나며 커지고 납작하게 내려앉는다.
    /// 호버링(Yoyo) · 머지 Sequence · 소환 팝 = 트윈 셋, 전부 SetAutoKill(false) 로 재사용. 뷰가 풀로 돌아가면 <see cref="Park"/> (Pause + 원복).
    /// </summary>
    public sealed class DroneMotion
    {
        private readonly FxConfig _fx;
        private readonly Transform _model;
        private readonly Vector3 _basePos;
        private readonly Vector3 _baseScale;

        private float _bob;          // -1 … 1
        private float _pull;         // 1 → 0
        private Vector3 _pullLocal;  // 루트 로컬 단위 끌려옴 방향 × 거리
        private float _scale = 1f;
        private Vector3 _punch;
        private float _squash;       // 세로 늘어남 몫 (− 면 납작)

        private Tween _idle;
        private Sequence _merge;
        private Tween _pop;

        public DroneView View { get; }
        /// <summary>자식 Model (없으면 null — 그때는 아무것도 하지 않는다).</summary>
        public Transform Model => _model;
        public Vector3 BaseLocalPosition => _basePos;
        public Vector3 BaseLocalScale => _baseScale;
        public bool IsBobbing => _idle != null && _idle.IsActive() && _idle.IsPlaying();
        public bool IsMerging => _merge != null && _merge.IsActive() && _merge.IsPlaying();
        public bool IsPopping => _pop != null && _pop.IsActive() && _pop.IsPlaying();
        /// <summary>머지 Sequence 길이 (아직 없으면 0).</summary>
        public float MergeDuration => _merge != null ? _merge.Duration() : 0f;
        /// <summary>지금까지 이 뷰에 만든 트윈 수 (Sequence 1 + 안쪽 포함).</summary>
        public int TweenCount { get; private set; }

        public DroneMotion(DroneView view, string modelChildName, FxConfig fx)
        {
            View = view;
            _fx = fx;
            _model = view != null ? view.transform.Find(modelChildName) : null;
            if (_model == null) return;
            _basePos = _model.localPosition;
            _baseScale = _model.localScale;
        }

        // ── 호버링 ───────────────────────────────────────────

        /// <summary>둥실 시작 (이미 돌고 있으면 그대로). 위상 = 칸 번호 × phaseStep.</summary>
        public void Bob(int cell)
        {
            if (_model == null) return;
            if (_idle == null)
            {
                // 한 구간(-1 → 1) = 반 주기, Yoyo 로 계속
                _idle = DOTween.To(GetBob, SetBob, 1f, _fx.droneHoverPeriod * 0.5f)
                    .From(-1f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetAutoKill(false)
                    .Pause();
                TweenCount++;
            }
            if (_idle.IsPlaying()) return;
            float phase = Mathf.Repeat(cell * _fx.droneHoverPhaseStep, 1f) * _fx.droneHoverPeriod;
            _idle.Goto(phase, true);
        }

        /// <summary>둥실 멈춤 + 원래 높이 (드래그 중 · 풀 반납).</summary>
        public void StopBob()
        {
            if (_idle != null && _idle.IsActive()) _idle.Pause();
            _bob = 0f;
            Apply();
        }

        // ── 머지 ─────────────────────────────────────────────

        /// <summary>
        /// 끌려옴 + 0.8 → 1.25 → 1 + Punch. 전체 = mergeLockDuration (Sequence 는 처음 한 번 그 길이로 만든다).
        /// pullWorld = 놓은 쪽(from) 방향 × 거리 (월드). 도중이면 앞 것을 Complete 하고 처음부터.
        /// </summary>
        public void PlayMerge(Vector3 pullWorld, float total)
        {
            if (_model == null) return;
            if (_pop != null && _pop.IsActive() && _pop.IsPlaying()) _pop.Complete();
            if (_merge == null) BuildMerge(total);
            else if (_merge.IsPlaying()) _merge.Complete();

            Transform parent = _model.parent;
            _pullLocal = parent != null ? parent.InverseTransformVector(pullWorld) : pullWorld;
            _merge.Restart();
            // DOVirtual 값은 다음 갱신 때 칠해진다 — 놓은 프레임부터 시작 모습이 보이게 바로 칠한다
            _pull = 1f;
            _scale = _fx.mergeScaleFrom;
            _punch = Vector3.zero;
            _squash = 0f;
            Apply();
        }

        private void BuildMerge(float total)
        {
            float up = total * _fx.mergeScaleUpRatio;
            float down = total * _fx.mergeScaleDownRatio;
            float punch = Mathf.Max(0f, total - up - down);
            float pull = total * _fx.mergePullRatio;

            _merge = DOTween.Sequence().SetAutoKill(false).Pause();
            _merge.Insert(0f, DOVirtual.Float(1f, 0f, pull, SetPull).SetEase(Ease.OutQuad));
            _merge.Insert(0f, DOVirtual.Float(_fx.mergeScaleFrom, _fx.mergeScalePeak, up, SetScale).SetEase(_fx.mergeScaleUpEase, _fx.mergeScaleOvershoot));
            _merge.Insert(up, DOVirtual.Float(_fx.mergeScalePeak, 1f, down, SetScale).SetEase(_fx.mergeScaleDownEase, _fx.mergeScaleOvershoot));
            _merge.Insert(up + down, DOTween.Punch(GetPunch, SetPunch, Vector3.one * _fx.mergePunchStrength, punch,
                _fx.mergePunchVibrato, _fx.mergePunchElasticity));
            _merge.Insert(0f, DOVirtual.Float(0f, 1f, total, SetSquashT).SetEase(Ease.Linear)); // U17 — 곡선은 SetSquashT 에서 (Feel)
            // 비율 합이 1 보다 작아도 전체 길이는 mergeLock 그대로 (끝에 빈 구간)
            if (_merge.Duration() < total) _merge.AppendInterval(total - _merge.Duration());
            _merge.OnComplete(ResetShape);
            TweenCount += 6;
        }

        private void ResetShape()
        {
            _squash = 0f;
            _pull = 0f;
            _scale = 1f;
            _punch = Vector3.zero;
            Apply();
        }

        // ── 소환 팝 ──────────────────────────────────────────

        /// <summary>Model 0 → 1.1 → 1 (OutBack).</summary>
        public void PlayPop()
        {
            if (_model == null) return;
            if (_merge != null && _merge.IsActive() && _merge.IsPlaying()) _merge.Complete();
            if (_pop == null)
            {
                _pop = DOVirtual.Float(0f, 1f, _fx.spawnPopDuration, SetScale)
                    .SetEase(Ease.OutBack, _fx.spawnPopOvershoot)
                    .SetAutoKill(false)
                    .OnComplete(ResetShape)
                    .Pause();
                TweenCount++;
            }
            _pop.Restart();
            SetScale(0f); // 소환 프레임부터 0 (다음 갱신부터 OutBack)
        }

        // ── 반납 ─────────────────────────────────────────────

        /// <summary>뷰가 풀로 돌아갔다 — 트윈 전부 멈추고 Model 을 원래대로 (Kill 아님 — 뷰와 함께 재사용).</summary>
        public void Park()
        {
            if (_model == null) return;
            if (_idle != null && _idle.IsActive()) _idle.Pause();
            if (_merge != null && _merge.IsActive() && _merge.IsPlaying()) _merge.Complete();
            if (_pop != null && _pop.IsActive() && _pop.IsPlaying()) _pop.Complete();
            _bob = 0f;
            ResetShape();
        }

        public void Kill()
        {
            if (_idle != null) _idle.Kill();
            if (_merge != null) _merge.Kill();
            if (_pop != null) _pop.Kill();
            _idle = null;
            _merge = null;
            _pop = null;
        }

        // ── 값 → Model ───────────────────────────────────────

        private float GetBob() => _bob;
        private void SetBob(float v) { _bob = v; Apply(); }
        private void SetPull(float v) { _pull = v; Apply(); }
        private void SetScale(float v) { _scale = v; Apply(); }
        private Vector3 GetPunch() => _punch;
        private void SetPunch(Vector3 v) { _punch = v; Apply(); }

        /// <summary>머지 진행 k (0~1) → 스쿼시 몫. Feel MMTween.Evaluate 는 정적 계산 (할당 없음).</summary>
        private void SetSquashT(float k)
        {
            float a = _fx.mergeSquashAmount;
            _squash = a > 0f
                ? a * Mathf.Sin(k * _fx.mergeSquashCycles * Mathf.PI * 2f) * (1f - MMTween.Evaluate(Mathf.Clamp01(k), _fx.mergeSquashDecay))
                : 0f;
            Apply();
        }

        private void Apply()
        {
            if (_model == null) return;
            Vector3 p = _basePos + _pullLocal * _pull;
            p.y += _fx.droneHoverAmp * _bob;
            _model.localPosition = p;
            Vector3 k = _baseScale * _scale + Vector3.Scale(_baseScale, _punch);
            if (_squash != 0f)
            {
                float side = 1f - _squash * 0.5f;
                k.x *= side;
                k.y *= 1f + _squash;
                k.z *= side;
            }
            _model.localScale = k;
        }
    }
}
