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
        public static IReadOnlyList<string> Stations
        {
            get
            {
                if (_stations == null || _stations.Count == 0)
                    _stations = StationPieces();
                return _stations;
            }
        }
        private static List<string> _stations;

        public static readonly string[] Chests =
            { "piece_chest_wood", "piece_chest", "piece_chest_blackmetal", "piece_chest_private", "piece_chest_grausten" };

        // Further containers that go in alongside the chest, one of each.
        public static readonly string[] ExtraContainers = { "piece_chest_barrel", "piece_chest_warderobe" };

        public static IEnumerable<string> Containers => Chests.Concat(ExtraContainers);

        public static readonly string[] Beds = { "bed", "piece_bed02", "ashwood_bed" };

        public static readonly string[] Rugs =
        {
            "rug_deer", "rug_Bjorn", "rug_wolf", "rug_fur", "rug_straw", "rug_hare", "rug_asksvin", "rug_seal", "rug_moose",
            "jute_carpet", "jute_carpet_blue",
        };

        public static readonly string[] Furniture = Beds.Concat(Rugs).Append("piece_bathtub").ToArray();

        public static readonly string[] Lights =
        {
            "piece_FairylightGarland", "piece_walltorch", "piece_dvergr_lantern", "piece_dvergr_lantern_pole", "piece_Lavalantern",
            "piece_hoodedlantern", "piece_jackoturnip", "piece_snowlantern",
        };

        public const string WispTorch = "piece_groundtorch_mist";

        // Up to two beehives ride in a cart; the second rides in its own slot, "piece_beehive#2".
        public static readonly string[] BeehiveSlots = { CartUpgrades.Beehive, CartUpgrades.Beehive + "#2" };

        // The piece riding in a slot.
        public static string PieceIn(string slot) => slot.Split('#')[0];

        // The slots a piece can ride in: two for a beehive, one (its own name) for anything else.
        public static IEnumerable<string> SlotsFor(string piece) => piece == CartUpgrades.Beehive ? BeehiveSlots : new[] { piece };

        // Every piece that rides hidden in the cart as itself.
        public static readonly string[] Riders = Containers.Concat(Furniture).Concat(Lights).Append(WispTorch).Concat(BeehiveSlots).ToArray();

        // A cart roof is no single piece: it is built from this many of the game's roof pieces of its kind.
        public const int RoofPiecesPerRoof = 5;

        public static readonly string[] Roofs = { CartUpgrades.ThatchRoof, CartUpgrades.ShingleRoof, CartUpgrades.GraustenRoof };

        // The game piece each cart roof is made of, and the roof it is built over.
        private static readonly Dictionary<string, (string piece, string over)> RoofMadeOf = new Dictionary<string, (string, string)>
        {
            [CartUpgrades.ThatchRoof] = ("wood_roof", null),
            [CartUpgrades.ShingleRoof] = ("darkwood_roof", CartUpgrades.ThatchRoof),
            [CartUpgrades.GraustenRoof] = ("piece_grausten_roof_45", CartUpgrades.ShingleRoof),
        };

        // The upgrade panel's sections, in order.
        public static IEnumerable<(string title, IReadOnlyList<string> pieces)> Sections()
        {
            yield return ("Crafting", Stations);
            yield return ("Fire", new[] { "hearth" });
            yield return ("Roof (each over the last)", Roofs);
            yield return ("Chest (one per cart)", Chests);
            yield return ("More storage", ExtraContainers);
            yield return ("Furniture (one bed, one rug)", Furniture);
            yield return ("Light (one per cart)", Lights);
            yield return ("Wisp", new[] { WispTorch });
            yield return ("Bees (up to two hives)", new[] { CartUpgrades.Beehive });
        }

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
            var hammer = ObjectDB.instance.GetItemPrefab("Hammer")?.GetComponent<ItemDrop>();
            var table = hammer ? hammer.m_itemData.m_shared.m_buildPieces : null;
            if (table)
                foreach (var p in table.m_pieces.Where(p => p))
                {
                    var piece = p.GetComponent<Piece>();
                    var container = p.GetComponent<Container>();
                    var bed = p.GetComponent<Bed>();
                    var light = p.GetComponentInChildren<Light>(true);
                    var fire = p.GetComponent<Fireplace>();
                    if (!container && !bed && (!piece || piece.m_comfort <= 0) && !light && !fire && !p.name.Contains("roof"))
                        continue;
                    Debug.Log($"[Cartheim] Hammer piece {p.name} ({DisplayName(p.name)}): comfort {piece?.m_comfort} group {piece?.m_comfortGroup}" +
                              (container ? $", chest {container.m_width}x{container.m_height} {container.m_privacy}" : "") +
                              (bed ? ", bed" : "") + (fire ? $", fire (infinite fuel {fire.m_infiniteFuel})" : "") +
                              (light ? $", light {light.color} range {light.range}" : "") +
                              $": {string.Join(", ", Recipe(p.name))}");
                }
            var hearth = Prefab("hearth");
            if (hearth)
                foreach (var t in hearth.GetComponentsInChildren<Transform>(true))
                    Debug.Log($"[Cartheim] hearth part {t.name} (parent {t.parent?.name}) at {t.localPosition}: {string.Join(", ", t.GetComponents<Component>().Select(c => c is EffectArea a ? $"EffectArea {a.m_type}" : c.GetType().Name))}");
            var fireplace = hearth ? hearth.GetComponent<Fireplace>() : null;
            if (fireplace)
                Debug.Log($"[Cartheim] hearth enabledObject {fireplace.m_enabledObject?.name}, high {fireplace.m_enabledObjectHigh?.name}, low {fireplace.m_enabledObjectLow?.name}");
            var cart = Prefab("Cart");
            if (cart)
                Debug.Log($"[Cartheim] Cart prefab components: {string.Join(", ", cart.GetComponents<Component>().Select(c => c.GetType().Name))}");
        }

        public static GameObject Prefab(string piece) => ZNetScene.instance ? ZNetScene.instance.GetPrefab(piece) : null;

        public static string DisplayName(string piece)
        {
            if (RoofMadeOf.TryGetValue(piece, out var roof))
                return DisplayName(roof.piece).Split(' ')[0] + " roof";
            var p = Prefab(piece)?.GetComponent<Piece>();
            return p ? Localization.instance.Localize(p.m_name) : piece;
        }

        // The piece's own recipe as prefab names, as the hammer would charge it.
        public static List<ItemCount> Recipe(string piece)
        {
            if (RoofMadeOf.TryGetValue(piece, out var roof))
                return Recipe(roof.piece).Select(c => new ItemCount(c.Prefab, c.Count * RoofPiecesPerRoof)).ToList();
            var p = Prefab(piece)?.GetComponent<Piece>();
            if (!p)
                return new List<ItemCount>();
            return p.m_resources.Where(r => r.m_resItem && r.m_amount > 0)
                .Select(r => new ItemCount(r.m_resItem.gameObject.name, r.m_amount)).ToList();
        }

        // The station a piece extends (the forge for a forge cooler), or null for a piece that extends nothing.
        public static string Extends(string piece)
        {
            if (RoofMadeOf.TryGetValue(piece, out var roof))
                return roof.over;
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
