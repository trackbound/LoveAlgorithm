using LoveAlgo.Common;
using UnityEngine;

namespace LoveAlgo.Shop
{
    /// <summary>
    /// 상점 모듈 진입점.
    /// ShopManager 정적 클래스를 IShop 인터페이스로 노출.
    /// 씬 하이어라키: _Modules/ShopModule
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class ShopModule : MonoBehaviour, IShop
    {
        void Awake() => Services.Register<IShop>(this);

        void OnDestroy()
        {
            if (Services.TryGet<IShop>() == (IShop)this)
                Services.Unregister<IShop>();
        }

        public bool HasItem(string itemId) => ShopManager.GetItemCount(itemId) > 0;
        public int GetItemCount(string itemId) => ShopManager.GetItemCount(itemId);

        public int UseConsumable(string itemId, int currentDay = -1)
        {
            var item = ItemDatabase.Get(itemId);
            if (item == null || item.Category != ItemCategory.Consumable) return 0;
            return ShopManager.UseConsumable(itemId, currentDay);
        }

        public bool UseSessionBuff(string itemId, int currentDay)
        {
            var item = ItemDatabase.Get(itemId);
            if (item == null || item.Category != ItemCategory.SessionBuff) return false;
            return ShopManager.UseSessionBuff(itemId, currentDay) > 0;
        }
    }
}
