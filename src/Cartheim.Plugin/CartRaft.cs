using System.Linq;
using BepInEx;
using Cartheim.Core;
using HarmonyLib;
using UnityEngine;

namespace Cartheim.Plugin
{
    // Fording a river: the cart goes aboard its raft and is paddled across, then put ashore. Both are placed as the hammer
    // places a piece: a ghost follows where the player looks, the scroll wheel turns it, a left click puts it there and a
    // right click cancels. The raft goes where the water is deep enough to float it, near the cart; the cart goes ashore
    // on dry ground near its raft, and the raft is packed away. Afloat, the raft has no sail and the cart can't be used:
    // no stations, no storage.
    [HarmonyPatch]
    internal static class CartRaft
    {
        private enum Placing { Nothing, Raft, Cart }

        private const float AimReach = 40f;
        private const float TurnPerNotch = 22.5f;

        private static Placing _placing;
        private static CartBase _cart;
        private static GameObject _ghost;
        private static float _yaw;
        private static string _said;
        // Whether the inventory screen (closed to place the raft, which takes it a frame or two) has gone; opened again, it
        // cancels placing.
        private static bool _inventoryGone;

        public static bool IsPlacing => _placing != Placing.Nothing;

        // From the raft tab: place the raft, the cart going aboard.
        public static void BeginLaunch(CartBase cart)
        {
            var player = Player.m_localPlayer;
            if (!player || !cart)
                return;
            var vagon = cart.GetComponent<Vagon>();
            if (cart.TentUp)
            {
                player.Message(MessageHud.MessageType.Center, "Pack up the tent first");
                return;
            }
            if (vagon && vagon.IsAttached(player))
            {
                player.Message(MessageHud.MessageType.Center, "Unhitch the cart first");
                return;
            }
            InventoryGui.instance?.Hide();
            Begin(cart, Placing.Raft, Ghost(Raft.Piece));
        }

        // Shift+E on the cart afloat: place it ashore.
        public static void BeginLanding(CartBase cart) => Begin(cart, Placing.Cart, Ghost("Cart"));

        private static void Begin(CartBase cart, Placing placing, GameObject ghost)
        {
            Cancel(null);
            if (!ghost)
                return;
            _cart = cart;
            _placing = placing;
            _ghost = ghost;
            _yaw = cart.transform.eulerAngles.y;
            _said = null;
            _inventoryGone = false;
            Debug.Log($"[Cartheim] Placing the {(placing == Placing.Raft ? "raft" : "cart ashore")}");
        }

        public static void Cancel(string message)
        {
            if (IsPlacing)
                Debug.Log("[Cartheim] Stopped placing" + (message != null ? ": " + message : ""));
            if (_ghost)
                Object.Destroy(_ghost);
            _ghost = null;
            _placing = Placing.Nothing;
            _cart = null;
            if (message != null)
                Player.m_localPlayer?.Message(MessageHud.MessageType.Center, message);
        }

        // The piece's meshes (a raft without its mast and sail), nothing to bump into.
        private static GameObject Ghost(string piece)
        {
            var ship = GamePieces.Prefab(piece)?.GetComponent<Ship>();
            var ghost = CartLooks.Model(piece, null, ship ? new[] { ship.m_mastObject, ship.m_sailObject } : null);
            if (ghost)
                ghost.name = "Cartheim placing " + piece;
            return ghost;
        }

