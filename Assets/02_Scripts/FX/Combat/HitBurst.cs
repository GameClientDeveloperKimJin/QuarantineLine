using UnityEngine;

namespace QL.FX.Combat
{
    /// <summary>U24 피격 이펙트 모양 — 프리팹마다 하나.</summary>
    public enum HitBurstStyle
    {
        /// <summary>흡입 — 소용돌이 (main) 돌며 줄어듦 + 방울 (bits) 이 드론 쪽으로.</summary>
        Suction = 0,
        /// <summary>소각 — 폭발 (main) 펑 + 불씨 (bits) 위로 + 연기 (smoke).</summary>
        Plasma = 1,
        /// <summary>냉각 — 빙하 물결 (main, 바닥) 퍼짐 + 서리 반짝 (flash) + 얼음 조각 (bits).</summary>
        Cryo = 2,
        /// <summary>빙결 풀림 — 얼음 조각 (bits) 만 깨져 흩어짐.</summary>
        Shatter = 3,
    }

    /// <summary>
    /// U24 피격 이펙트 하나 (FX_Hit*.prefab 루트 — 자식 쿼드 여러 장, 재질은 텍스처마다 하나 · 색은 MPB _BaseColor).
    /// 트윈 없음 — 주인 (<see cref="HitImpactFx"/> · <see cref="FrostFx"/>) 이 Update 에서 <see cref="Tick"/> 에 게임 시간 dt 를 준다
    /// (일시정지 = dt 0 → 그 자리에 멈춤). 점액에 붙이지 않는다 (재생 순간 위치 그대로). 배열은 Init 에 한 번 — 재생 중 할당 0.
    /// 빌보드 쿼드는 재생 순간 카메라 회전 (카메라 고정 구도), 바닥 쿼드(main, 냉각)는 눕힌다.
    /// </summary>
    public sealed class HitBurst : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Quaternion FlatRotation = Quaternion.Euler(90f, 0f, 0f);

        [SerializeField] private HitBurstStyle style;
        [Tooltip("흡입 = FX_Suck_Swirl · 소각 = FX_Plasma_Burst · 냉각 = FX_Ice_Wave (바닥) · 깨짐 = 비움")]
        [SerializeField] private Renderer main;
        [Tooltip("냉각 = FX_Frost_Burst 가운데 반짝 (그 밖은 비움)")]
        [SerializeField] private Renderer flash;
        [Tooltip("소각 = FX_Smoke_Puff (알파 블렌드 회색, 그 밖은 비움)")]
        [SerializeField] private Renderer smoke;
        [Tooltip("흡입 = FX_Goo_Droplet · 소각 = FX_Ember · 냉각 · 깨짐 = FX_Ice_Shard — 최대 개수만큼 미리 (3등급 +50% 포함)")]
        [SerializeField] private Renderer[] bits = new Renderer[0];

        private Transform _mainT, _flashT, _smokeT;
        private Transform[] _bitT;
        private Vector3[] _bitVel;   // 흡입: 방향 (단위) · 그 밖: 처음 속도 (칸/초 × 크기)
        private float[] _bitDelay;   // 흡입: 출발 늦춤 (수명 비율) · 그 밖: 회전 시작 각
        private float[] _bitSpin;
        private MaterialPropertyBlock _block;

        private FxConfig _fx;
        private Vector3 _foot, _center, _right, _up, _forward;
        private Quaternion _camRot;
        private Color _color;
        private float _scale, _elapsed, _duration, _mainYaw;
        private int _bitCount;

        public HitBurstStyle Style => style;
        public bool Active => gameObject.activeSelf;
        public int BitCapacity => bits.Length;
        /// <summary>이번 재생에 쓴 조각 수.</summary>
        public int BitCount => _bitCount;
        /// <summary>재생 순서 (주인이 매김 — 가장 오래된 것 확인용).</summary>
        public int Serial { get; internal set; }

        internal void Init()
        {
            _block = new MaterialPropertyBlock();
            _mainT = main != null ? main.transform : null;
            _flashT = flash != null ? flash.transform : null;
            _smokeT = smoke != null ? smoke.transform : null;
            int n = bits.Length;
            _bitT = new Transform[n];
            _bitVel = new Vector3[n];
            _bitDelay = new float[n];
            _bitSpin = new float[n];
            for (int i = 0; i < n; i++) _bitT[i] = bits[i].transform;
            gameObject.SetActive(false);
        }

