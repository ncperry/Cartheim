using System.Collections.Generic;
using System.Linq;
using Cartheim.Core;
using UnityEngine;

namespace Cartheim.Plugin
{
    // What the game says about the pieces a cart can be upgraded with: their names, recipes and which station each extends.
    internal static class GamePieces
    {
        // In panel order.
        public static readonly string[] Upgrades = { "piece_workbench" };

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
