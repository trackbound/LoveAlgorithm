using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo.Shop
{
    /// <summary>
    /// 상점 아이템 노출 필터 (테스트 빌드용)
    /// 인스펙터에서 체크된 아이템만 상점에 노출
    /// ShopUI과 같은 GameObject에 부착하거나, ShopUI에서 참조
    /// </summary>
    public class ShopItemFilter : MonoBehaviour
    {
        [System.Serializable]
        public class ItemEntry
        {
            [HideInInspector] public string id;
            [HideInInspector] public int csvNo;
            public string name;
            public bool enabled = true;
        }

        [Header("체크된 아이템만 상점에 노출됩니다")]
        [SerializeField] List<ItemEntry> items = new();

        /// <summary>캐싱용 HashSet (런타임)</summary>
        HashSet<string> _enabledIds;

        /// <summary>필터가 설정되어 있는지 (항목이 0이면 필터 미사용 = 전체 표시)</summary>
        public bool HasFilter => items.Count > 0;

        void Awake()
        {
            RebuildCache();
        }

        /// <summary>해당 아이템 ID가 노출 허용되는지</summary>
        public bool IsItemEnabled(string itemId)
        {
            if (items.Count == 0) return true; // 필터 미설정 시 전체 허용
            _enabledIds ??= BuildCache();
            return _enabledIds.Contains(itemId);
        }

        /// <summary>Inspector 변경 시 캐시 갱신</summary>
        void OnValidate()
        {
            RebuildCache();
        }

        void RebuildCache()
        {
            _enabledIds = BuildCache();
        }

        HashSet<string> BuildCache()
        {
            var set = new HashSet<string>();
            foreach (var entry in items)
            {
                if (entry.enabled && !string.IsNullOrEmpty(entry.id))
                    set.Add(entry.id);
            }
            return set;
        }

        /// <summary>
        /// ItemDatabase에서 전체 아이템 목록을 가져와 엔트리 동기화
        /// 기존 체크 상태 유지, 새 아이템 추가, 삭제된 아이템 제거
        /// </summary>
        [ContextMenu("아이템 목록 동기화")]
        void SyncFromDatabase()
        {
#if UNITY_EDITOR
            var allItems = ItemDatabase.GetAll();
            var existing = new Dictionary<string, ItemEntry>();
            foreach (var entry in items)
            {
                if (!string.IsNullOrEmpty(entry.id))
                    existing[entry.id] = entry;
            }

            // iconPath 접두어 번호(CSV No.)로 정렬
            var sorted = new List<ItemData>(allItems);
            sorted.Sort((a, b) => ExtractNo(a.IconPath).CompareTo(ExtractNo(b.IconPath)));

            var newList = new List<ItemEntry>();
            foreach (var item in sorted)
            {
                int no = ExtractNo(item.IconPath);
                if (existing.TryGetValue(item.Id, out var entry))
                {
                    // 기존 엔트리 유지 (체크 상태 보존)
                    entry.name = item.Name;
                    entry.csvNo = no;
                    newList.Add(entry);
                }
                else
                {
                    // 새 아이템 추가 (기본: 비활성)
                    newList.Add(new ItemEntry
                    {
                        id = item.Id,
                        csvNo = no,
                        name = item.Name,
                        enabled = false
                    });
                }
            }

            items = newList;
            RebuildCache();
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[ShopItemFilter] 동기화 완료: {items.Count}개 아이템 ({_enabledIds.Count}개 활성)");
#endif
        }

        /// <summary>iconPath 접두어에서 CSV No. 추출 (예: "16_consume_energy_drink" → 16)</summary>
        static int ExtractNo(string iconPath)
        {
            if (string.IsNullOrEmpty(iconPath)) return int.MaxValue;
            int idx = iconPath.IndexOf('_');
            if (idx > 0 && int.TryParse(iconPath.Substring(0, idx), out int no))
                return no;
            return int.MaxValue;
        }
    }
}
