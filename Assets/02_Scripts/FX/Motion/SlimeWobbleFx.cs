using System.Collections.Generic;
using DG.Tweening;
using QL.Core;
using QL.Game;
using QL.Game.Slimes;
using UnityEngine;

namespace QL.FX.Motion
{
    /// <summary>
    /// 점액 출렁임 — 스쿼시 · 스트레치 (MotionFx.prefab). SlimeSpawned(view) → 켜진 변형 자식(Model_A/B) 의 localScale 만
    /// (가로 ↑ 세로 ↓) ↔ (가로 ↓ 세로 ↑) Yoyo. 피벗이 바닥(A04)이라 바닥에 붙은 채 출렁인다. 루트 · Y 180° 회전은 건드리지 않는다.
    /// 변이 점액은 더 느리고 묵직하게 (<see cref="FxConfig.slimeWobbleMutantAmp"/>). 위상 = 점액 풀 번호(Id) — 무작위 없음.
    /// SlimeKilled · SlimeReachedHatch → Kill 이 아니라 <b>Pause + 스케일 원복</b> — 뷰가 풀 안에서 재사용되므로 트윈도 재사용
    /// (S3 규칙 4 「풀 반납 시 Kill」 대신, U04 의 드론 · 이펙트와 같은 방식으로 통일). 처치 납작 · 흩어짐은 U05.
    /// </summary>
    public sealed class SlimeWobbleFx : MonoBehaviour
    {
        /// <summary>점액 뷰 하나의 출렁임 (뷰마다 처음 한 번 만든다).</summary>
        public sealed class Wobble
        {
            private readonly FxConfig _fx;
            private readonly Transform _model;
            private readonly Vector3 _base;
            private float _w;   // -1 … 1
            private float _amp;
            private Tween _tween;

            public SlimeView View { get; }
            public Transform Model => _model;
            public Vector3 BaseScale => _base;
            public bool IsPlaying => _tween != null && _tween.IsActive() && _tween.IsPlaying();

            public Wobble(SlimeView view, Transform model, FxConfig fx)
            {
                View = view;
                _fx = fx;
                _model = model;
                if (_model != null) _base = _model.localScale;
            }

            public void Play(SlimeType type)
            {
                if (_model == null) return;
                bool mutant = type == SlimeType.Mutant;
                _amp = mutant ? _fx.slimeWobbleMutantAmp : _fx.slimeWobbleAmp;
                float period = mutant ? _fx.slimeWobbleMutantPeriod : _fx.slimeWobblePeriod;

                if (_tween == null)
                {
                    // 한 구간(-1 → 1) 길이 1초 — 종류별 주기는 timeScale 로 (트윈을 다시 만들지 않는다)
                    _tween = DOTween.To(GetW, SetW, 1f, 1f)
                        .From(-1f)
                        .SetEase(Ease.InOutSine)
                        .SetLoops(-1, LoopType.Yoyo)
                        .SetAutoKill(false)
                        .Pause();
                }
                _tween.timeScale = 2f / period;
                int id = View != null ? View.Id : 0;
                _tween.Goto(Mathf.Repeat(id * _fx.slimeWobblePhaseStep, 1f) * 2f, true);
            }

            /// <summary>멈춤 + 원래 크기.</summary>
            public void Park()
            {
                if (_tween != null && _tween.IsActive()) _tween.Pause();
                _w = 0f;
                if (_model != null) _model.localScale = _base;
            }

            public void Kill()
            {
                if (_tween != null) _tween.Kill();
                _tween = null;
            }

            private float GetW() => _w;

            private void SetW(float w)
            {
                _w = w;
                float k = _amp * w;
                _model.localScale = new Vector3(_base.x * (1f + k), _base.y * (1f - k), _base.z * (1f + k));
            }
        }

        [SerializeField] private FxConfig fx;
        [Tooltip("켜진 변형을 찾을 자식 이름 (SlimeView.variantChildNames 와 같게). 다 없으면 출렁임 없음")]
        [SerializeField] private string[] modelChildNames = { "Model_A", "Model_B" };

        private readonly Dictionary<SlimeView, Wobble> _byView = new Dictionary<SlimeView, Wobble>();
        private readonly List<Wobble> _all = new List<Wobble>();

        /// <summary>만든 출렁임 수 (= 연출을 받은 점액 뷰 수 = 트윈 수).</summary>
        public int WobbleCount => _all.Count;

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

        private void OnDestroy()
        {
            for (int i = 0; i < _all.Count; i++) _all[i].Kill();
            _all.Clear();
            _byView.Clear();
        }

        /// <summary>이미 만든 출렁임 (없으면 null).</summary>
        public Wobble Find(SlimeView view) => view != null && _byView.TryGetValue(view, out Wobble w) ? w : null;

        private void OnSpawned(SlimeView view)
        {
            if (view == null || fx == null) return;
            if (!_byView.TryGetValue(view, out Wobble w))
            {
                w = new Wobble(view, FindActiveModel(view.transform), fx);
                _byView.Add(view, w);
                _all.Add(w);
            }
            w.Play(view.Type);
        }

        private void OnGone(SlimeView view)
        {
            Wobble w = Find(view);
            if (w != null) w.Park();
        }

        /// <summary>켜진 변형 자식 (변형은 풀 생성 때 정해져 고정 — 처음 한 번만 찾는다).</summary>
        private Transform FindActiveModel(Transform root)
        {
            Transform first = null;
            for (int i = 0; i < modelChildNames.Length; i++)
            {
                Transform c = string.IsNullOrEmpty(modelChildNames[i]) ? null : root.Find(modelChildNames[i]);
                if (c == null) continue;
                if (c.gameObject.activeSelf) return c;
                if (first == null) first = c;
            }
            return first;
        }
    }
}
