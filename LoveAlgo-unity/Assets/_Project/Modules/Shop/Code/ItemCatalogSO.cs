using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo.Shop
{
    /// <summary>
    /// 아이템 카탈로그 ScriptableObject
    /// 인스펙터에서 아이템 데이터를 편집 가능
    /// </summary>
    [CreateAssetMenu(fileName = "ItemCatalog", menuName = "LoveAlgo/Item Catalog")]
    public class ItemCatalogSO : ScriptableObject
    {
        [SerializeField] List<ItemData> items = new();

        public IReadOnlyList<ItemData> Items => items;
    }
}
