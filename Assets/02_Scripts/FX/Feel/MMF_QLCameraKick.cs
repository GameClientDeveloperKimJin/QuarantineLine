using MoreMountains.Feedbacks;
using QL.FX.Alert;
using UnityEngine;

namespace QL.FX.Feel
{
    /// <summary>
    /// U18b — MMF_Player 안에서 쓰는 카메라 흔들림. 카메라는 CameraRig 소유라 Feel 의 MMCameraShaker 를 쓰지 않고
    /// <see cref="CameraShakeFx.Kick"/> 를 부른다 (→ CameraRig.SetShakeOffset, S3 규칙 9). 세기 · 시간은 이 피드백 인스펙터 값.
    /// 할당 없음 (CameraShakeFx 는 초기화 때 한 번 찾는다).
    /// </summary>
    [AddComponentMenu("")]
    [System.Serializable]
    [FeedbackPath("QL/Camera Kick (CameraShakeFx)")]
    [FeedbackHelp("QL: CameraShakeFx.Kick(세기, 시간) — 메인 카메라에 MMCameraShaker 를 붙이지 않는 흔들림 (S3 규칙 9).")]
    public class MMF_QLCameraKick : MMF_Feedback
    {
        public static bool FeedbackTypeAuthorized = true;
#if UNITY_EDITOR
        public override Color FeedbackColor => MMFeedbacksInspectorColors.CameraColor;
#endif
        public override float FeedbackDuration { get => KickDuration; set => KickDuration = value; }

        [MMFInspectorGroup("QL Camera Kick", true, 20)]
        [Tooltip("카메라 로컬 유닛 (CameraShakeFx.Kick 세기)")]
        public float Strength = 0.04f;
        [Tooltip("초 (남은 시간 비율로 잦아듦)")]
        public float KickDuration = 0.12f;

        /// <summary>마지막으로 Kick 을 부른 횟수 (테스트용).</summary>
        public int KickCalls { get; private set; }

        private CameraShakeFx _shake;

        protected override void CustomInitialization(MMF_Player owner)
        {
            base.CustomInitialization(owner);
            if (_shake == null) _shake = Object.FindAnyObjectByType<CameraShakeFx>();
        }

        protected override void CustomPlayFeedback(Vector3 position, float feedbacksIntensity = 1.0f)
        {
            if (!Active || !FeedbackTypeAuthorized) return;
            if (_shake == null) return;
            _shake.Kick(Strength * feedbacksIntensity, KickDuration);
            KickCalls++;
        }
    }
}
