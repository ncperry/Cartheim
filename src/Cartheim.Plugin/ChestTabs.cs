using System.Collections.Generic;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cartheim.Plugin
{
    // Tabs above the storage window while a cart with built-in containers is open: "Cart" for its own storage, then one per
    // container (chest, barrel, wardrobe), to switch between its pages.
    internal class ChestTabs
    {
        private const float TabWidth = 110f;

        private static readonly AccessTools.FieldRef<InventoryGui, Container> CurrentContainer =
            AccessTools.FieldRefAccess<InventoryGui, Container>("m_currentContainer");

        private GameObject _tabs;
        private readonly List<(Container page, Button tab)> _shown = new List<(Container, Button)>();

        public void Update()
        {
            var gui = InventoryGui.instance;
            var open = gui && InventoryGui.IsVisible() ? CurrentContainer(gui) : null;
            var pages = open ? PagesOf(open) : null;
            if (pages == null)
            {
                if (_tabs)
                    _tabs.SetActive(false);
                return;
            }
            EnsureCreated(gui);
            if (!SameAsShown(pages))
                Rebuild(pages);
            _tabs.SetActive(true);
            // The page on show is the one that can't be clicked.
            foreach (var (page, tab) in _shown)
                tab.interactable = page != open;
        }

        public void Destroy()
        {
            if (_tabs)
                Object.Destroy(_tabs);
            _tabs = null;
            _shown.Clear();
        }

        // The pages of the cart this container belongs to (its own storage first), if it has any container built in.
        private static List<Container> PagesOf(Container container)
        {
            foreach (var cart in CartBase.All)
            {
                if (!cart)
                    continue;
                var built = cart.Containers;
                var vagon = cart.GetComponent<Vagon>();
                if (built.Count == 0 || !vagon || !vagon.m_container)
                    continue;
                if (container == vagon.m_container || built.Contains(container))
                {
                    var pages = new List<Container> { vagon.m_container };
                    pages.AddRange(built);
                    return pages;
                }
            }
            return null;
        }

        private bool SameAsShown(List<Container> pages)
        {
            if (pages.Count != _shown.Count)
                return false;
            for (var i = 0; i < pages.Count; i++)
                if (pages[i] != _shown[i].page)
                    return false;
            return true;
        }

        private void Rebuild(List<Container> pages)
        {
            foreach (Transform child in _tabs.transform)
                Object.Destroy(child.gameObject);
            _shown.Clear();
            for (var i = 0; i < pages.Count; i++)
            {
                var page = pages[i];
                var name = i == 0 ? "Cart" : Localization.instance.Localize(page.m_name);
                _shown.Add((page, Tab(name, i * TabWidth, () => Switch(page))));
            }
        }

        private static void Switch(Container target)
        {
            var player = Player.m_localPlayer;
            if (!player || !target)
                return;
            Debug.Log($"[Cartheim] Switching to the cart's {target.m_name}");
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
            _tabs = new GameObject("Cartheim container tabs", typeof(RectTransform));
            var rect = (RectTransform)_tabs.transform;
            rect.SetParent(gui.m_container, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(10f, 4f);
            rect.sizeDelta = new Vector2(4 * TabWidth, 32f);
        }

        private Button Tab(string text, float x, System.Action onClick)
        {
            var corner = new Vector2(0f, 0.5f);
            var go = GUIManager.Instance.CreateButton(text, _tabs.transform, corner, corner, new Vector2(x + TabWidth / 2f, 0f), TabWidth - 4f, 30f);
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(() => onClick());
            return button;
        }
    }
}
