using System;
using DG.Tweening;
using UnityEngine;

namespace QL.FX.Motion
{
    /// <summary>
    /// 이펙트 풀 (FxPool.prefab, Game.unity [FX] 아래 하나). 종류(<see cref="FxKind"/>)마다 프리팹 하나 ·
    /// 크기 = <see cref="FxConfig.PoolSizeOf"/> 만큼 Awake 에 미리 만든다 — 런타임 Instantiate 없음.
    /// 모자라면 새로 만들지 않고 가장 오래 전에 재생한 것을 끊고 다시 쓴다 (종류별 원형 순서 = 오래된 순).
    /// 머지 · 소환(U04) 이 쓰고, U05 (처치 · 코인) · U06 (경고 · 피격) 이 종류를 더해 같이 쓴다.
    /// DOTween 용량도 여기서 시작 때 한 번 늘린다 (<see cref="FxConfig.tweenerCapacity"/>).
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class FxPool : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            public FxKind kind;
            public FxSprite prefab;
        }

        [SerializeField] private FxConfig fx;
        [SerializeField] private Entry[] entries;
        [Tooltip("빌보드 이펙트가 바라볼 카메라. 비우면 Camera.main 을 처음 한 번")]
        [SerializeField] private Camera viewCamera;

        private FxSprite[][] _items; // [(int)kind][slot]
        private int[] _next;         // 종류별 다음 슬롯 = 가장 오래된 것
        private int _serial;

        public FxConfig Config => fx;
        /// <summary>이펙트가 바라보는 카메라 (비어 있으면 Camera.main 을 처음 한 번).</summary>
        public Camera ViewCamera
        {
            get
            {
                if (viewCamera == null) viewCamera = Camera.main;
                return viewCamera;
            }
        }
        public bool IsReady => _items != null;
        /// <summary>Awake 에 만든 이펙트 총수 (이후 늘지 않는다).</summary>
        public int CreatedCount { get; private set; }
        /// <summary>재생 요청 총수.</summary>
        public int PlayCount => _serial;

        private void Awake()
        {
            if (fx == null)
            {
                Debug.LogError("[FxPool] FxConfig 가 비어 있다", this);
                enabled = false;
                return;
            }
            DOTween.SetTweensCapacity(fx.tweenerCapacity, fx.sequenceCapacity);

            int kinds = 0;
            for (int i = 0; i < entries.Length; i++) kinds = Mathf.Max(kinds, (int)entries[i].kind + 1);
            _items = new FxSprite[kinds][];
            _next = new int[kinds];

            for (int i = 0; i < entries.Length; i++)
            {
                Entry e = entries[i];
                if (e.prefab == null || _items[(int)e.kind] != null) continue;
                int size = Mathf.Max(1, fx.PoolSizeOf(e.kind));
                var slots = new FxSprite[size];
                for (int s = 0; s < size; s++)
                {
                    FxSprite item = Instantiate(e.prefab, transform);
                    item.name = e.prefab.name + "_" + s.ToString("00");
                    item.Init(fx);
                    slots[s] = item;
                    CreatedCount++;
                }
                _items[(int)e.kind] = slots;
            }
        }

        /// <summary>한 번 재생. 그 종류 풀이 없으면 null.</summary>
        public FxSprite Play(FxKind kind, in FxSpriteParams p)
        {
            FxSprite[] slots = SlotsOf(kind);
            if (slots == null) return null;

            int k = (int)kind;
            FxSprite item = slots[_next[k]];
            _next[k] = (_next[k] + 1) % slots.Length;

            Camera cam = ViewCamera;
            Quaternion look = cam != null ? cam.transform.rotation : Quaternion.identity;
            item.Serial = ++_serial;
            item.Play(in p, look);
            return item;
        }

        /// <summary>그 종류 풀 크기 (없으면 0).</summary>
        public int SizeOf(FxKind kind)
        {
            FxSprite[] slots = SlotsOf(kind);
            return slots != null ? slots.Length : 0;
        }

        /// <summary>그 종류에서 지금 보이는 개수.</summary>
        public int ActiveCount(FxKind kind)
        {
            FxSprite[] slots = SlotsOf(kind);
            if (slots == null) return 0;
            int n = 0;
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].gameObject.activeSelf) n++;
            return n;
        }

        /// <summary>모든 종류에서 지금 보이는 개수.</summary>
        public int ActiveCountAll()
        {
            if (_items == null) return 0;
            int n = 0;
            for (int k = 0; k < _items.Length; k++)
                if (_items[k] != null) n += ActiveCount((FxKind)k);
            return n;
        }

        /// <summary>슬롯 하나 (테스트 · 확인용).</summary>
        public FxSprite ItemAt(FxKind kind, int slot)
        {
            FxSprite[] slots = SlotsOf(kind);
            return slots != null && slot >= 0 && slot < slots.Length ? slots[slot] : null;
        }

        /// <summary>전부 즉시 끄기.</summary>
        public void StopAll()
        {
            if (_items == null) return;
            for (int k = 0; k < _items.Length; k++)
            {
                FxSprite[] slots = _items[k];
                if (slots == null) continue;
                for (int i = 0; i < slots.Length; i++) slots[i].Stop();
            }
        }

        private FxSprite[] SlotsOf(FxKind kind)
        {
            int k = (int)kind;
            return _items != null && k >= 0 && k < _items.Length ? _items[k] : null;
        }
    }
}
