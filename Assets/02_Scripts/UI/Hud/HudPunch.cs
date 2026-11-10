using DG.Tweening;
using QL.FX;
using UnityEngine;

namespace QL.UI.Hud
{
    /// <summary>
    /// 숫자 한 번 톡 튀기 — Punch Scale 트윈 하나를 만들어 두고 <see cref="Play"/> 때마다 Restart (S3 규칙 4, 할당 없음).
    /// 세기 · 시간은 FxConfig. 대상 Transform 의 평소 크기는 1 이어야 한다 (Punch 는 처음 시작 때 크기로 돌아간다).
    /// </summary>
    internal sealed class HudPunch
    {
        private readonly Tween _tween;

        public HudPunch(Transform target, FxConfig fx)
        {
            _tween = target
                .DOPunchScale(Vector3.one * fx.hudPunchStrength, fx.hudPunchDuration, fx.hudPunchVibrato, fx.hudPunchElasticity)
                .SetAutoKill(false)
                .Pause();
        }

        public bool IsPlaying => _tween.IsActive() && _tween.IsPlaying();

        public void Play() => _tween.Restart();

        public void Kill() => _tween.Kill();
    }
}
