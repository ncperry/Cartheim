using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cartheim.Core;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Cartheim.Plugin
{
    // Beside a cart, what's in its storage counts as the player's own for building and crafting: the game sees it when it
    // checks what can be made, shows it in the costs, and takes from it (after the player's inventory) when paying.
    internal static class CartMaterials
    {
        // Within the reach of a workbench's building range: about as far as the cart's own stations let you build.
        private const float Reach = 20f;

        // The cart whose storage the player can draw on: the one whose station they're crafting at, else the nearest within
        // reach.
        public static CartBase InReach(Player player)
        {
            if (!Plugin.Enabled.Value || !player)
                return null;
            var station = player.GetCurrentCraftingStation();
            var atCart = station ? CartCrafting.CartOf(station) : null;
            if (atCart)
                return atCart;
            CartBase nearest = null;
            var best = Reach;
            foreach (var cart in CartBase.All)
            {
                if (!cart || cart.Rafting)
                    continue;
                var distance = Vector3.Distance(cart.transform.position, player.transform.position);
                if (distance <= best)
                {
                    best = distance;
                    nearest = cart;
                }
            }
            return nearest;
        }

        // The cart's storage: its own, its crates, then each container built into it.
        private static List<Container> StorageOf(CartBase cart)
        {
            var storage = new List<Container>();
            var vagon = cart.GetComponent<Vagon>();
            if (vagon && vagon.m_container)
                storage.Add(vagon.m_container);
            storage.AddRange(Crates.All.Where(c => c != Crates.Misc).Select(cart.CrateContainer));
            storage.AddRange(cart.Containers);
            return storage.Where(c => c && c.GetInventory() != null).ToList();
        }

        // The places to draw from when the local player pays: their own inventory, then the cart's storage if one is near.
        private static List<Inventory> Sources(Inventory inventory)
        {
            var sources = new List<Inventory> { inventory };
            var player = Player.m_localPlayer;
            if (!player || inventory != player.GetInventory())
                return sources;
            var cart = InReach(player);
            if (cart)
                sources.AddRange(StorageOf(cart).Select(c => c.GetInventory()));
            return sources;
        }

        // In place of the game's Inventory.CountItems where it checks costs: the local player's count includes the cart's.
        public static int CountWithCart(Inventory inventory, string name, int quality, bool matchWorldLevel) =>
            Sources(inventory).Sum(i => i.CountItems(name, quality, matchWorldLevel));

        public static bool HaveWithCart(Inventory inventory, string name, bool matchWorldLevel) =>
            Sources(inventory).Any(i => i.HaveItem(name, matchWorldLevel));

        // Takes the items from the player's inventory, and what it lacks from the cart's storage.
        public static void Take(Player player, string name, int amount, int quality = -1)
        {
            var cart = InReach(player);
            var storage = cart ? StorageOf(cart) : new List<Container>();
            var places = new List<Inventory> { player.GetInventory() }.Concat(storage.Select(c => c.GetInventory())).ToList();
            var take = MaterialTake.Split(amount, places.Select(i => i.CountItems(name, quality)).ToArray());
            for (var i = 0; i < places.Count; i++)
            {
                if (take[i] <= 0)
                    continue;
                // The cart's storage is saved by whoever owns it: the player taking from it, now.
                if (i > 0)
                {
                    var nview = storage[i - 1].GetComponentInParent<ZNetView>();
                    if (nview && nview.IsValid() && !nview.IsOwner())
                        nview.ClaimOwnership();
                }
                places[i].RemoveItem(name, take[i], quality);
            }
            if (take.Skip(1).Any(t => t > 0))
                Debug.Log($"[Cartheim] Paid {amount} {name}: {take[0]} from the inventory, {take.Skip(1).Sum()} from the cart");
        }

        private static readonly MethodInfo CountItems = AccessTools.Method(typeof(Inventory), nameof(Inventory.CountItems), new[] { typeof(string), typeof(int), typeof(bool) });
        private static readonly MethodInfo HaveItem = AccessTools.Method(typeof(Inventory), nameof(Inventory.HaveItem), new[] { typeof(string), typeof(bool) });

        // Where the game checks whether the player has what something costs, and where it shows it.
        [HarmonyPatch]
        internal static class CountTheCart
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(Player), "HaveRequirementItems");
                yield return AccessTools.Method(typeof(Player), nameof(Player.HaveRequirements), new[] { typeof(Piece), typeof(Player.RequirementMode) });
                yield return AccessTools.Method(typeof(InventoryGui), nameof(InventoryGui.SetupRequirement));
            }

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                foreach (var instruction in instructions)
                {
                    if (instruction.Calls(CountItems))
                        yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Call, AccessTools.Method(typeof(CartMaterials), nameof(CountWithCart)))
                            .MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
                    else if (instruction.Calls(HaveItem))
                        yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Call, AccessTools.Method(typeof(CartMaterials), nameof(HaveWithCart)))
                            .MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
                    else
                        yield return instruction;
                }
            }
        }

        // Paying: as the game does, but what the player's inventory lacks comes out of the cart's storage.
        [HarmonyPatch(typeof(Player), nameof(Player.ConsumeResources)), HarmonyPrefix]
        private static bool PayFromTheCart(Player __instance, Piece.Requirement[] requirements, int qualityLevel, int itemQuality, int multiplier)
        {
            if (__instance != Player.m_localPlayer || !InReach(__instance))
                return true;
            var station = __instance.GetCurrentCraftingStation();
            foreach (var requirement in requirements)
            {
                if ((station && station.m_upgrader != requirement.m_upgraderResource) || (!station && requirement.m_upgraderResource) || !requirement.m_resItem)
                    continue;
                var amount = requirement.GetAmount(qualityLevel) * multiplier;
                if (amount > 0)
                    Take(__instance, requirement.m_resItem.m_itemData.m_shared.m_name, amount, itemQuality);
            }
            return false;
        }

        private const string HaveLabel = "Cartheim have";

        // Each cost shows how many the player has, counting the cart's, small at the top right corner of its icon (the
        // amount it takes stays below the icon, as the game shows it).
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.SetupRequirement)), HarmonyPostfix]
        private static void ShowHave(Transform elementRoot, Piece.Requirement req, Player player, bool __result)
        {
            var icon = elementRoot.Find("res_icon");
            var label = icon ? icon.Find(HaveLabel)?.GetComponent<TMP_Text>() : null;
            if (!__result || !Plugin.Enabled.Value || !req.m_resItem || !player || !icon)
            {
                if (label)
                    label.gameObject.SetActive(false);
                return;
            }
            if (!label)
            {
                var amount = elementRoot.Find("res_amount")?.GetComponent<TMP_Text>();
                if (!amount)
                    return;
                label = Object.Instantiate(amount, icon, false);
                label.name = HaveLabel;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.fontSize = amount.fontSize * HaveSize;
                label.enableAutoSizing = false;
                label.alignment = TextAlignmentOptions.BottomRight;
                label.color = Color.white;
                var rect = label.rectTransform;
                // Just above the icon's top right corner.
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 0f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(60f, 20f);
            }
            label.gameObject.SetActive(true);
            label.text = CountWithCart(player.GetInventory(), req.m_resItem.m_itemData.m_shared.m_name, -1, true).ToString();
        }

        private const float HaveSize = 0.75f;
    }
}
