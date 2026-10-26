using System;
using System.Collections.Generic;
using System.Linq;
using Cartheim.Core;
using HarmonyLib;
using UnityEngine;

namespace Cartheim.Plugin
{
    // A cart's storage sorted by type into crates (see Crates): each crate but misc is a wood chest riding hidden in the
    // cart, saved by the game as any chest is; misc is the cart's own storage. The types are Valheim Inventory's, which
    // shares them; without it the cart has no crates. Items in the wrong crate move to their own whenever the cart's
    // storage is open, counted before and after; a drag into the wrong crate is refused, a Ctrl+click goes to the right one.
    [HarmonyPatch]
    internal static class CartCrates
    {
        public const string Prefab = "piece_chest_wood";

        // Read by Valheim Inventory, so its Alt+click puts things in the right crate and says which.
        private const string TakesKey = "cartheim_crate_takes";
        private const string NameKey = "cartheim_crate_name";
        private const string CategoryOfKey = "ValheimInventory.CategoryOf";

        private static readonly AccessTools.FieldRef<Inventory, string> InventoryName = AccessTools.FieldRefAccess<Inventory, string>("m_name");
        private static readonly AccessTools.FieldRef<Container, bool> Loading = AccessTools.FieldRefAccess<Container, bool>("m_loading");
        private static readonly AccessTools.FieldRef<InventoryGui, Container> CurrentContainer =
            AccessTools.FieldRefAccess<InventoryGui, Container>("m_currentContainer");
        private static readonly AccessTools.FieldRef<InventoryGui, ItemDrop.ItemData> DragItem =
            AccessTools.FieldRefAccess<InventoryGui, ItemDrop.ItemData>("m_dragItem");
        private static readonly AccessTools.FieldRef<InventoryGui, Inventory> DragInventory =
            AccessTools.FieldRefAccess<InventoryGui, Inventory>("m_dragInventory");
        private static readonly AccessTools.FieldRef<InventoryGui, GameObject> DragGo =
            AccessTools.FieldRefAccess<InventoryGui, GameObject>("m_dragGo");

        // Valheim Inventory's (prefab, item type) to category, while it is loaded.
        private static Func<string, string, string> CategoryOf => AppDomain.CurrentDomain.GetData(CategoryOfKey) as Func<string, string, string>;

        public static bool Available => CategoryOf != null;

        // The crate an item belongs in; misc for one the game can't name.
        public static Crate For(ItemDrop.ItemData item)
        {
            var categoryOf = CategoryOf;
            var prefab = item.m_dropPrefab ? item.m_dropPrefab.name : null;
            return categoryOf == null || prefab == null ? Crates.Misc : Crates.For(categoryOf(prefab, item.m_shared.m_itemType.ToString()));
        }

        private static string Name(ItemDrop.ItemData item) => Localization.instance.Localize(item.m_shared.m_name);

        // Its name on its panel, and (by its owner, in its save data) the types it takes and what it's called in messages;
        // with no crate, the marks are cleared (the cart's own storage while it has no crates takes anything).
        public static void Mark(Container container, ZDO zdo, Crate crate)
        {
            if (crate != null && container)
            {
                container.m_name = $"{crate.Name} crate";
                if (container.GetInventory() != null)
                    InventoryName(container.GetInventory()) = container.m_name;
            }
            if (zdo == null || !container || !container.IsOwner())
                return;
            var takes = crate == null ? "" : string.Join(",", crate.Categories);
            var name = crate == null ? "" : crate.InTheCart;
            if (zdo.GetString(TakesKey, "") != takes)
                zdo.Set(TakesKey, takes);
            if (zdo.GetString(NameKey, "") != name)
                zdo.Set(NameKey, name);
        }

        // The cart and crate an inventory is, if it is one of a cart's crates.
        public static bool Find(Inventory inventory, out CartBase cart, out Crate crate)
        {
            foreach (var c in CartBase.All)
            {
                if (!c || !c.HasCrates)
                    continue;
                foreach (var each in Crates.All)
                {
                    var container = c.CrateContainer(each);
                    if (container && container.GetInventory() == inventory)
                    {
                        cart = c;
                        crate = each;
                        return true;
                    }
                }
            }
            cart = null;
            crate = null;
            return false;
        }

        // Once item totals come out different, sorting stops until the game (or the mod) is reloaded.
        private static bool _halted;

