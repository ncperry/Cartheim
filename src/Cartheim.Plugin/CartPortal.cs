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

        // A cart to go to: its name, where it is and which way it faces.
        private struct PortalCart
        {
            public string Name;
            public Vector3 At;
            public Quaternion Facing;
        }

        // Every cart with a portal built that this game knows of, as saved (near or far). The server knows them all; a player
        // on a dedicated server only those they've been near, so they ask the server (when it runs Cartheim too).
        private static List<PortalCart> KnownPortalCarts()
        {
            var zdos = new List<ZDO>();
            var index = 0;
            while (ZDOMan.instance != null && !ZDOMan.instance.GetAllZDOsWithPrefabIterative("Cart", zdos, ref index))
            {
            }
            return zdos.Where(z => CartUpgrades.Load(z.GetString(CartBase.SaveKey, "")).Has(Portal.Piece))
                .Select(z => new PortalCart { Name = z.GetString(NameKey, "A cart"), At = z.GetPosition(), Facing = z.GetRotation() })
                .ToList();
        }

        private const string AskRpc = "Cartheim_WhichPortalCarts";
        private const string AnswerRpc = "Cartheim_PortalCartsAre";
        // How long to wait for the server's list before going by this game's own.
        private const float AnswerWait = 2f;
        private static ZRoutedRpc _registeredWith;
        private static float _askedAt = -1f;

        private static readonly System.Reflection.FieldInfo FunctionsField = AccessTools.Field(typeof(ZRoutedRpc), "m_functions");

        private static void Register()
        {
            var rpc = ZRoutedRpc.instance;
            if (rpc == null || rpc == _registeredWith)
                return;
            _registeredWith = rpc;
            var functions = (System.Collections.IDictionary)FunctionsField.GetValue(rpc);
            functions.Remove(AskRpc.GetStableHashCode());
            functions.Remove(AnswerRpc.GetStableHashCode());
            rpc.Register(AskRpc, RPC_Ask);
            rpc.Register<ZPackage>(AnswerRpc, RPC_Answer);
        }

        public static void Unregister()
        {
            if (_registeredWith != null)
            {
                var functions = (System.Collections.IDictionary)FunctionsField.GetValue(_registeredWith);
                functions.Remove(AskRpc.GetStableHashCode());
                functions.Remove(AnswerRpc.GetStableHashCode());
            }
            _registeredWith = null;
        }

        // The server: its list, to whoever asked.
        private static void RPC_Ask(long sender)
        {
            var carts = KnownPortalCarts();
            var package = new ZPackage();
            package.Write(carts.Count);
            foreach (var cart in carts)
            {
                package.Write(cart.Name);
                package.Write(cart.At);
                package.Write(cart.Facing);
            }
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, AnswerRpc, package);
        }

        private static void RPC_Answer(long sender, ZPackage package)
        {
            if (_askedAt < 0f)
                return;
            _askedAt = -1f;
            var carts = new List<PortalCart>();
            var count = package.ReadInt();
            for (var i = 0; i < count; i++)
                carts.Add(new PortalCart { Name = package.ReadString(), At = package.ReadVector3(), Facing = package.ReadQuaternion() });
            Debug.Log($"[Cartheim] The server knows {carts.Count} cart(s) with a portal");
            Offer(carts);
        }

        private static void AskForCarts()
        {
            if (ZNet.instance.IsServer())
            {
                Offer(KnownPortalCarts());
                return;
            }
            _askedAt = Time.time;
            ZRoutedRpc.instance.InvokeRoutedRPC(AskRpc);
        }

        // No cart: said so; one: straight there; several: picked by name.
        private static void Offer(List<PortalCart> carts)
        {
            var player = Player.m_localPlayer;
            if (!player)
                return;
            var labels = Portal.Labels(carts.Select(c => c.Name));
            for (var i = 0; i < carts.Count; i++)
                carts[i] = new PortalCart { Name = labels[i], At = carts[i].At, Facing = carts[i].Facing };
            Debug.Log($"[Cartheim] At the sacrificial stones, carts with a portal: {string.Join(", ", labels)}");
            if (carts.Count == 0)
                player.Message(MessageHud.MessageType.Center, "No cart has a portal");
            else if (carts.Count == 1)
                ToCart(player, carts[0]);
            else
                Choose(player, carts);
        }

        private static void ToCart(Player player, PortalCart cart)
        {
            if (!player)
                return;
            if (!player.IsTeleportable(false))
            {
                player.Message(MessageHud.MessageType.Center, "$msg_noteleport");
                return;
            }
            var at = cart.At + cart.Facing * Vector3.right * BesideCart + Vector3.up;
            Debug.Log($"[Cartheim] From the spawn to {cart.Name}, at {at}");
            player.TeleportTo(at, cart.Facing, true);
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
            AskForCarts();
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

        private static void Choose(Player player, List<PortalCart> carts)
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
                var cart = carts[i];
                var button = GUIManager.Instance.CreateButton(cart.Name, _panel.transform, middle, middle,
                    new Vector2(0f, height / 2f - 75f - i * RowHeight), Width - 60f, RowHeight - 6f);
                button.GetComponent<Button>().onClick.AddListener(() =>
                {
                    Close();
                    ToCart(Player.m_localPlayer, cart);
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

        // Escape closes the list. A server that hasn't answered (it doesn't run Cartheim): this game's own list.
        public static void Update()
        {
            Register();
            if (_panel && BepInEx.UnityInput.Current.GetKeyDown(KeyCode.Escape))
                Close();
            if (_askedAt >= 0f && Time.time - _askedAt > AnswerWait)
            {
                _askedAt = -1f;
                Debug.Log("[Cartheim] The server didn't list the carts (it may not run Cartheim); going by the carts this game knows");
                Offer(KnownPortalCarts());
            }
        }
    }
}
