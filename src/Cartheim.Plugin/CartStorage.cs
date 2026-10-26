using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Cartheim.Core;
using HarmonyLib;
using Jotunn.Managers;
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
            if (cart && cart.Rafting)
                return;
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

        // A cart whose storage just changed while the player had their inventory open beside it (an Alt+click putting
        // something into the cart or taking it out): its storage is shown next frame. Not when the game merely reloads the
        // storage from the cart's save data, which it does whenever that data changes (as it does while the cart rolls or
        // settles), so it doesn't pop up just after the inventory opens.
        private static CartBase _changed;

        private static readonly AccessTools.FieldRef<Container, bool> Loading = AccessTools.FieldRefAccess<Container, bool>("m_loading");

        [HarmonyPatch(typeof(Inventory), "Changed"), HarmonyPostfix]
        private static void ShowWhenChanged(Inventory __instance)
        {
            if (_cart || !Growing.TryGetValue(__instance, out _) || !InventoryGui.IsVisible() || !InventoryGui.instance ||
                CurrentContainer(InventoryGui.instance) || !Plugin.Enabled.Value)
                return;
            // Into one of its crates (an Alt+click files things by type): that crate is the tab shown.
            if (CartCrates.Find(__instance, out var crateCart, out var crate))
            {
                var container = crateCart.CrateContainer(crate);
                if (container && !Loading(container))
                {
                    _changed = crateCart;
                    _lastCrate = crate;
                }
                return;
            }
            var cart = CartBase.All.FirstOrDefault(c => c && c.GetComponent<Vagon>()?.m_container?.GetInventory() == __instance);
            var storage = cart ? cart.GetComponent<Vagon>().m_container : null;
            if (storage && !Loading(storage))
                _changed = cart;
        }

        public static void Update()
        {
            UpdateCrates();
            if (_changed)
            {
                var changed = _changed;
                _changed = null;
                var local = Player.m_localPlayer;
                var screen = InventoryGui.instance;
                if (!_cart && local && screen && InventoryGui.IsVisible() && !CurrentContainer(screen) &&
                    Vector3.Distance(local.transform.position, changed.transform.position) <= screen.m_autoCloseDistance)
                {
                    Debug.Log("[Cartheim] The cart's storage changed with the inventory open beside it; showing it");
                    Open(changed);
                }
            }
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
                (current && current != vagon.m_container && !(CartCrates.Find(current.GetInventory(), out var of, out _) && of == _cart)) ||
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

        // The game puts most things (materials, food) in the lowest empty slot, bottom row first. With an empty row always
        // at the bottom, the cart's own storage then filled down its first column, a new row each time; it fills from the
        // top left instead.
        [HarmonyPatch(typeof(Inventory), "TopFirst"), HarmonyPrefix]
        private static bool FillFromTheTop(Inventory __instance, ref bool __result)
        {
            if (!Growing.TryGetValue(__instance, out _))
                return true;
            __result = true;
            return false;
        }

        // The game trims a container to fit its items after loading it (whenever the cart's saved data changes); the
        // cart's own storage gets its empty row back.
        [HarmonyPatch(typeof(Container), "UpdateRows"), HarmonyPostfix]
        private static void RegrowAfterLoad(Container __instance)
        {
            var inventory = __instance.GetInventory();
            if (inventory != null)
                Grow(inventory);
        }

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
            // The cart's own storage has no end, nor its crates: they grow a row at a time as they fill.
            if (container.GetComponentInParent<Vagon>() || IsCrate(container))
            {
                if (!Growing.TryGetValue(inventory, out _))
                {
                    Growing.Add(inventory, new StrongBox<int>(height));
                    Debug.Log($"[Cartheim] Cart storage grows from {height} rows; {InventoryWidth(inventory)} wide, {InventoryHeight(inventory)} rows, {inventory.GetAllItems().Count} items: " +
                              string.Join(" ", inventory.GetAllItems().OrderBy(i => i.m_gridPos.y).ThenBy(i => i.m_gridPos.x).Select(i => $"{i.m_dropPrefab?.name}@{i.m_gridPos.x},{i.m_gridPos.y}")));
                }
                Grow(inventory);
            }
        }

        // The game's own container this one was made from, if it is a cart's storage or a container built into a cart.
        private static Container Original(Container container)
        {
            if (container.GetComponentInParent<Vagon>() || IsCrate(container))
                return GamePieces.Prefab("Cart")?.GetComponentInChildren<Container>(true);
            var nview = container.GetComponent<ZNetView>();
            var zdo = nview ? nview.GetZDO() : null;
            if (zdo == null || zdo.GetZDOID(CartRiders.CartKey) == ZDOID.None)
                return null;
            return GamePieces.Prefab(Utils.GetPrefabName(container.gameObject))?.GetComponent<Container>();
        }

        // One of a cart's crates (by the slot it rides in), sized as the cart's own storage.
        private static bool IsCrate(Container container)
        {
            var nview = container.GetComponent<ZNetView>();
            var zdo = nview ? nview.GetZDO() : null;
            return zdo != null && Crates.IsSlot(zdo.GetString(CartRiders.SlotKey, ""));
        }

        // Granted one of the cart's containers: show it in a panel of its own instead of in place of the cart's storage.
        [HarmonyPatch(typeof(Container), "RPC_OpenResponse"), HarmonyPrefix]
        private static bool ShowBeside(Container __instance, bool granted)
        {
            if (__instance == _switchingTo)
            {
                _switchingTo = null;
                if (granted && InventoryGui.instance)
                    ShowInPanel(InventoryGui.instance, __instance);
                return false;
            }
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

        // Alt+click on something in the cart (a crate, or a container built in) takes the whole stack into the player's
        // inventory, as much of it as fits.
        [HarmonyPatch(typeof(InventoryGui), "OnSelectedItem"), HarmonyPrefix]
        private static bool AltTakeFromCart(InventoryGui __instance, InventoryGrid grid, ItemDrop.ItemData item)
        {
            var alt = BepInEx.UnityInput.Current.GetKey(KeyCode.LeftAlt) || BepInEx.UnityInput.Current.GetKey(KeyCode.RightAlt);
            var player = Player.m_localPlayer;
            if (!alt || item == null || !player || grid == __instance.m_playerGrid || DragItem(__instance) != null || !Plugin.Enabled.Value)
                return true;
            var from = grid.GetInventory();
            string where;
            if (CartCrates.Find(from, out _, out var crate))
                where = $"the {crate.Name.ToLowerInvariant()} crate";
            else if (Shown.Any(s => s.grid == grid))
                where = "the cart";
            else
                return true;
            if (item.m_shared.m_questItem)
                return false;
            var name = Localization.instance.Localize(item.m_shared.m_name);
            var had = item.m_stack;
            player.GetInventory().MoveItemToThis(from, item);
            var taken = from.ContainsItem(item) ? had - item.m_stack : had;
            __instance.m_moveItemEffects.Create(__instance.transform.position, Quaternion.identity);
            var message = taken == 0 ? $"No room in your inventory for {name}" : $"{taken} {name} taken from {where}";
            Debug.Log($"[Cartheim] Alt+click in the cart: {message}");
            player.Message(MessageHud.MessageType.Center, message);
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
            // Those below it go below its crate tabs too.
            var tabs = row > 0 && _tabs && _tabs.activeSelf ? TabSize + 2 * Gap : 0f;
            rect.anchoredPosition = main.anchoredPosition + new Vector2(column * (main.rect.width + Gap), -row * (main.rect.height + Gap) - tabs);
        }

        // The crate tabs: a row of icons under the cart's storage panel, one per crate, the one on show unclickable.
        private const float TabSize = 48f;
        private static readonly AccessTools.FieldRef<InventoryGui, bool> FirstContainerUpdate =
            AccessTools.FieldRefAccess<InventoryGui, bool>("m_firstContainerUpdate");
        private static GameObject _tabs;
        private static readonly List<(Crate crate, Button tab, UITooltip tooltip)> Tabs = new List<(Crate, Button, UITooltip)>();
        // The crate last on show, shown again the next time the cart's storage opens; the one asked to open as a tab; the
        // cart whose storage is open; when its crates are next sorted.
        private static Crate _lastCrate;
        private static Container _switchingTo;
        private static CartBase _openCart;
        private static float _nextSort;
        private const float SortEvery = 1f;

        private static void UpdateCrates()
        {
            var gui = InventoryGui.instance;
            var current = gui && InventoryGui.IsVisible() ? CurrentContainer(gui) : null;
            CartBase cart = null;
            Crate crate = null;
            if (current && Plugin.Enabled.Value)
                CartCrates.Find(current.GetInventory(), out cart, out crate);
            if (!cart)
            {
                if (_tabs)
                    _tabs.SetActive(false);
                _openCart = null;
                return;
            }
            if (cart != _openCart)
            {
                _openCart = cart;
                _nextSort = 0f;
                Debug.Log($"[Cartheim] Cart storage open on its {crate.Name} crate; showing {(_lastCrate ?? crate).Name}");
                if (_lastCrate != null && _lastCrate != crate)
                    SwitchTo(cart, _lastCrate);
            }
            if (Time.time >= _nextSort)
            {
                _nextSort = Time.time + SortEvery;
                CartCrates.Sort(cart);
            }
            EnsureTabs(gui);
            _tabs.SetActive(true);
            foreach (var (each, tab, tooltip) in Tabs)
            {
                tab.interactable = each != crate;
                var stacks = cart.CrateContainer(each)?.GetInventory()?.NrOfItems() ?? 0;
                tooltip.m_text = $"{each.Name} crate: {stacks} {(stacks == 1 ? "stack" : "stacks")}";
            }
        }

        // Shows a crate in the cart's storage panel, once the game grants it (as it does opening any chest).
        private static void SwitchTo(CartBase cart, Crate crate)
        {
            var player = Player.m_localPlayer;
            var target = cart ? cart.CrateContainer(crate) : null;
            if (!player || !target)
                return;
            _lastCrate = crate;
            if (InventoryGui.instance && CurrentContainer(InventoryGui.instance) == target)
                return;
            _switchingTo = target;
            target.Interact(player, false, false);
        }

        private static void ShowInPanel(InventoryGui gui, Container container)
        {
            var old = CurrentContainer(gui);
            if (old == container)
                return;
            if (old && old.IsOwner())
                old.SetInUse(false);
            CurrentContainer(gui) = container;
            FirstContainerUpdate(gui) = true;
            Debug.Log($"[Cartheim] Showing the cart's {container.m_name}");
        }

        private static void EnsureTabs(InventoryGui gui)
        {
            if (_tabs)
                return;
            _tabs = new GameObject("Cartheim crate tabs", typeof(RectTransform));
            var rect = (RectTransform)_tabs.transform;
            rect.SetParent(gui.m_container, false);
            // Just below the storage panel's bottom edge, from its left.
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(0f, -Gap);
            rect.sizeDelta = new Vector2(Crates.All.Count * (TabSize + Gap), TabSize);
            Tabs.Clear();
            for (var i = 0; i < Crates.All.Count; i++)
            {
                var crate = Crates.All[i];
                var corner = new Vector2(0f, 0.5f);
                var go = GUIManager.Instance.CreateButton("", _tabs.transform, corner, corner, new Vector2(i * (TabSize + Gap) + TabSize / 2f, 0f), TabSize, TabSize);
                var icon = new GameObject("icon", typeof(RectTransform), typeof(Image));
                var iconRect = (RectTransform)icon.transform;
                iconRect.SetParent(go.transform, false);
                iconRect.anchorMin = Vector2.zero;
                iconRect.anchorMax = Vector2.one;
                iconRect.offsetMin = new Vector2(5f, 5f);
                iconRect.offsetMax = new Vector2(-5f, -5f);
                var image = icon.GetComponent<Image>();
                image.sprite = ObjectDB.instance?.GetItemPrefab(crate.Icon)?.GetComponent<ItemDrop>()?.m_itemData.GetIcon();
                image.preserveAspect = true;
                image.raycastTarget = false;
                var tooltip = go.AddComponent<UITooltip>();
                var gameTooltip = gui.m_craftButton.GetComponent<UITooltip>();
                if (gameTooltip)
                    tooltip.m_tooltipPrefab = gameTooltip.m_tooltipPrefab;
                tooltip.m_text = crate.Name;
                var button = go.GetComponent<Button>();
                button.onClick.AddListener(() => SwitchTo(_openCart, crate));
                Tabs.Add((crate, button, tooltip));
            }
        }

        public static void DestroyTabs()
        {
            if (_tabs)
                Object.Destroy(_tabs);
            _tabs = null;
            Tabs.Clear();
        }
    }
}