        public static void Update()
        {
            if (!IsPlacing)
                return;
            var player = Player.m_localPlayer;
            if (!InventoryGui.IsVisible())
                _inventoryGone = true;
            if (!player || player.IsDead() || !_cart || (_inventoryGone && InventoryGui.IsVisible()) || Menu.IsVisible() || Minimap.IsOpen())
            {
                Cancel(null);
                return;
            }
            if (UnityInput.Current.GetMouseButtonDown(1))
            {
                Cancel("Cancelled");
                return;
            }
            _yaw += UnityInput.Current.mouseScrollDelta.y * TurnPerNotch;
            var camera = GameCamera.instance ? GameCamera.instance.transform : null;
            var mask = LayerMask.GetMask("terrain", "static_solid", "Default");
            if (!camera || !Physics.Raycast(camera.position, camera.forward, out var hit, AimReach, mask))
            {
                _ghost.SetActive(false);
                Say("Aim at where it goes");
                return;
            }
            var ground = ZoneSystem.instance ? ZoneSystem.instance.GetGroundHeight(hit.point) : hit.point.y;
            var water = ZoneSystem.instance ? ZoneSystem.instance.m_waterLevel : 30f;
            var raft = _cart.RaftShip;
            string problem;
            Vector3 at;
            if (_placing == Placing.Raft)
            {
                at = new Vector3(hit.point.x, water, hit.point.z);
                problem = !Raft.Floats(ground, water) ? "Too shallow to float the raft"
                    : !Raft.Near(Vector3.Distance(at, _cart.transform.position)) ? "Too far from the cart"
                    : null;
            }
            else
            {
                at = new Vector3(hit.point.x, ground, hit.point.z);
                problem = !Raft.Ashore(ground, water) ? "Put the cart ashore, on dry ground"
                    : raft && !Raft.Near(Vector3.Distance(at, raft.transform.position)) ? "Too far from the raft"
                    : null;
            }
            var facing = Quaternion.Euler(0f, _yaw, 0f);
            _ghost.SetActive(true);
            _ghost.transform.SetPositionAndRotation(at, facing);
            Say(problem ?? (_placing == Placing.Raft
                ? "Left click: launch the raft here (the cart goes aboard). Scroll: turn it. Right click: cancel"
                : "Left click: put the cart ashore here (the raft is packed away). Scroll: turn it. Right click: cancel"));
            if (problem != null || !UnityInput.Current.GetMouseButtonDown(0))
                return;
            var cart = _cart;
            var placing = _placing;
            Cancel(null);
            if (placing == Placing.Raft)
                cart.Launch(at, facing);
            else
                cart.GoAshore(at, facing);
        }

        private static void Say(string message)
        {
            if (message == _said)
                return;
            _said = message;
            Player.m_localPlayer?.Message(MessageHud.MessageType.Center, message);
        }

        // No sail: paddled only, as fast as paddling goes.
        public static void Dress(Ship raft)
        {
            if (!raft)
                return;
            if (raft.m_mastObject && raft.m_mastObject.activeSelf)
                raft.m_mastObject.SetActive(false);
            if (raft.m_sailObject && raft.m_sailObject.activeSelf)
                raft.m_sailObject.SetActive(false);
        }

        // The top of the raft's deck at its middle, by its own colliders.
        public static float DeckHeight(Ship raft)
        {
            var ray = new Ray(raft.transform.position + Vector3.up * 10f, Vector3.down);
            var deck = float.MinValue;
            foreach (var collider in raft.GetComponentsInChildren<Collider>())
                if (collider.enabled && !collider.isTrigger && collider.Raycast(ray, out var hit, 20f))
                    deck = Mathf.Max(deck, hit.point.y);
            return deck > float.MinValue ? deck : raft.transform.position.y + 0.5f;
        }

        private static bool IsCartRaft(Ship ship) => ship && ship.GetComponent<ZNetView>()?.GetZDO()?.GetLong(CartBase.RaftKey) is long uid && uid != 0;

        private static readonly AccessTools.FieldRef<Ship, Ship.Speed> Speed = AccessTools.FieldRefAccess<Ship, Ship.Speed>("m_speed");

        [HarmonyPatch(typeof(Ship), "RPC_Forward"), HarmonyPostfix]
        private static void PaddleOnly(Ship __instance)
        {
            if (Speed(__instance) > Ship.Speed.Slow && IsCartRaft(__instance))
                Speed(__instance) = Ship.Speed.Slow;
        }

        // While placing, clicks place or cancel instead of attacking or blocking, and the scroll wheel turns the ghost
        // instead of zooming.
        [HarmonyPatch(typeof(Player), nameof(Player.SetControls)), HarmonyPrefix]
        private static void NoFighting(ref bool attack, ref bool attackHold, ref bool secondaryAttack, ref bool secondaryAttackHold, ref bool block, ref bool blockHold)
        {
            if (!IsPlacing)
                return;
            attack = attackHold = secondaryAttack = secondaryAttackHold = block = blockHold = false;
        }

        [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetMouseScrollWheel)), HarmonyPostfix]
        private static void NoZoom(ref float __result)
        {
            if (IsPlacing)
                __result = 0f;
        }

        // The cart's storage can't be opened while it's afloat.
        [HarmonyPatch(typeof(Container), nameof(Container.Interact)), HarmonyPrefix]
        private static bool StowedAfloat(Container __instance, Humanoid character, ref bool __result)
        {
            var cart = __instance.GetComponentInParent<CartBase>();
            if (!cart || !cart.Rafting)
                return true;
            character.Message(MessageHud.MessageType.Center, "The cart is on its raft: Shift+E on it to put it ashore");
            __result = false;
            return false;
        }
    }
}
