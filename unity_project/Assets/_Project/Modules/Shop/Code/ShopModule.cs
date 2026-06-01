using LoveAlgo.Contracts;
using LoveAlgo.Common;
using LoveAlgo.Simulation;
using LoveAlgo.UI;
using UnityEngine;

namespace LoveAlgo.Shop
{
    /// <summary>
    /// 상점 모듈 진입점.
    /// ShopSystem 정적 클래스를 IShop 인터페이스로 노출 + ShopUI lazy spawn.
    /// 시뮬레이션 sub-mode(Shop)로서 SimulationModule에 자기 등록.
    /// 씬 하이어라키: _Modules/ShopModule
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class ShopModule : MonoBehaviour, IShop, ISimulationSubMode
    {
        [Header("UI (씬 인스턴스 우선 / 없으면 prefab spawn)")]
        [Tooltip("씬에 미리 배치된 인스턴스. 비어있으면 prefab spawn.")]
        [SerializeField] ShopUI shopUISceneInstance;
        [SerializeField] ShopUI shopUIPrefab;

        ShopUI _shopUI;

        public ShopUI ShopUI
        {
            get
            {
                if (_shopUI != null) return _shopUI;
                if (shopUISceneInstance != null) { _shopUI = shopUISceneInstance; return _shopUI; }
                if (shopUIPrefab != null)
                {
                    var parent = UIManager.Instance?.GetGroupRoot(UIGroup.Simulation);
                    _shopUI = parent != null ? Instantiate(shopUIPrefab, parent) : Instantiate(shopUIPrefab);
                    _shopUI.name = shopUIPrefab.name;
                    _shopUI.gameObject.SetActive(false);
                    LoveAlgo.Modules.Audio.AudioManager.Instance?.BindButtonsInTransform(_shopUI.transform);
                }
                return _shopUI;
            }
        }

        public LoveAlgo.Simulation.SimulationMode Mode => LoveAlgo.Simulation.SimulationMode.Shop;

        void Awake()
        {
            Services.Register<IShop>(this);
            Services.TryGet<ISimulation>()?.RegisterSubMode(this);
        }

        void OnDestroy()
        {
            if (Services.TryGet<IShop>() == (IShop)this)
                Services.Unregister<IShop>();
        }

        // ── ISimulationSubMode ───────────────────────
        public void Enter()
        {
            var ui = ShopUI;
            if (ui != null) ui.gameObject.SetActive(true);
        }

        public void Exit()
        {
            if (_shopUI != null) _shopUI.gameObject.SetActive(false);
        }

        // ── IShop (도메인) ───────────────────────────
        public bool HasItem(string itemId) => ShopSystem.GetItemCount(itemId) > 0;
        public int GetItemCount(string itemId) => ShopSystem.GetItemCount(itemId);

        public int UseConsumable(string itemId, int currentDay = -1)
        {
            var item = ItemDatabase.Get(itemId);
            if (item == null || item.Category != ItemCategory.Consumable) return 0;
            return ShopSystem.UseConsumable(itemId, currentDay);
        }

        public bool UseSessionBuff(string itemId, int currentDay)
        {
            var item = ItemDatabase.Get(itemId);
            if (item == null || item.Category != ItemCategory.SessionBuff) return false;
            return ShopSystem.UseSessionBuff(itemId, currentDay) > 0;
        }
    }
}
