using QL.Game.Bootstrap;
using UnityEngine;

namespace QL.Game.Stage
{
    /// <summary>
    /// 고정 쿼터뷰. 해치 뒤(−z) 위에서 +z 를 내려다본다 — 해치가 화면 아래, 스폰선이 화면 위.
    /// StageBounds 꼭짓점 8개가 현재 화면비에서 여백 안에 전부 들어오는 가장 가까운 거리를 해석식으로 구한다.
    /// 화면비 · 인스펙터 값이 바뀔 때만 다시 계산한다 (할당 0).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] private GameBootstrap bootstrap;

        [Header("연출값")]
        [Tooltip("내려다보는 각도 (도)")]
        [Range(10f, 89f)]
        [SerializeField] private float pitch = 55f;
        [Tooltip("수평 회전 (도). 0 = +z 를 본다")]
        [SerializeField] private float yaw = 0f;
        [Tooltip("세로 화각 (도)")]
        [Range(10f, 100f)]
        [SerializeField] private float fieldOfView = 40f;
        [Tooltip("화면 가장자리 여백 비율 (Viewport 단위, 한쪽)")]
        [Range(0f, 0.4f)]
        [SerializeField] private float margin = 0.08f;

        private Camera _camera;
        private Bounds _bounds;
        private bool _hasBounds;

        private int _lastWidth = -1;
        private int _lastHeight = -1;
        private float _lastPitch = float.NaN;
        private float _lastYaw = float.NaN;
        private float _lastFov = float.NaN;
        private float _lastMargin = float.NaN;

        public Camera Camera => _camera;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _camera.orthographic = false;
        }

        private void Start()
        {
            if (bootstrap == null || !bootstrap.IsReady)
            {
                Debug.LogError("[CameraRig] GameBootstrap 이 없거나 준비되지 않았다", this);
                enabled = false;
                return;
            }

            _bounds = bootstrap.Space.StageBounds;
            _hasBounds = true;
            Refit();
        }

        private void LateUpdate()
        {
            if (!_hasBounds) return;
            if (Screen.width == _lastWidth && Screen.height == _lastHeight &&
                pitch == _lastPitch && yaw == _lastYaw && fieldOfView == _lastFov && margin == _lastMargin)
                return;
            Refit();
        }

        /// <summary>현재 화면비 · 각도로 거리와 화면 중심 보정을 다시 계산해 배치한다.</summary>
        public void Refit()
        {
            if (!_hasBounds) return;

            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
            _lastPitch = pitch;
            _lastYaw = yaw;
            _lastFov = fieldOfView;
            _lastMargin = margin;

            _camera.fieldOfView = fieldOfView;
            _camera.ResetAspect();
            float aspect = _camera.aspect;

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Quaternion inverse = Quaternion.Inverse(rotation);
            Vector3 center = _bounds.center;
            Vector3 extents = _bounds.extents;

            // 화면 안 조건 (카메라 공간, 꼭짓점 p, 카메라 = 중심 − forward·d + 화면 평행 이동 o):
            //   |p.x − o.x| ≤ kx (p.z + d),  |p.y − o.y| ≤ ky (p.z + d)
            // → d ≥ (max(p − k·p.z) − min(p + k·p.z)) / 2k  (축마다), o = 허용 구간의 가운데
            float usable = 1f - 2f * margin;
            float ky = Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad) * usable;
            float kx = ky * aspect;

            float xLowMax = float.NegativeInfinity, xHighMin = float.PositiveInfinity;
            float yLowMax = float.NegativeInfinity, yHighMin = float.PositiveInfinity;
            float zMin = float.PositiveInfinity;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = center + new Vector3(
                    (i & 1) == 0 ? -extents.x : extents.x,
                    (i & 2) == 0 ? -extents.y : extents.y,
                    (i & 4) == 0 ? -extents.z : extents.z);
                Vector3 p = inverse * (corner - center);
                xLowMax = Mathf.Max(xLowMax, p.x - kx * p.z);
                xHighMin = Mathf.Min(xHighMin, p.x + kx * p.z);
                yLowMax = Mathf.Max(yLowMax, p.y - ky * p.z);
                yHighMin = Mathf.Min(yHighMin, p.y + ky * p.z);
                zMin = Mathf.Min(zMin, p.z);
            }

            float distance = Mathf.Max((xLowMax - xHighMin) / (2f * kx), (yLowMax - yHighMin) / (2f * ky));
            // 모든 꼭짓점이 근평면 앞에 있게
            distance = Mathf.Max(distance, _camera.nearClipPlane - zMin);

            float offsetX = 0.5f * ((xLowMax - kx * distance) + (xHighMin + kx * distance));
            float offsetY = 0.5f * ((yLowMax - ky * distance) + (yHighMin + ky * distance));

            Vector3 position = center + rotation * new Vector3(offsetX, offsetY, -distance);
            transform.SetPositionAndRotation(position, rotation);
        }
    }
}
