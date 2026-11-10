using System;
using QL.Game.Flow;
using QL.FX.Motion;
using QL.Game;
using QL.Game.Slimes;
using UnityEngine;

namespace QL.FX.Combat
{
    /// <summary>
    /// 코인 비행 (CombatFx.prefab — 오버레이 캔버스 한 장, HUD 보다 위). 진짜 지갑(Wallet)은 이미 바뀌어 있고, HUD 숫자만 코인이
    /// 도착할 때 따라간다 — HUD(QL.UI HudCoin)가 <see cref="Target"/> 에 코인 아이콘을 넣고 <see cref="Delivered"/> 를 듣는다.
    /// <list type="bullet">
    /// <item>CoinChanged(delta &gt; 0) 는 그 프레임 LateUpdate 에서 짝을 찾는다: 같은 프레임 SlimeKilled 중 보상이 같은 처치가 있으면
    /// 그 점액 자리에서 코인 1개, 없으면 웨이브 클리어 보너스 → 화면 가운데에서 coinBonusCount 개 흩뿌려 차례로 (이벤트 구독 순서와 무관).</item>
    /// <item>겹침 방지: 동시에 나는 코인 ≤ coinFlyMax (= 풀 크기). 넘치면 날리지 않고 그 몫을 바로 <see cref="Delivered"/>.</item>
    /// <item>GameEnded → 날던 코인 · 짝 대기 몫을 전부 즉시 Delivered (결과 화면 전에 숫자가 맞게).</item>
    /// <item>delta &lt; 0 (소환 지출)은 여기서 다루지 않는다 — HUD 가 바로 굴린다.</item>
    /// </list>
    /// 게임 시간 (일시정지 중 멈춤). 풀 · 버퍼는 Awake 에 미리 — 웨이브 중 할당 없음.
    /// </summary>
    public sealed class CoinFlyFx : MonoBehaviour
    {
        private struct KillMark
        {
            public Vector3 World;
            public int Reward;
            public bool Used;
        }

        private const int FrameBuffer = 64;

        [SerializeField] private FxConfig fx;
        [Tooltip("비어 있으면 Awake 에 한 번 찾는다 (FxPool 은 DefaultExecutionOrder -40 이라 먼저 준비됨)")]
        [SerializeField] private FxPool pool;
        [Tooltip("코인을 놓을 오버레이 캔버스의 RectTransform (가운데 피벗)")]
        [SerializeField] private RectTransform canvasRect;
        [SerializeField] private FlyingCoin prefab;

        private FlyingCoin[] _coins;
        private readonly KillMark[] _kills = new KillMark[FrameBuffer];
        private readonly int[] _credits = new int[FrameBuffer];
        private int _killCount;
        private int _creditCount;
        private Action<FlyingCoin> _onArrived;

        /// <summary>코인이 닿을 HUD 코인 아이콘 (HudCoin 이 시작 때 넣는다). 비어 있으면 날리지 않고 바로 Delivered.</summary>
        public RectTransform Target { get; set; }
        /// <summary>(몫, 도착인가) — 도착이면 true, 상한 초과 · 판 끝 즉시 반영이면 false. 부르기 전에 <see cref="Pending"/> 이 이미 줄어 있다.</summary>
        public event Action<int, bool> Delivered;
        /// <summary>아직 표시에 더해지지 않은 코인 합 (짝 대기 + 날고 있음).</summary>
        public int Pending { get; private set; }
        public int FlyingCount { get; private set; }
        public int PoolSize => _coins != null ? _coins.Length : 0;
        public int CreatedCount { get; private set; }
        /// <summary>날린 코인 수 (누적).</summary>
        public int LaunchCount { get; private set; }
        /// <summary>상한 초과 · 목표 없음으로 날리지 않고 바로 더한 몫 합 (누적).</summary>
        public int ImmediateTotal { get; private set; }

        private void Awake()
        {
            if (pool == null) pool = FindAnyObjectByType<FxPool>(); // 프리팹은 씬의 [FX]/FxPool 을 직렬화할 수 없다 (MergeFx 와 같음)
            _onArrived = OnArrived;
            int size = Mathf.Max(1, fx.coinFlyMax);
            _coins = new FlyingCoin[size];
            for (int i = 0; i < size; i++)
            {
                FlyingCoin c = Instantiate(prefab, canvasRect);
                c.name = prefab.name + "_" + i.ToString("00");
                c.Init(fx, _onArrived);
                _coins[i] = c;
                CreatedCount++;
            }
        }

        private void OnEnable()
        {
            GameEvents.SlimeKilled += OnKilled;
            GameEvents.CoinChanged += OnCoinChanged;
            GameEvents.GameEnded += OnGameEnded;
        }