        // Moves every item in the wrong crate into its own, one stack at a time, as the game moves a stack between
        // containers (a stack too big for the room left stays, in part, where it was). Only with every crate loaded, owned
        // by this player (so the moves are saved) and not loading. The item being dragged stays put.
        public static void Sort(CartBase cart)
        {
            if (_halted || !cart || !cart.HasCrates)
                return;
            var containers = Crates.All.ToDictionary(c => c, cart.CrateContainer);
            if (containers.Values.Any(c => !c || c.GetInventory() == null || !c.IsOwner() || Loading(c)))
                return;
            var dragged = InventoryGui.instance ? DragItem(InventoryGui.instance) : null;
            var moves = containers
                .SelectMany(pair => pair.Value.GetInventory().GetAllItems().Where(i => i != dragged && i.m_dropPrefab)
                    .Select(item => (item, from: pair.Value, to: For(item), at: pair.Key)))
                .Where(m => m.to != m.at)
                .ToList();
            if (moves.Count == 0)
                return;
            var before = Count(containers.Values);
            var done = new List<string>();
            foreach (var (item, from, to, _) in moves)
            {
                if (!from.GetInventory().ContainsItem(item))
                    continue;
                var stack = item.m_stack;
                containers[to].GetInventory().MoveItemToThis(from.GetInventory(), item);
                done.Add($"{stack} {item.m_dropPrefab.name} {from.m_name} -> {to.Name}");
            }
            var differences = Crates.Differences(before, Count(containers.Values));
            if (differences.Count > 0)
            {
                _halted = true;
                Debug.LogError($"[Cartheim] SORTING THE CART INTO CRATES CHANGED ITS ITEM TOTALS; sorting stopped. {string.Join("; ", differences)}. Moves: {string.Join("; ", done)}");
                Player.m_localPlayer?.Message(MessageHud.MessageType.Center, "Cart sorting stopped: item counts changed (see the log)");
                return;
            }
            Debug.Log($"[Cartheim] Sorted {done.Count} stacks of cart {cart.name} into their crates, totals unchanged: {string.Join("; ", done)}");
        }

        private static List<ItemCount> Count(IEnumerable<Container> containers) =>
            containers.SelectMany(c => c.GetInventory().GetAllItems())
                .Select(i => new ItemCount(i.m_dropPrefab ? i.m_dropPrefab.name : i.m_shared.m_name, i.m_stack))
                .ToList();

        // Dropping a dragged item into a crate that doesn't take it, or onto an item in another inventory that would swap
        // into a crate that doesn't take that: refused, saying where it belongs.
        [HarmonyPatch(typeof(InventoryGui), "OnSelectedItem"), HarmonyPrefix]
        private static bool KeepCratesSorted(InventoryGui __instance, InventoryGrid grid, ItemDrop.ItemData item, InventoryGrid.Modifier mod)
        {
            var player = Player.m_localPlayer;
            if (!player || !Plugin.Enabled.Value || !Available)
                return true;
            var into = grid.GetInventory();
            if (DragGo(__instance))
            {
                var dragged = DragItem(__instance);
                var from = DragInventory(__instance);
                if (dragged == null || from == null || from == into)
                    return true;
                if (Find(into, out _, out var crate) && For(dragged) != crate)
                    return Refuse(player, dragged, crate);
                // The item dropped on, swapping places, goes back to where the dragged one came from.
                if (item != null && item.m_shared.m_name != dragged.m_shared.m_name && Find(from, out _, out var back) && For(item) != back)
                    return Refuse(player, item, back);
                return true;
            }
            // Ctrl+click in the player's inventory with a crate open: into the crate it belongs in.
            var current = CurrentContainer(__instance);
            if (mod != InventoryGrid.Modifier.Move || item == null || grid != __instance.m_playerGrid || !current ||
                !Find(current.GetInventory(), out var cart, out var open))
                return true;
            var belongs = For(item);
            if (belongs == open)
                return true;
            var target = cart.CrateContainer(belongs);
            if (!target || !target.IsOwner() || item.m_shared.m_questItem)
            {
                player.Message(MessageHud.MessageType.Center, $"Can't reach {belongs.InTheCart} just now");
                return false;
            }
            player.RemoveEquipAction(item);
            player.UnequipItem(item);
            var stack = item.m_stack;
            target.GetInventory().MoveItemToThis(player.GetInventory(), item);
            __instance.m_moveItemEffects.Create(__instance.transform.position, Quaternion.identity);
            var message = $"{stack} {Name(item)} placed in {belongs.InTheCart}";
            Debug.Log($"[Cartheim] Ctrl+click with the {open.Name} crate open: {message}");
            player.Message(MessageHud.MessageType.Center, message);
            return false;
        }

        private static bool Refuse(Player player, ItemDrop.ItemData item, Crate crate)
        {
            var message = $"The {crate.Name.ToLowerInvariant()} crate doesn't take {Name(item)}: try {For(item).InTheCart}";
            Debug.Log($"[Cartheim] Refused: {message}");
            player.Message(MessageHud.MessageType.Center, message);
            return false;
        }
    }
}
