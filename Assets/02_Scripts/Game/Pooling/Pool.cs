using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using Object = UnityEngine.Object;

namespace QL.Game.Pooling
{
    /// <summary>
    /// 컴포넌트 풀 (V01 6장). Instantiate 는 생성자(미리 만들기)와 크기 부족 때 Get 에서만.
    /// 쉬는 오브젝트는 비활성. 반납 때 transform 트윈을 Kill 한다.
    /// 점액(G04) · 빔 · 투사체(G05) · 데미지 숫자(U05) 가 같이 쓴다.
    /// </summary>
    public sealed class Pool<T> where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly Stack<T> _free;
        private bool _warnedGrowth;

        /// <exception cref="ArgumentNullException">prefab 이 null</exception>
        /// <exception cref="ArgumentOutOfRangeException">prewarm 이 음수</exception>
        public Pool(T prefab, Transform parent, int prewarm)
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            if (prewarm < 0) throw new ArgumentOutOfRangeException(nameof(prewarm), prewarm, "prewarm 은 0 이상");
            _prefab = prefab;
            _parent = parent;
            _free = new Stack<T>(prewarm);
            for (int i = 0; i < prewarm; i++) _free.Push(Create());
        }

        /// <summary>꺼내 쓰는 중인 개수.</summary>
        public int CountActive => CountTotal - _free.Count;

        /// <summary>이 풀이 만든 전체 개수 (런타임 확장 포함).</summary>
        public int CountTotal { get; private set; }

        /// <summary>활성화해서 돌려준다. 남은 게 없으면 하나 더 만들고 경고 1회 (크기 부족 신호).</summary>
        public T Get()
        {
            T item;
            if (_free.Count > 0)
            {
                item = _free.Pop();
            }
            else
            {
                if (!_warnedGrowth)
                {
                    _warnedGrowth = true;
                    Debug.LogWarning("[Pool] " + _prefab.name + " 풀이 모자라 런타임에 늘린다 — 미리 만드는 수를 늘릴 것", _prefab);
                }
                item = Create();
            }

            item.gameObject.SetActive(true);
            return item;
        }

        /// <summary>트윈 Kill 후 비활성. 이미 쉬는(비활성) 것이면 무시 — 두 번 반납 방지.</summary>
        public void Release(T item)
        {
            if (item == null || !item.gameObject.activeSelf) return;
            item.transform.DOKill();
            item.gameObject.SetActive(false);
            _free.Push(item);
        }

        private T Create()
        {
            T item = Object.Instantiate(_prefab, _parent);
            item.gameObject.SetActive(false);
            CountTotal++;
            return item;
        }
    }
}
