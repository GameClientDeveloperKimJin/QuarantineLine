using DG.Tweening;
using UnityEngine;

namespace QL.FX.Motion
{
    /// <summary>한 번 재생할 값 — 위치 · 이동 · 크기 · 색 · 수명. 구조체라 할당 없음.</summary>
    public struct FxSpriteParams
    {
        /// <summary>시작 위치 (월드).</summary>
        public Vector3 Position;
        /// <summary>수명 동안 움직이는 양 (월드, <see cref="FxConfig.fxGrowEase"/> 로 감속).</summary>
        public Vector3 Travel;
        /// <summary>지름 시작 → 끝 (월드).</summary>
        public float SizeFrom;
        public float SizeTo;
        /// <summary>틴트 (흰 텍스처 × 이 색). 알파 = 시작 세기, 수명 끝에 0.</summary>
        public Color Color;
        /// <summary>수명 (초, 게임 시간).</summary>
        public float Duration;
    }

    /// <summary>
    /// 풀 이펙트 하나 (FX_Ring · FX_Spark · FX_Glow_Dot 프리팹 루트). 자식 쿼드 한 장 + 가산 재질 (A10 블렌딩).
    /// 트윈은 처음 한 번 만든 0→1 진행값 하나(SetAutoKill(false)) — 재생마다 값만 바꾸고 Restart (S3 규칙 4).
    /// 수명이 재생마다 달라도 트윈을 새로 만들지 않게 길이 1초짜리를 timeScale 로 늘이고 줄인다. 끝나면 비활성.
    /// billboard 면 재생 순간 카메라 쪽을 본다 (매 프레임 회전 없음 — 카메라는 고정 구도). 아니면 바닥에 눕힌 그대로.
    /// </summary>
    public sealed class FxSprite : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Renderer quad;
        [Tooltip("재생 순간 카메라 회전을 따른다 (반짝임 · 섬광). 끄면 프리팹 회전 그대로 (바닥 링)")]
        [SerializeField] private bool billboard;

        private Transform _root;
        private Tween _tween;
        private MaterialPropertyBlock _block;
        private FxSpriteParams _p;
        private Ease _growEase = Ease.OutQuad;
        private Ease _fadeEase = Ease.InQuad;
        private float _t;

        public bool IsPlaying => _tween != null && _tween.IsActive() && _tween.IsPlaying();
        /// <summary>마지막으로 칠한 색 (알파 포함, 이번 프레임 값).</summary>
        public Color CurrentColor { get; private set; }
        /// <summary>마지막 재생 값.</summary>
        public FxSpriteParams Params => _p;
        /// <summary>마지막 재생 순서 (풀이 매김 — 가장 오래된 것 찾기 확인용).</summary>
        public int Serial { get; internal set; }

        /// <summary>풀 생성 때 한 번 — 트윈 · MPB 를 만든다.</summary>
        internal void Init(FxConfig fx)
        {
            _root = transform;
            _block = new MaterialPropertyBlock();
            if (fx != null)
            {
                _growEase = fx.fxGrowEase;
                _fadeEase = fx.fxFadeEase;
            }
            _tween = DOTween.To(GetT, SetT, 1f, 1f)
                .SetEase(Ease.Linear)
                .SetAutoKill(false)
                .OnComplete(Finish)
                .Pause();
            gameObject.SetActive(false);
        }

        internal void Play(in FxSpriteParams p, Quaternion cameraRotation)
        {
            _p = p;
            if (billboard) _root.rotation = cameraRotation;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            _tween.timeScale = p.Duration > 0f ? 1f / p.Duration : 1000f;
            _tween.Restart(); // 0 → SetT(0) 로 첫 모습이 곧바로 칠해진다
            Apply(0f);
        }

        /// <summary>즉시 끝내고 숨긴다.</summary>
        internal void Stop()
        {
            if (_tween != null && _tween.IsActive()) _tween.Pause();
            Finish();
        }

        private float GetT() => _t;

        private void SetT(float t)
        {
            _t = t;
            Apply(t);
        }

        private void Apply(float t)
        {
            float grow = DOVirtual.EasedValue(0f, 1f, t, _growEase);
            float fade = DOVirtual.EasedValue(0f, 1f, t, _fadeEase);
            _root.position = _p.Position + _p.Travel * grow;
            float size = Mathf.LerpUnclamped(_p.SizeFrom, _p.SizeTo, grow);
            _root.localScale = new Vector3(size, size, size);

            Color c = _p.Color;
            c.a *= 1f - fade;
            CurrentColor = c;
            if (quad != null)
            {
                _block.SetColor(BaseColorId, c);
                quad.SetPropertyBlock(_block);
            }
        }

        private void Finish()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_tween != null) _tween.Kill();
        }
    }
}