        private void OnDisable()
        {
            GameEvents.SlimeKilled -= OnKilled;
            GameEvents.CoinChanged -= OnCoinChanged;
            GameEvents.GameEnded -= OnGameEnded;
        }

        public FlyingCoin CoinAt(int i) => _coins[i];

        private void OnKilled(SlimeView view)
        {
            if (view == null || _killCount >= _kills.Length) return;
            Transform t = view.transform;
            _kills[_killCount++] = new KillMark
            {
                World = t.position + Vector3.up * (fx.damageHeadHeight * 0.5f * t.localScale.y),
                Reward = view.Reward,
                Used = false,
            };
        }

        private void OnCoinChanged(int now, int delta)
        {
            if (delta <= 0) return;
            Pending += delta;
            if (_creditCount < _credits.Length) _credits[_creditCount++] = delta;
            else Deliver(delta, false);
        }

        private void LateUpdate()
        {
            if (_creditCount > 0) Resolve();
            _killCount = 0;
        }

        /// <summary>이번 프레임 코인 획득을 처치와 짝지어 날린다.</summary>
        private void Resolve()
        {
            for (int c = 0; c < _creditCount; c++)
            {
                int amount = _credits[c];
                int kill = -1;
                for (int k = 0; k < _killCount; k++)
                {
                    if (_kills[k].Used || _kills[k].Reward != amount) continue;
                    kill = k;
                    break;
                }
                if (kill >= 0)
                {
                    _kills[kill].Used = true;
                    LaunchFromWorld(_kills[kill].World, amount);
                }
                else
                {
                    LaunchBonus(amount);
                }
            }
            _creditCount = 0;
        }

        private void LaunchFromWorld(Vector3 world, int amount)
        {
            Camera cam = pool != null ? pool.ViewCamera : Camera.main;
            Vector2 start = canvasRect.rect.center;
            if (cam != null)
            {
                Vector3 screen = cam.WorldToScreenPoint(world);
                if (screen.z > 0f) RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out start);
            }
            Vector2 pop = new Vector2(UnityEngine.Random.Range(-0.3f, 0.3f) * fx.coinPopHeight, fx.coinPopHeight);
            Launch(amount, start, pop, 0f);
        }

        private void LaunchBonus(int amount)
        {
            int n = Mathf.Clamp(fx.coinBonusCount, 1, amount);
            int each = amount / n;
            int extra = amount - each * n;
            Vector2 center = canvasRect.rect.center;
            for (int i = 0; i < n; i++)
            {
                int share = each + (i < extra ? 1 : 0);
                float a = (i + 0.5f) * (Mathf.PI * 2f / n);
                float r = fx.coinBonusScatter * (0.55f + 0.45f * ((i * 3) % n) / (float)Mathf.Max(1, n - 1)); // 반지름을 번갈아 — 원 하나로 줄 서지 않게
                Vector2 pop = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                Launch(share, center, pop, i * fx.coinBonusInterval);
            }
        }

        private void Launch(int amount, Vector2 start, Vector2 pop, float hold)
        {
            if (Target == null || FlyingCount >= _coins.Length || !TargetLocal(out Vector2 target))
            {
                Deliver(amount, false);
                return;
            }
            FlyingCoin coin = null;
            for (int i = 0; i < _coins.Length; i++)
            {
                if (_coins[i].Active) continue;
                coin = _coins[i];
                break;
            }
            if (coin == null)
            {
                Deliver(amount, false);
                return;
            }
            FlyingCount++;
            LaunchCount++;
            coin.Play(amount, start, pop, hold, target);
        }

        /// <summary>HUD 코인 아이콘 가운데 → 코인 캔버스 로컬 (둘 다 오버레이).</summary>
        private bool TargetLocal(out Vector2 local)
        {
            Vector3 world = Target.TransformPoint(Target.rect.center);
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, world);
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out local);
        }

        private void OnArrived(FlyingCoin coin)
        {
            FlyingCount--;
            Deliver(coin.Amount, true);
        }

        private void Deliver(int amount, bool arrived)
        {
            if (amount <= 0) return;
            Pending -= amount;
            if (!arrived) ImmediateTotal += amount;
            Delivered?.Invoke(amount, arrived);
        }

        /// <summary>날던 코인 · 짝 대기 몫을 전부 즉시 반영 (GameEnded).</summary>
        public void FlushAll()
        {
            int sum = 0;
            for (int i = 0; i < _coins.Length; i++)
            {
                FlyingCoin c = _coins[i];
                if (!c.Active) continue;
                sum += c.Amount;
                c.Stop();
            }
            for (int i = 0; i < _creditCount; i++) sum += _credits[i];
            _creditCount = 0;
            _killCount = 0;
            FlyingCount = 0;
            Deliver(sum, false);
        }

        private void OnGameEnded(ResultSummary summary) => FlushAll();
    }
}