        /// <param name="foot">점액 발 위치 (월드, 바닥)</param>
        /// <param name="scale">점액 크기 (칸 × EnemyData.scale) × 등급 배율</param>
        /// <param name="toward">드론 쪽 수평 단위 벡터 (흡입 방울 방향)</param>
        /// <param name="toCamera">카메라 쪽 단위 벡터</param>
        /// <param name="bitCount">이번 조각 수 (프리팹 개수까지)</param>
        internal void Play(Vector3 foot, float scale, Vector3 toward, Quaternion cameraRotation, Vector3 toCamera,
            int bitCount, Color color, float duration, FxConfig fx)
        {
            _fx = fx;
            _foot = foot;
            _scale = scale;
            _camRot = cameraRotation;
            _right = cameraRotation * Vector3.right;
            _up = cameraRotation * Vector3.up;
            _forward = cameraRotation * Vector3.forward;
            _center = foot + Vector3.up * (fx.hitImpactHeight * scale) + toCamera * (fx.hitImpactCameraOffset * scale);
            _color = color;
            _duration = Mathf.Max(0.01f, duration);
            _elapsed = 0f;
            _bitCount = Mathf.Clamp(bitCount, 0, bits.Length);
            _mainYaw = Random.Range(0f, 360f);

            for (int i = 0; i < bits.Length; i++)
            {
                bool on = i < _bitCount;
                if (bits[i].gameObject.activeSelf != on) bits[i].gameObject.SetActive(on);
                if (!on) continue;
                _bitSpin[i] = Random.Range(-1f, 1f);
                switch (style)
                {
                    case HitBurstStyle.Suction:
                    {
                        // 드론 쪽 ± 퍼짐 (수평), 조금씩 늦게 출발 — 하나씩 뜯겨 나감
                        float spread = _bitCount > 1 ? Mathf.Lerp(-1f, 1f, i / (float)(_bitCount - 1)) : 0f;
                        float yaw = (spread + Random.Range(-0.25f, 0.25f)) * fx.suckDropletSpread;
                        _bitVel[i] = Quaternion.AngleAxis(yaw, Vector3.up) * toward;
                        _bitDelay[i] = Random.Range(0f, fx.suckDropletStagger);
                        break;
                    }
                    case HitBurstStyle.Plasma:
                    {
                        float side = Random.Range(-1f, 1f) * fx.plasmaEmberSideSpeed;
                        float up = fx.plasmaEmberUpSpeed * Random.Range(0.7f, 1.15f);
                        _bitVel[i] = (_right * side + Vector3.up * up) * scale;
                        _bitDelay[i] = 0f;
                        break;
                    }
                    default: // Cryo · Shatter — 둥글게 바깥으로 + 위로
                    {
                        float a = (i + Random.Range(-0.3f, 0.3f)) / Mathf.Max(1, _bitCount) * Mathf.PI * 2f;
                        Vector3 outDir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        float speed = fx.cryoShardOutSpeed * Random.Range(0.75f, 1.2f);
                        float up = fx.cryoShardUpSpeed * Random.Range(0.7f, 1.2f);
                        _bitVel[i] = (outDir * speed + Vector3.up * up) * scale;
                        _bitDelay[i] = Random.Range(0f, 360f);
                        break;
                    }
                }
            }

            if (!gameObject.activeSelf) gameObject.SetActive(true);
            Apply();
        }

        /// <summary>시간 진행. 끝나면 숨기고 false.</summary>
        internal bool Tick(float dt)
        {
            if (!gameObject.activeSelf) return false;
            _elapsed += dt;
            if (_elapsed >= _duration)
            {
                Stop();
                return false;
            }
            Apply();
            return true;
        }

        internal void Stop()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        private void Apply()
        {
            float t = _elapsed / _duration; // 0 → 1
            switch (style)
            {
                case HitBurstStyle.Suction: ApplySuction(t); break;
                case HitBurstStyle.Plasma: ApplyPlasma(t); break;
                case HitBurstStyle.Cryo: ApplyCryo(t); break;
                default: ApplyBits(t, _elapsed); break;
            }
        }

