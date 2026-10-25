using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Cartheim.Core;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cartheim.Plugin
{
    // Shift+E on a cart shows all its storage at once: the cart's own in the game's container panel, and beside and below
    // it a copy of that panel for each container built in (chest, barrel, wardrobe). Each is opened as the game opens a
    // chest, so only one player has it open at a time, and let go when the inventory closes or the player walks away.
    internal static class CartStorage
    {
        private const float Gap = 8f;

        private static readonly AccessTools.FieldRef<InventoryGui, Container> CurrentContainer =
            AccessTools.FieldRefAccess<InventoryGui, Container>("m_currentContainer");
        private static readonly AccessTools.FieldRef<InventoryGui, ItemDrop.ItemData> DragItem =
            AccessTools.FieldRefAccess<InventoryGui, ItemDrop.ItemData>("m_dragItem");
        private static readonly MethodInfo OnSelectedItem =
            AccessTools.Method(typeof(InventoryGui), "OnSelectedItem") ?? throw new MissingMethodException("InventoryGui", "OnSelectedItem");
        private static readonly MethodInfo OnRightClickItem =
            AccessTools.Method(typeof(InventoryGui), "OnRightClickItem") ?? throw new MissingMethodException("InventoryGui", "OnRightClickItem");

        // The cart whose storage is on show, the containers asked for but not yet granted, and the ones on show.
        private static CartBase _cart;
        private static readonly HashSet<Container> Asked = new HashSet<Container>();
        private static readonly List<(Container container, GameObject panel, InventoryGrid grid)> Shown = new List<(Container, GameObject, InventoryGrid)>();

        public static void Open(CartBase cart)
        {
            Close();
            var player = Player.m_localPlayer;
            var vagon = cart.GetComponent<Vagon>();
            if (!player || !vagon || !vagon.m_container)
                return;
            _cart = cart;
            vagon.m_container.Interact(player, false, false);
            foreach (var container in cart.Containers)
            {
                Asked.Add(container);
                container.Interact(player, false, false);
            }
        }

        public static void Update()
        {
            if (!_cart)
            {
                if (Shown.Count > 0 || Asked.Count > 0)
                    Close();
                return;
            }
            var gui = InventoryGui.instance;
            var player = Player.m_localPlayer;
            var vagon = _cart.GetComponent<Vagon>();
            var current = gui ? CurrentContainer(gui) : null;
            if (!gui || !player || !InventoryGui.IsVisible() || !Plugin.Enabled.Value ||
                (current && current != vagon.m_container) ||
                Vector3.Distance(player.transform.position, _cart.transform.position) > gui.m_autoCloseDistance)
            {
                Close();
                return;
            }
            for (var i = 0; i < Shown.Count; i++)
            {
                var (container, panel, grid) = Shown[i];
                if (!container || !container.IsOwner())
                    continue;
                container.SetInUse(true);
                Place(gui, panel, i);
                grid.UpdateInventory(container.GetInventory(), null, DragItem(gui));
            }
        }

        public static void Close()
        {
            foreach (var (container, panel, _) in Shown)
            {
                if (container && container.IsOwner())
                    container.SetInUse(false);
                if (panel)
                    Object.Destroy(panel);
            }
            Shown.Clear();
            Asked.Clear();
            _cart = null;
        }

        private static readonly AccessTools.FieldRef<Inventory, int> InventoryWidth = AccessTools.FieldRefAccess<Inventory, int>("m_width");
        private static readonly AccessTools.FieldRef<Inventory, int> InventoryHeight = AccessTools.FieldRefAccess<Inventory, int>("m_height");

        // The cart's own storage has no end, and every container built into it holds twice the slots. Not tied to the mod switch:
        // shrinking a full chest back would hide what's in the extra rows.
        [HarmonyPatch(typeof(Container), "Awake"), HarmonyPrefix]
        private static void DoubleCartStorage(Container __instance) => Double(__instance);

        // Again once it has its inventory (made and loaded in Awake), so the cart's own storage starts growing.
        [HarmonyPatch(typeof(Container), "Awake"), HarmonyPostfix]
        private static void GrowCartStorage(Container __instance) => Double(__instance);

        // The cart's own storage, with the rows it has to begin with.
        private static readonly ConditionalWeakTable<Inventory, StrongBox<int>> Growing = new ConditionalWeakTable<Inventory, StrongBox<int>>();

        // After anything goes in or out (and after loading, which places items past the last row as saved), the cart's
        // own storage has just enough rows for its lowest item and an empty row below it.
        [HarmonyPatch(typeof(Inventory), "Changed"), HarmonyPostfix]
        private static void Grow(Inventory __instance)
        {
            if (!Growing.TryGetValue(__instance, out var baseRows))
                return;
            var items = __instance.GetAllItems();
            var lowest = items.Count == 0 ? -1 : items.Max(i => i.m_gridPos.y);
            var rows = StorageSize.GrowingRows(baseRows.Value, lowest);
            if (rows != InventoryHeight(__instance))
                Debug.Log($"[Cartheim] Cart storage now {rows} rows (lowest item in row {lowest})");
            InventoryHeight(__instance) = rows;
        }

        // Sized as doubled from the game's own piece, so doing it again (a hot reload, a second load) changes nothing.
        public static void Double(Container container)
        {
            var original = Original(container);
            if (!original)
                return;
            var (width, height) = StorageSize.Doubled(original.m_width, original.m_height);
            container.m_width = width;
            container.m_height = height;
            var inventory = container.GetInventory();
            if (inventory == null)
                return;
            if (InventoryHeight(inventory) < height)
            {
                InventoryWidth(inventory) = width;
                InventoryHeight(inventory) = height;
            }
            // The cart's own storage has no end: it grows a row at a time as it fills.
            if (container.GetComponentInParent<Vagon>())
            {
                if (!Growing.TryGetValue(inventory, out _))
                {
                    Growing.Add(inventory, new StrongBox<int>(height));
                    Debug.Log($"[Cartheim] Cart storage grows from {height} rows; has {inventory.GetAllItems().Count} items");
                }
                Grow(inventory);
            }
        }

        // The game's own container this one was made from, if it is a cart's storage or a container built into a cart.
        private static Container Original(Container container)
        {
            if (container.GetComponentInParent<Vagon>())
                return GamePieces.Prefab("Cart")?.GetComponentInChildren<Container>(true);
            var nview = container.GetComponent<ZNetView>();
            var zdo = nview ? nview.GetZDO() : null;
            if (zdo == null || zdo.GetZDOID(CartRiders.CartKey) == ZDOID.None)
                return null;
            return GamePieces.Prefab(Utils.GetPrefabName(container.gameObject))?.GetComponent<Container>();
        }

        // Granted one of the cart's containers: show it in a panel of its own instead of in place of the cart's storage.
        [HarmonyPatch(typeof(Container), "RPC_OpenResponse"), HarmonyPrefix]
        private static bool ShowBeside(Container __instance, bool granted)
        {
            if (!Asked.Remove(__instance))
                return true;
            if (granted && _cart && InventoryGui.instance)
                Show(InventoryGui.instance, __instance);
            return false;
        }

        // Ctrl+click in one of these panels moves the item into the player's inventory (the game would move it from the
        // player's inventory into the cart's storage, as it does for the panel it knows).
        [HarmonyPatch(typeof(InventoryGui), "OnSelectedItem"), HarmonyPrefix]
        private static bool MoveToPlayer(InventoryGui __instance, InventoryGrid grid, ItemDrop.ItemData item, InventoryGrid.Modifier mod)
        {
            if (mod != InventoryGrid.Modifier.Move || item == null || !Shown.Any(s => s.grid == grid))
                return true;
            var player = Player.m_localPlayer;
            if (!player || item.m_shared.m_questItem || DragItem(__instance) != null)
                return true;
            player.GetInventory().MoveItemToThis(grid.GetInventory(), item);
            __instance.m_moveItemEffects.Create(__instance.transform.position, Quaternion.identity);
            return false;
        }

        // A copy of the game's container panel, its grid emptied and wired to the inventory screen, its buttons (take all,
        // other mods' sort and stack) left out: they act on the cart's own storage.
        private static void Show(InventoryGui gui, Container container)
        {
            var panel = Object.Instantiate(gui.m_container.gameObject, gui.m_container.parent);
            panel.name = "Cartheim " + container.m_name;
            var grid = panel.GetComponentInChildren<InventoryGrid>(true);
            foreach (Transform element in grid.m_gridRoot)
                Object.Destroy(element.gameObject);
            grid.m_onSelected = (Action<InventoryGrid, ItemDrop.ItemData, Vector2i, InventoryGrid.Modifier>)
                Delegate.CreateDelegate(typeof(Action<InventoryGrid, ItemDrop.ItemData, Vector2i, InventoryGrid.Modifier>), gui, OnSelectedItem);
            grid.m_onRightClick = (Action<InventoryGrid, ItemDrop.ItemData, Vector2i>)
                Delegate.CreateDelegate(typeof(Action<InventoryGrid, ItemDrop.ItemData, Vector2i>), gui, OnRightClickItem);
            foreach (var button in panel.GetComponentsInChildren<Button>(true))
                button.gameObject.SetActive(false);
            foreach (var text in panel.GetComponentsInChildren<TMP_Text>(true))
                if (text.name == gui.m_containerName.name)
                    text.text = Localization.instance.Localize(container.GetInventory().GetName());
                else if (text.name == gui.m_containerWeight.name)
                    text.text = Mathf.CeilToInt(container.GetInventory().GetTotalWeight()).ToString();
            panel.SetActive(true);
            Shown.Add((container, panel, grid));
            Debug.Log($"[Cartheim] Showing the cart's {container.m_name} beside its storage");
        }

        // The cart's storage at the top left; the others to its right, below it, and below right.
        private static void Place(InventoryGui gui, GameObject panel, int index)
        {
            var main = gui.m_container;
            var rect = (RectTransform)panel.transform;
            var column = (index + 1) % 2;
            var row = (index + 1) / 2;
            rect.anchoredPosition = main.anchoredPosition + new Vector2(column * (main.rect.width + Gap), -row * (main.rect.height + Gap));
        }
    }
}
