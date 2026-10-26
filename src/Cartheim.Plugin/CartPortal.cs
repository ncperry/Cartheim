using System.Collections.Generic;
using System.Linq;
using Cartheim.Core;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace Cartheim.Plugin
{
    // A portal built onto a cart: its tab beside the cart's stations takes the player to the world's spawn, and nowhere
    // else; Shift+E on the sacrificial stones there takes them to a cart with a portal, straight there when there's one,
    // or picked by name from a list. As with any portal, ore and metal carried keep the player from going.
    [HarmonyPatch]
    internal static class CartPortal
    {
        // On the cart: what it's called in the list.
        private const string NameKey = "cartheim_name";
        // Where the player arrives, beside the cart.
        private const float BesideCart = 2.5f;

        public static void Name(CartBase cart, Player player)
        {
            var zdo = cart ? cart.GetComponent<ZNetView>()?.GetZDO() : null;
            if (zdo != null && player && zdo.GetString(NameKey, "") == "")
                zdo.Set(NameKey, Portal.CartName(player.GetPlayerName()));
        }

        public static void ToSpawn(Player player)
        {
            if (!player || !ZoneSystem.instance || !Game.instance)
                return;
            if (!player.IsTeleportable(false))
            {
                player.Message(MessageHud.MessageType.Center, "$msg_noteleport");
                return;
            }
            if (!ZoneSystem.instance.GetLocationIcon(Game.instance.m_StartLocation, out var spawn))
            {
                player.Message(MessageHud.MessageType.Center, "Can't find the spawn");
                return;
            }
            InventoryGui.instance?.Hide();
            Debug.Log($"[Cartheim] Through the cart's portal to the spawn, at {spawn}");
            player.TeleportTo(spawn + Vector3.up, player.transform.rotation, true);
        }

        // Every cart in the world with a portal built, as saved (near or far).
        private static List<(ZDO zdo, string label)> PortalCarts()
        {
            var zdos = new List<ZDO>();
            var index = 0;
            while (ZDOMan.instance != null && !ZDOMan.instance.GetAllZDOsWithPrefabIterative("Cart", zdos, ref index))
            {
            }
            var carts = zdos.Where(z => CartUpgrades.Load(z.GetString(CartBase.SaveKey, "")).Has(Portal.Piece)).ToList();
            var labels = Portal.Labels(carts.Select(z => z.GetString(NameKey, "A cart")));
            return carts.Select((z, i) => (z, labels[i])).ToList();
        }

        private static void ToCart(Player player, ZDO cart, string label)
        {
            if (!player)
                return;
            if (!player.IsTeleportable(false))
            {
                player.Message(MessageHud.MessageType.Center, "$msg_noteleport");
                return;
            }
            var facing = cart.GetRotation();
            var at = cart.GetPosition() + facing * Vector3.right * BesideCart + Vector3.up;
            Debug.Log($"[Cartheim] From the spawn to {label}, at {at}");
            player.TeleportTo(at, facing, true);
        }

        private static bool AtSacrificialStones(ItemStand stand) => stand && stand.GetComponentInParent<BossStone>();

        // Shift+E on the sacrificial stones: to a cart (plain E still sets a trophy in place).
        [HarmonyPatch(typeof(ItemStand), nameof(ItemStand.Interact)), HarmonyPrefix]
        private static bool ToACart(ItemStand __instance, Humanoid user, bool hold, bool alt, ref bool __result)
        {
            if (!alt || !Plugin.Enabled.Value || !AtSacrificialStones(__instance) || user != Player.m_localPlayer)
                return true;
            __result = true;
            if (hold)
                return false;
            var player = (Player)user;
            var carts = PortalCarts();
            Debug.Log($"[Cartheim] At the sacrificial stones, carts with a portal: {string.Join(", ", carts.Select(c => c.label))}");
            if (carts.Count == 0)
                player.Message(MessageHud.MessageType.Center, "No cart has a portal");
            else if (carts.Count == 1)
                ToCart(player, carts[0].zdo, carts[0].label);
            else
                Choose(player, carts);
            return false;
        }

        [HarmonyPatch(typeof(ItemStand), nameof(ItemStand.GetHoverText)), HarmonyPostfix]
        private static void SayToACart(ItemStand __instance, ref string __result)
        {
            if (Plugin.Enabled.Value && AtSacrificialStones(__instance))
                __result += Localization.instance.Localize("\n[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>] Travel to a cart");
        }

        // The list of carts to pick from.
        private static GameObject _panel;
        private const float Width = 360f;
        private const float RowHeight = 44f;

        private static void Choose(Player player, List<(ZDO zdo, string label)> carts)
        {
            Close();
            var height = 110f + carts.Count * RowHeight;
            var middle = new Vector2(0.5f, 0.5f);
            _panel = GUIManager.Instance.CreateWoodpanel(GUIManager.CustomGUIFront.transform, middle, middle, Vector2.zero, Width, height, draggable: false);
            var title = GUIManager.Instance.CreateText("Travel to which cart?", _panel.transform, middle, middle, new Vector2(0f, height / 2f - 30f),
                GUIManager.Instance.AveriaSerifBold, 22, GUIManager.Instance.ValheimOrange, true, Color.black, Width - 40f, 30f, false);
            title.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            for (var i = 0; i < carts.Count; i++)
            {
                var (zdo, label) = carts[i];
                var button = GUIManager.Instance.CreateButton(label, _panel.transform, middle, middle,
                    new Vector2(0f, height / 2f - 75f - i * RowHeight), Width - 60f, RowHeight - 6f);
                button.GetComponent<Button>().onClick.AddListener(() =>
                {
                    Close();
                    ToCart(Player.m_localPlayer, zdo, label);
                });
            }
            var cancel = GUIManager.Instance.CreateButton("Cancel", _panel.transform, middle, middle, new Vector2(0f, -height / 2f + 30f), 140f, 36f);
            cancel.GetComponent<Button>().onClick.AddListener(Close);
            GUIManager.BlockInput(true);
        }

        public static void Close()
        {
            if (!_panel)
                return;
            Object.Destroy(_panel);
            _panel = null;
            GUIManager.BlockInput(false);
        }

        // Escape closes the list.
        public static void Update()
        {
            if (_panel && BepInEx.UnityInput.Current.GetKeyDown(KeyCode.Escape))
                Close();
        }
    }
}
