using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cartheim.Plugin
{
    // A column of icon tabs down the left of the crafting screen while at one of a cart's stations: the Cart (its
    // upgrades), each station built in, and its tent, pitched or packed up. (Its containers are all open beside the
    // crafting screen, see CartStorage; its bed and beehives are used by clicking them on the cart.)
    internal class CartTabs
    {
        private const float Size = 56f;
        private const float Gap = 4f;

        private GameObject _column;
        private CartBase _shownFor;
        private readonly List<(Object target, Button tab, UITooltip tooltip)> _shown = new List<(Object, Button, UITooltip)>();
        private bool _logged;

        public void Update()
        {
            var gui = InventoryGui.instance;
            var player = Player.m_localPlayer;
            var cart = gui && player && InventoryGui.IsVisible() ? CartCrafting.CartOf(player.GetCurrentCraftingStation()) : null;
            if (!cart || !Plugin.Enabled.Value)
            {
                if (_column)
                    _column.SetActive(false);
                return;
            }
            EnsureCreated(gui);
            PlaceBelowRepair(gui);
            var targets = TargetsOf(cart);
            if (cart != _shownFor || !targets.Select(t => t.target).SequenceEqual(_shown.Select(s => s.target)))
                Rebuild(cart, targets);
            _column.SetActive(true);
            // The station on show is the tab that can't be clicked.
            var current = player.GetCurrentCraftingStation();
            for (var i = 0; i < _shown.Count; i++)
            {
                _shown[i].tab.interactable = _shown[i].target != current;
                // Kept current: the tent's tooltip changes as it's pitched and packed up.
                _shown[i].tooltip.m_text = targets[i].name;
            }
        }

        public void Destroy()
        {
            if (_column)
                Object.Destroy(_column);
            _column = null;
            _shown.Clear();
            _shownFor = null;
        }

        private static List<(Object target, string piece, string name)> TargetsOf(CartBase cart)
        {
            var targets = new List<(Object, string, string)>();
            if (cart.UpgradeStation)
                targets.Add((cart.UpgradeStation, "Cart", "Cart: build upgrades"));
            foreach (var piece in cart.Stations)
            {
                var station = cart.Station(piece);
                if (station)
                    targets.Add((station, piece, GamePieces.DisplayName(piece)));
            }
            // Last: the tent, pitched or packed up with a click.
            if (cart.HasTent)
                targets.Add((cart, Cartheim.Core.Tent.Piece, cart.TentUp ? "Tent: click to pack it up (the cart can move again)" : "Tent: click to pitch it beside the cart (the cart then stays put)"));
            // Then the raft, launched with a click (the cart goes aboard).
            if (cart.HasRaft && cart.GetComponent<Vagon>() is Vagon vagon)
                targets.Add((vagon, Cartheim.Core.Raft.Piece, "Raft: click to launch it and put the cart aboard, to paddle it across water"));
            return targets;
        }

        private void Rebuild(CartBase cart, List<(Object target, string piece, string name)> targets)
        {
            foreach (Transform child in _column.transform)
                Object.Destroy(child.gameObject);
            _shown.Clear();
            _shownFor = cart;
            for (var i = 0; i < targets.Count; i++)
            {
                var (target, piece, name) = targets[i];
                var tab = Tab(piece, name, i, () => Click(target));
                _shown.Add((target, tab, tab.GetComponent<UITooltip>()));
            }
        }

        private static void Click(Object target)
        {
            var player = Player.m_localPlayer;
            if (!player || !target)
                return;
            switch (target)
            {
                case CraftingStation station:
                    CartCrafting.Use(station);
                    break;
                case CartBase cart:
                    cart.ToggleTent(player);
                    break;
                case Vagon vagon:
                    CartRaft.BeginLaunch(vagon.GetComponent<CartBase>());
                    break;
            }
        }

        private void EnsureCreated(InventoryGui gui)
        {
            if (_column)
                return;
            _column = new GameObject("Cartheim station tabs", typeof(RectTransform));
            var rect = (RectTransform)_column.transform;
            rect.SetParent(gui.m_crafting, false);
            // Just outside the crafting panel's left edge, from below the repair button down (see PlaceBelowRepair).
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-Gap, -Gap);
            rect.sizeDelta = new Vector2(Size, 10 * (Size + Gap));
            if (!_logged)
            {
                _logged = true;
                var corners = new Vector3[4];
                gui.m_crafting.GetWorldCorners(corners);
                Debug.Log($"[Cartheim] Crafting panel spans {corners[0]} to {corners[2]} on a {Screen.width}x{Screen.height} screen");
            }
        }


        // The repair button hangs off the crafting panel's top-left corner; the tabs start just below it, wherever the
        // game (or another mod) has put it.
        private void PlaceBelowRepair(InventoryGui gui)
        {
            var rect = (RectTransform)_column.transform;
            var top = -Gap;
            var repair = gui.m_repairPanel as RectTransform ?? (gui.m_repairButton ? (RectTransform)gui.m_repairButton.transform : null);
            if (repair && repair.gameObject.activeInHierarchy)
            {
                var corners = new Vector3[4];
                repair.GetWorldCorners(corners);
                var bottom = gui.m_crafting.InverseTransformPoint(corners[0]).y;
                top = Mathf.Min(top, bottom - gui.m_crafting.rect.yMax - Gap);
            }
            rect.anchoredPosition = new Vector2(-Gap, top);
        }
        private Button Tab(string piece, string name, int index, System.Action onClick)
        {
            var corner = new Vector2(0.5f, 1f);
            var go = GUIManager.Instance.CreateButton("", _column.transform, corner, corner, new Vector2(0f, -index * (Size + Gap) - Size / 2f), Size, Size);
            var icon = new GameObject("icon", typeof(RectTransform), typeof(Image));
            var iconRect = (RectTransform)icon.transform;
            iconRect.SetParent(go.transform, false);
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(6f, 6f);
            iconRect.offsetMax = new Vector2(-6f, -6f);
            var image = icon.GetComponent<Image>();
            image.sprite = GamePieces.Icon(piece);
            image.preserveAspect = true;
            image.raycastTarget = false;
            var tooltip = go.AddComponent<UITooltip>();
            var gameTooltip = InventoryGui.instance.m_craftButton.GetComponent<UITooltip>();
            if (gameTooltip)
                tooltip.m_tooltipPrefab = gameTooltip.m_tooltipPrefab;
            tooltip.m_text = name;
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(() => onClick());
            return button;
        }
    }
}
