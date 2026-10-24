using System.Collections.Generic;
using System.Linq;
using Cartheim.Core;
using UnityEngine;

namespace Cartheim.Plugin
{
    // What the game says about the pieces a cart can be upgraded with: their names, recipes and which station each extends.
    internal static class GamePieces
    {
        // What can be built into a cart, in the hammer's order (each station followed by its extensions). Read from the game
        // once it has loaded, so new stations from a game update show up by themselves.
        public static IReadOnlyList<string> Upgrades
        {
            get
            {
                if (_upgrades == null || _upgrades.Count == 0)
                    _upgrades = StationPieces();
                return _upgrades;
            }
        }
        private static List<string> _upgrades;

        // Every hammer piece that is a crafting station or extends one, as the game has them.
        public static List<string> StationPieces()
        {
            var hammer = ObjectDB.instance?.GetItemPrefab("Hammer")?.GetComponent<ItemDrop>();
            var table = hammer ? hammer.m_itemData.m_shared.m_buildPieces : null;
            if (!table)
                return new List<string>();
            return table.m_pieces.Where(p => p && (p.GetComponent<CraftingStation>() || p.GetComponent<StationExtension>()))
                .Select(p => p.name).ToList();
        }

        // What the cart would charge for each station piece, for checking the nail tiers against the game's real recipes.
        public static void LogStationPieces()
        {
            foreach (var piece in StationPieces())
                Debug.Log($"[Cartheim] Station piece {piece} ({DisplayName(piece)}), extends {Extends(piece) ?? "nothing"}: " +
                          string.Join(", ", UpgradeCost.For(Recipe(piece))));
            var cart = Prefab("Cart");
            if (cart)
                Debug.Log($"[Cartheim] Cart prefab components: {string.Join(", ", cart.GetComponents<Component>().Select(c => c.GetType().Name))}");
        }

        public static GameObject Prefab(string piece) => ZNetScene.instance ? ZNetScene.instance.GetPrefab(piece) : null;

        public static string DisplayName(string piece)
        {
            var p = Prefab(piece)?.GetComponent<Piece>();
            return p ? Localization.instance.Localize(p.m_name) : piece;
        }

        // The piece's own recipe as prefab names, as the hammer would charge it.
        public static List<ItemCount> Recipe(string piece)
        {
            var p = Prefab(piece)?.GetComponent<Piece>();
            if (!p)
                return new List<ItemCount>();
            return p.m_resources.Where(r => r.m_resItem && r.m_amount > 0)
                .Select(r => new ItemCount(r.m_resItem.gameObject.name, r.m_amount)).ToList();
        }

        // The station a piece extends (the forge for a forge cooler), or null for a piece that extends nothing.
        public static string Extends(string piece)
        {
            var extension = Prefab(piece)?.GetComponent<StationExtension>();
            return extension && extension.m_craftingStation ? extension.m_craftingStation.gameObject.name : null;
        }

        public static ItemDrop Item(string prefab) => ObjectDB.instance?.GetItemPrefab(prefab)?.GetComponent<ItemDrop>();

        public static string ItemName(string prefab)
        {
            var item = Item(prefab);
            return item ? Localization.instance.Localize(item.m_itemData.m_shared.m_name) : prefab;
        }

        // How many of each material in the cost the player carries.
        public static Dictionary<string, int> Carried(Player player, IEnumerable<ItemCount> cost)
        {
            var carried = new Dictionary<string, int>();
            foreach (var c in cost)
            {
                var item = Item(c.Prefab);
                carried[c.Prefab] = item ? player.GetInventory().CountItems(item.m_itemData.m_shared.m_name) : 0;
            }
            return carried;
        }

        // Drops the items on the ground around a point, in full stacks, the way a broken chest spills its contents.
        public static void Drop(ItemCount items, Vector3 around)
        {
            var prefab = ObjectDB.instance?.GetItemPrefab(items.Prefab);
            var item = prefab ? prefab.GetComponent<ItemDrop>() : null;
            if (!item)
            {
                Debug.LogWarning($"[Cartheim] No item {items.Prefab} to drop");
                return;
            }
            var maxStack = Mathf.Max(1, item.m_itemData.m_shared.m_maxStackSize);
            for (var left = items.Count; left > 0; left -= maxStack)
            {
                var position = around + Vector3.up * 0.5f + Random.insideUnitSphere * 0.5f;
                var drop = Object.Instantiate(prefab, position, Quaternion.Euler(0f, Random.Range(0, 360), 0f)).GetComponent<ItemDrop>();
                ItemDrop.OnCreateNew(drop);
                drop.SetStack(Mathf.Min(left, maxStack));
            }
        }

        public static void Take(Player player, IEnumerable<ItemCount> cost)
        {
            foreach (var c in cost)
            {
                var item = Item(c.Prefab);
                if (item)
                    player.GetInventory().RemoveItem(item.m_itemData.m_shared.m_name, c.Count);
            }
        }
    }
}
