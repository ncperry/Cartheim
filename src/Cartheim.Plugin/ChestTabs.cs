using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cartheim.Plugin
{
    // "Cart" and "Chest" tabs above the storage window while a cart with a chest is open, to switch between its two pages.
    internal class ChestTabs
    {
        private static readonly AccessTools.FieldRef<InventoryGui, Container> CurrentContainer =
            AccessTools.FieldRefAccess<InventoryGui, Container>("m_currentContainer");

        private GameObject _tabs;
        private Button _cartTab;
        private Button _chestTab;
        private CartBase _cart;

        public void Update()
        {
            var gui = InventoryGui.instance;
            var open = gui && InventoryGui.IsVisible() ? CurrentContainer(gui) : null;
            _cart = open ? CartOf(open) : null;
            if (!_cart)
            {
                if (_tabs)
                    _tabs.SetActive(false);
                return;
            }
            EnsureCreated(gui);
            _tabs.SetActive(true);
            // The page on show is the one that can't be clicked.
            var onChest = open == _cart.Chest;
            _cartTab.interactable = onChest;
            _chestTab.interactable = !onChest;
        }

        public void Destroy()
        {
            if (_tabs)
                Object.Destroy(_tabs);
            _tabs = null;
        }

        // The cart whose storage or chest this is, if it has a chest to switch to.
        private static CartBase CartOf(Container container)
        {
            foreach (var cart in CartBase.All)
            {
                if (!cart || !cart.Chest)
                    continue;
                var vagon = cart.GetComponent<Vagon>();
                if (container == cart.Chest || (vagon && container == vagon.m_container))
                    return cart;
            }
            return null;
        }

        private void Switch(bool toChest)
        {
            var player = Player.m_localPlayer;
            var vagon = _cart ? _cart.GetComponent<Vagon>() : null;
            var target = toChest ? _cart?.Chest : vagon?.m_container;
            if (!player || !target)
                return;
            Debug.Log($"[Cartheim] Switching to the cart's {(toChest ? "chest" : "storage")}");
            // Showing another container doesn't let go of the one on show; let go of it, or nobody else could open it.
            var current = CurrentContainer(InventoryGui.instance);
            if (current && current.IsOwner())
                current.SetInUse(false);
            target.Interact(player, false, false);
        }

        private void EnsureCreated(InventoryGui gui)
        {
            if (_tabs)
                return;
            _tabs = new GameObject("Cartheim chest tabs", typeof(RectTransform));
            var rect = (RectTransform)_tabs.transform;
            rect.SetParent(gui.m_container, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(10f, 4f);
            rect.sizeDelta = new Vector2(240f, 32f);
            _cartTab = Tab("Cart", 0f, () => Switch(toChest: false));
            _chestTab = Tab("Chest", 120f, () => Switch(toChest: true));
        }

        private Button Tab(string text, float x, System.Action onClick)
        {
            var corner = new Vector2(0f, 0.5f);
            var go = GUIManager.Instance.CreateButton(text, _tabs.transform, corner, corner, new Vector2(x + 55f, 0f), 110f, 30f);
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(() => onClick());
            return button;
        }
    }
}
