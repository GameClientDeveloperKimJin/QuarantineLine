using DG.Tweening;
using MoreMountains.Tools;
using QL.FX.Motion;
using UnityEngine;

namespace QL.FX.Combat
{
    /// <summary>
    /// 처치 「대역」 하나 (FX_SlimeDouble.prefab — MeshFilter + MeshRenderer 만). 죽은 점액은 같은 프레임에 풀로 돌아가 곧 재사용되므로
    /// 그 점액 대신 이것이 켜진 Model 의 메시 · 재질 · 월드 자리를 넘겨받아 납작해지고(y 1 → deathSquashY, xz 1 → deathSquashXZ) 사라진다.
    /// 메시 피벗이 바닥이라 바닥에 붙은 채 납작해진다. 재질은 점액 것 그대로(sharedMaterial, 투명 Lit) — 알파만 MPB 로.
    /// F01: 첫 <see cref="FxConfig.deathFlashDuration"/> 동안 히트 플래시와 같은 흰색 (_BaseMap 흰 텍스처) — 처치 타격도 번쩍이게.
    /// U17: 납작해지기 전 <see cref="FxConfig.deathStretchDuration"/> 동안 위로 쭉 (예비 동작, Feel MMTween 곡선) — 납작은 남은 시간에 끝나 끝 시점(deathSquashDuration)은 그대로.
    /// 트윈 = 0→1 진행값 하나 (SetAutoKill(false) + Restart, 수명은 timeScale). puddleDelay 가 지나면 풀에 Puddle 을 한 번 부른다.
    /// </summary>
    public sealed class SlimeDouble : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        [SerializeField] private MeshFilter meshFilter;
        [SerializeField] private MeshRenderer meshRenderer;

        private Transform _root;
        private Tween _tween;
        private MaterialPropertyBlock _block;
        private FxConfig _fx;
        private FxPool _pool;
        private Vector3 _baseScale;
        private Color _baseColor;
        private Texture _baseMap;
        private FxSpriteParams _puddle;
        private bool _puddlePlayed;
        private float _total;
        private float _t;

        public bool Active => gameObject.activeSelf;
        public Mesh Mesh => meshFilter.sharedMesh;
        public Material Material => meshRenderer.sharedMaterial;
        public Vector3 BaseScale => _baseScale;
        /// <summary>마지막으로 칠한 알파.</summary>
        public float Alpha { get; private set; }
        /// <summary>전체 수명 (초).</summary>
        public float TotalDuration => _total;

        internal void Init(FxConfig fx, FxPool pool)
        {
            _fx = fx;
            _pool = pool;
            _root = transform;
            _block = new MaterialPropertyBlock();
            _tween = DOTween.To(GetT, SetT, 1f, 1f)
                .SetEase(Ease.Linear)
                .SetAutoKill(false)
                .OnComplete(Finish)
                .Pause();
            gameObject.SetActive(false);
        }

        /// <summary>점액 Model 렌더러의 메시 · 재질 · 월드 자리 그대로 재생 (참조 대입 — 할당 없음).</summary>
        internal void Play(Mesh mesh, Material material, Vector3 position, Quaternion rotation, Vector3 scale, in FxSpriteParams puddle)
        {
            meshFilter.sharedMesh = mesh;
            meshRenderer.sharedMaterial = material;
            _baseColor = material != null && material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId) : Color.white;
            _baseMap = material != null && material.HasProperty(BaseMapId) ? material.GetTexture(BaseMapId) : null;
            _root.SetPositionAndRotation(position, rotation);
            _baseScale = scale;
            _puddle = puddle;
            _puddlePlayed = false;
            _total = Mathf.Max(_fx.deathSquashDuration, _fx.deathFadeDelay + _fx.deathFadeDuration, _fx.puddleDelay);
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            _tween.timeScale = 1f / _total;
            _tween.Restart();
            Apply(0f);
        }

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
            float elapsed = t * _total;
            float pre = Mathf.Min(_fx.deathStretchDuration, _fx.deathSquashDuration * 0.5f);
            float xz, y;
            if (elapsed < pre)
            {
                // 0 → 꼭대기 (Feel 곡선) — 늘어난 모양에서 바로 납작으로 넘어간다
                float u = MMTween.Evaluate(elapsed / pre, _fx.deathStretchCurve);
                xz = Mathf.LerpUnclamped(1f, _fx.deathStretchXZ, u);
                y = Mathf.LerpUnclamped(1f, _fx.deathStretchY, u);
            }
            else
            {
                float s = DOVirtual.EasedValue(0f, 1f, Mathf.Clamp01((elapsed - pre) / (_fx.deathSquashDuration - pre)), _fx.deathSquashEase);
                float fromXZ = pre > 0f ? _fx.deathStretchXZ : 1f;
                float fromY = pre > 0f ? _fx.deathStretchY : 1f;
                xz = Mathf.LerpUnclamped(fromXZ, _fx.deathSquashXZ, s);
                y = Mathf.LerpUnclamped(fromY, _fx.deathSquashY, s);
            }
            _root.localScale = new Vector3(_baseScale.x * xz, _baseScale.y * y, _baseScale.z * xz);

            float fade = Mathf.Clamp01((elapsed - _fx.deathFadeDelay) / _fx.deathFadeDuration);
            Alpha = 1f - fade;
            Color c = _baseColor;
            // F01: 처치 타격의 히트 플래시 (점액은 같은 프레임에 풀로 돌아가 HitFlashFx 가 못 그림) — 대역 첫 deathFlashDuration 동안 흰색
            if (elapsed < _fx.deathFlashDuration)
            {
                c = _fx.hitFlashColor * _fx.hitFlashIntensity;
                c.a = _baseColor.a;
                _block.SetTexture(BaseMapId, Texture2D.whiteTexture);
            }
            else
            {
                _block.SetTexture(BaseMapId, _baseMap != null ? _baseMap : Texture2D.whiteTexture);
            }
            c.a *= Alpha;
            _block.SetColor(BaseColorId, c);
            meshRenderer.SetPropertyBlock(_block);

            if (!_puddlePlayed && elapsed >= _fx.puddleDelay)
            {
                _puddlePlayed = true;
                if (_pool != null) _pool.Play(FxKind.Puddle, in _puddle);
            }
        }

        private void Finish()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            _tween?.Kill();
        }
    }
}