        // 흡입 — 소용돌이가 돌며 가운데로 줄어들고 (가속), 방울은 드론 쪽으로 점점 빨라지며 날아감
        private void ApplySuction(float t)
        {
            FxConfig fx = _fx;
            if (_mainT != null)
            {
                float shrink = t * t * (3f - 2f * t);
                float size = Mathf.Lerp(fx.suckSwirlSizeFrom, fx.suckSwirlSizeTo, shrink) * _scale;
                float spin = fx.suckSwirlSpin * _elapsed * (1f + t); // 줄어들수록 더 빨리
                _mainT.SetPositionAndRotation(_center, _camRot * Quaternion.Euler(0f, 0f, _mainYaw + spin));
                _mainT.localScale = new Vector3(size, size, size);
                float a = fx.suckSwirlAlpha * Mathf.Clamp01(t * 8f) * (1f - t * t);
                Paint(main, _color, a);
            }

            for (int i = 0; i < _bitCount; i++)
            {
                float d = _bitDelay[i];
                float u = Mathf.Clamp01((t - d) / Mathf.Max(0.05f, 1f - d));
                float pull = u * u; // 빨려 나가듯 가속
                Vector3 dir = _bitVel[i];
                Vector3 p = _center + dir * (fx.suckDropletTravel * _scale * pull)
                            + Vector3.up * (fx.suckDropletLift * _scale * Mathf.Sin(u * Mathf.PI));
                Transform bt = _bitT[i];
                bt.SetPositionAndRotation(p, PointAlong(dir));
                float s = fx.suckDropletSize * _scale * Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(u * 4f)) * (1f - 0.4f * u)
                          * (1f + 0.6f * pull); // 날아가며 길쭉
                bt.localScale = new Vector3(s * (1f - 0.25f * pull), s, s);
                float a = u <= 0f ? 0f : 1f - u * u * u;
                Paint(bits[i], Color.white, a); // 방울은 그림 초록 그대로
            }
        }

        // 소각 — 짧게 펑 (plasmaBurstTime) + 불씨 포물선 + 연기 한 덩이 (조금 늦게, 솟으며 커짐)
        private void ApplyPlasma(float t)
        {
            FxConfig fx = _fx;
            if (_mainT != null)
            {
                float bt = Mathf.Clamp01(_elapsed / fx.plasmaBurstTime);
                bool on = bt < 1f;
                if (main.gameObject.activeSelf != on) main.gameObject.SetActive(on);
                if (on)
                {
                    float grow = 1f - (1f - bt) * (1f - bt);
                    float size = Mathf.Lerp(fx.plasmaBurstSizeFrom, fx.plasmaBurstSizeTo, grow) * _scale;
                    _mainT.SetPositionAndRotation(_center, _camRot * Quaternion.Euler(0f, 0f, _mainYaw));
                    _mainT.localScale = new Vector3(size, size, size);
                    Paint(main, _color, fx.plasmaBurstAlpha * (1f - bt * bt));
                }
            }

            if (_smokeT != null)
            {
                float st = Mathf.Clamp01((_elapsed - fx.plasmaSmokeDelay) / Mathf.Max(0.01f, _duration - fx.plasmaSmokeDelay));
                float grow = 1f - (1f - st) * (1f - st);
                float size = Mathf.Lerp(fx.plasmaSmokeSizeFrom, fx.plasmaSmokeSizeTo, grow) * _scale;
                _smokeT.SetPositionAndRotation(_center + Vector3.up * (fx.plasmaSmokeRise * _scale * grow) - _forward * 0.01f,
                    _camRot * Quaternion.Euler(0f, 0f, _mainYaw * 0.5f + 40f * st));
                _smokeT.localScale = new Vector3(size, size, size);
                Paint(smoke, fx.plasmaSmokeColor, st <= 0f ? 0f : Mathf.Clamp01(st * 6f) * (1f - st));
            }

            float g = fx.plasmaEmberGravity * _scale;
            for (int i = 0; i < _bitCount; i++)
            {
                Vector3 v0 = _bitVel[i];
                float e = _elapsed;
                Vector3 p = _center + v0 * e + Vector3.down * (0.5f * g * e * e);
                Vector3 vel = v0 + Vector3.down * (g * e);
                Transform bt = _bitT[i];
                bt.SetPositionAndRotation(p, PointAlong(vel)); // 머리 = 움직이는 쪽, 꼬리 아래
                float s = fx.plasmaEmberSize * _scale * (1f - 0.5f * t);
                bt.localScale = new Vector3(s, s, s);
                Paint(bits[i], _color, 1f - t * t);
            }
        }

        // 냉각 — 발밑 바닥에 빙하 물결 고리가 퍼짐 (핵심) + 가운데 서리 반짝 + 얼음 조각
        private void ApplyCryo(float t)
        {
            FxConfig fx = _fx;
            if (_mainT != null)
            {
                float grow = 1f - (1f - t) * (1f - t) * (1f - t);
                float size = Mathf.Lerp(fx.cryoWaveSizeFrom, fx.cryoWaveSizeTo, grow) * _scale;
                _mainT.SetPositionAndRotation(new Vector3(_foot.x, _foot.y + fx.worldFxHeight + 0.005f, _foot.z),
                    Quaternion.AngleAxis(_mainYaw, Vector3.up) * FlatRotation);
                _mainT.localScale = new Vector3(size, size, size);
                float fade = Mathf.Clamp01((t - fx.cryoWaveFadeStart) / Mathf.Max(0.01f, 1f - fx.cryoWaveFadeStart));
                Paint(main, _color, fx.cryoWaveAlpha * Mathf.Clamp01(t * 10f) * (1f - fade * fade));
            }

            if (_flashT != null)
            {
                float ft = Mathf.Clamp01(_elapsed / fx.cryoFlashTime);
                bool on = ft < 1f;
                if (flash.gameObject.activeSelf != on) flash.gameObject.SetActive(on);
                if (on)
                {
                    float size = fx.cryoFlashSize * _scale * (0.6f + 0.6f * ft);
                    _flashT.SetPositionAndRotation(_center, _camRot * Quaternion.Euler(0f, 0f, _mainYaw));
                    _flashT.localScale = new Vector3(size, size, size);
                    Paint(flash, _color, fx.cryoFlashAlpha * (1f - ft));
                }
            }

            ApplyBits(t, _elapsed);
        }

        // 얼음 조각 — 포물선 + 돌기 (냉각 · 깨짐 공통)
        private void ApplyBits(float t, float e)
        {
            FxConfig fx = _fx;
            float g = fx.cryoShardGravity * _scale;
            for (int i = 0; i < _bitCount; i++)
            {
                Vector3 p = _center + _bitVel[i] * e + Vector3.down * (0.5f * g * e * e);
                if (p.y < _foot.y + 0.02f) p.y = _foot.y + 0.02f;
                Transform bt = _bitT[i];
                float roll = _bitDelay[i] + fx.cryoShardSpin * _bitSpin[i] * e;
                bt.SetPositionAndRotation(p, _camRot * Quaternion.Euler(0f, 0f, roll));
                float s = fx.cryoShardSize * _scale * (1f - 0.35f * t);
                bt.localScale = new Vector3(s, s, s);
                Paint(bits[i], _color, 1f - t * t * t);
            }
        }

        /// <summary>빌보드 쿼드의 위(+Y, 그림 뾰족 · 머리 쪽)가 화면에서 dir 을 향하게.</summary>
        private Quaternion PointAlong(Vector3 dir)
        {
            float sx = Vector3.Dot(dir, _right);
            float sy = Vector3.Dot(dir, _up);
            if (sx * sx + sy * sy < 1e-8f) return _camRot;
            float roll = Mathf.Atan2(-sx, sy) * Mathf.Rad2Deg;
            return _camRot * Quaternion.Euler(0f, 0f, roll);
        }

        private void Paint(Renderer r, Color c, float alpha)
        {
            c.a *= Mathf.Clamp01(alpha);
            _block.SetColor(BaseColorId, c);
            r.SetPropertyBlock(_block);
        }
    }
}
