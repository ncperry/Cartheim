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
        private enum Placing { Nothing, Raft, Cart, Garden, Ruin, LeaveRuin }

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
            if (cart.InRuin)
            {
                player.Message(MessageHud.MessageType.Center, "Return the cart to cart form first");
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

        // The ruin that settling is for (see CartRuin).
        private static Location _ruin;

        // From the ruin tab: the cart settled into a ruin, everything added to it shown in place over it first.
        public static void BeginRuin(CartBase cart, Location ruin)
        {
            var player = Player.m_localPlayer;
            if (!player || !cart || !ruin)
                return;
            var vagon = cart.GetComponent<Vagon>();
            var problem = cart.TentUp ? "Pack up the tent first" : vagon && vagon.IsAttached() ? "Unhitch the cart first" : null;
            if (problem != null)
            {
                player.Message(MessageHud.MessageType.Center, problem);
                return;
            }
            InventoryGui.instance?.Hide();
            Begin(cart, Placing.Ruin, CartRuin.Ghost(cart, ruin));
            _ruin = ruin;
        }

        // From the ruin tab, settled: put the cart down where it's to stand in cart form, as when putting it ashore.
        public static void BeginLeavingRuin(CartBase cart)
        {
            InventoryGui.instance?.Hide();
            Begin(cart, Placing.LeaveRuin, Ghost("Cart"));
            // What the camp added shows see-through, as it's about to go.
            if (IsPlacing)
                CartRuin.ShowThrough(CartRuin.CampOf(cart));
        }

        // Shift+E on the cart afloat: place it ashore.
        public static void BeginLanding(CartBase cart) => Begin(cart, Placing.Cart, Ghost("Cart"));

        // From the garden tab: set out a Wayside Garden near the cart, its gate towards the player.
        public static void BeginGarden(CartBase cart)
        {
            var player = Player.m_localPlayer;
            if (!player || !cart)
                return;
            InventoryGui.instance?.Hide();
            Begin(cart, Placing.Garden, CartGarden.Ghost());
            // A tarp over a ruin shows what's under it, while a garden is placed.
            if (IsPlacing)
                CartRuin.ShowThrough(CartBase.All.Where(c => c && c.Tarp).Select(c => c.Tarp));
            if (IsPlacing)
                _yaw = player.transform.eulerAngles.y;
        }

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
            _red = false;
            var renderers = ghost.GetComponentsInChildren<Renderer>();
            _ghostLift = renderers.Length > 0 ? ghost.transform.position.y - renderers.Min(r => r.bounds.min.y) : 0f;
            _inventoryGone = false;
            Debug.Log($"[Cartheim] Placing the {(placing == Placing.Raft ? "raft" : placing == Placing.Garden ? "garden" : "cart ashore")}");
        }

        public static void Cancel(string message)
        {
            if (IsPlacing)
            {
                Debug.Log("[Cartheim] Stopped placing" + (message != null ? ": " + message : ""));
                // The click that placed or cancelled is still down: it mustn't then punch or block.
                _clickHeld = true;
            }
            if (_ghost)
                Object.Destroy(_ghost);
            _ghost = null;
            _placing = Placing.Nothing;
            CartRuin.ShowAsTheyAre();
            _cart = null;
            if (message != null)
                Player.m_localPlayer?.Message(MessageHud.MessageType.Center, message);
        }

        // A copy of the piece as the game makes its own placement ghosts: made without joining the world, then stripped to its
        // looks (no behaviour or physics), a raft without its mast and sail.
        internal static GameObject Ghost(string piece)
        {
            var prefab = GamePieces.Prefab(piece);
            if (!prefab)
                return null;
            GameObject ghost;
            ZNetView.m_forceDisableInit = true;
            try
            {
                ghost = Object.Instantiate(prefab);
            }
            finally
            {
                ZNetView.m_forceDisableInit = false;
            }
            ghost.name = "Cartheim placing " + piece;
            var ship = ghost.GetComponent<Ship>();
            if (ship)
            {
                if (ship.m_mastObject)
                    ship.m_mastObject.SetActive(false);
                if (ship.m_sailObject)
                    ship.m_sailObject.SetActive(false);
            }
            foreach (var joint in ghost.GetComponentsInChildren<Joint>(true))
                Object.DestroyImmediate(joint);
            foreach (var body in ghost.GetComponentsInChildren<Rigidbody>(true))
                Object.DestroyImmediate(body);
            foreach (var collider in ghost.GetComponentsInChildren<Collider>(true))
                Object.Destroy(collider);
            foreach (var behaviour in ghost.GetComponentsInChildren<MonoBehaviour>(true))
                Object.Destroy(behaviour);
            return ghost;
        }

        // Set as placing ends, until both mouse buttons are let go.
        private static bool _clickHeld;

        public static void Update()
        {
            if (_clickHeld && !IsPlacing && !UnityInput.Current.GetMouseButton(0) && !UnityInput.Current.GetMouseButton(1))
                _clickHeld = false;
            if (!IsPlacing)
                return;
            var player = Player.m_localPlayer;
            if (!InventoryGui.IsVisible())
                _inventoryGone = true;
            var interrupted = !player ? "no player" : player.IsDead() ? "the player died" : !_cart ? "the cart is gone"
                : _inventoryGone && InventoryGui.IsVisible() ? "the inventory opened" : Menu.IsVisible() ? "the menu opened"
                : Minimap.IsOpen() ? "the map opened" : null;
            if (interrupted != null)
            {
                Debug.Log($"[Cartheim] Placing interrupted: {interrupted}");
                Cancel(null);
                return;
            }
            if (UnityInput.Current.GetMouseButtonDown(1))
            {
                Cancel("Cancelled");
                return;
            }
            // Settling into a ruin: the ghost stands over the ruin already; a left click settles the cart.
            if (_placing == Placing.Ruin)
            {
                Say("Left click: settle the cart into the ruin. Right click: cancel");
                if (!UnityInput.Current.GetMouseButtonDown(0))
                    return;
                var settling = _cart;
                var ruin = _ruin;
                Cancel(null);
                if (ruin)
                    CartRuin.Settle(settling, ruin, player);
                return;
            }
            _yaw += UnityInput.Current.mouseScrollDelta.y * TurnPerNotch;
            var camera = GameCamera.instance ? GameCamera.instance.transform : null;
            var mask = LayerMask.GetMask("terrain", "static_solid", "Default");
            if (!camera || !Aim(camera, mask, out var hit))
            {
                _ghost.SetActive(false);
                Say("Aim at where it goes");
                return;
            }
            // The surface looked at: the bed of the water, or the ground (or rock) the cart would stand on.
            var ground = hit.point.y;
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
            else if (_placing == Placing.LeaveRuin)
            {
                at = new Vector3(hit.point.x, ground, hit.point.z);
                problem = !Raft.Ashore(ground, water) ? "Put the cart on dry ground"
                    : !RuinCamp.Near(Vector3.Distance(at, _cart.transform.position)) ? "Too far from the ruin"
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
            if (_placing == Placing.Garden)
            {
                PlaceGarden(player, new Vector3(hit.point.x, ground, hit.point.z), facing);
                return;
            }
            _ghost.SetActive(true);
            Redden(problem != null);
            // The cart's ghost stands on its wheels there, not sunk to its middle.
            _ghost.transform.SetPositionAndRotation(_placing != Placing.Raft ? at + Vector3.up * _ghostLift : at, facing);
            Say(problem ?? (_placing == Placing.Raft
                ? "Left click: launch the raft here (the cart goes aboard). Scroll: turn it. Right click: cancel"
                : _placing == Placing.LeaveRuin
                    ? "Left click: return the cart to cart form here (what was added to the ruin goes). Scroll: turn it. Right click: cancel"
                    : "Left click: put the cart ashore here (the raft is packed away). Scroll: turn it. Right click: cancel"));
            if (problem != null || !UnityInput.Current.GetMouseButtonDown(0))
                return;
            var cart = _cart;
            var placing = _placing;
            Cancel(null);
            if (placing == Placing.Raft)
                cart.Launch(at, facing);
            else if (placing == Placing.LeaveRuin)
                cart.LeaveRuin(at, facing);
            else
                cart.GoAshore(at, facing);
        }

        // The garden's ghost on the ground, each fence where it would stand; a left click sets it out.
        private static void PlaceGarden(Player player, Vector3 at, Quaternion facing)
        {
            var problem = CartGarden.Problem(at, facing, _cart, player);
            _ghost.SetActive(true);
            Redden(problem != null);
            CartGarden.Pose(_ghost, at, facing);
            Say(problem ?? "Left click: set out the garden here. Scroll: turn it. Right click: cancel");
            if (problem != null || !UnityInput.Current.GetMouseButtonDown(0))
                return;
            Cancel(null);
            CartGarden.Place(at, facing, player);
        }

        // What's aimed at, looking through a tarp over a ruin (so a garden can be placed beside or under it).
        private static bool Aim(Transform camera, int mask, out RaycastHit hit)
        {
            foreach (var each in Physics.RaycastAll(camera.position, camera.forward, AimReach, mask, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                if (each.collider.transform.root.name != TentAssets.TarpName)
                {
                    hit = each;
                    return true;
                }
            hit = default;
            return false;
        }

        // Red where it can't go, as the game shows its own ghosts.
        private static bool _red;
        // How far the ghost's middle is above its lowest point.
        private static float _ghostLift;

        private static void Redden(bool red)
        {
            if (red == _red || !_ghost || !MaterialMan.instance)
                return;
            _red = red;
            if (red)
            {
                MaterialMan.instance.SetValue(_ghost, ShaderProps._Color, Color.red);
                MaterialMan.instance.SetValue(_ghost, ShaderProps._EmissionColor, Color.red * 0.7f);
            }
            else
            {
                MaterialMan.instance.ResetValue(_ghost, ShaderProps._Color);
                MaterialMan.instance.ResetValue(_ghost, ShaderProps._EmissionColor);
            }
        }

        private static void Say(string message)
        {
            if (message == _said)
                return;
            _said = message;
            Player.m_localPlayer?.Message(MessageHud.MessageType.Center, message);
        }

        // Kept level: the waves lift each corner of a raft on its own, rocking it, and the cart on it with it. Level, it only
        // turns and bobs.
        private const RigidbodyConstraints Level = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // No sail: paddled only, as fast as paddling goes; and level.
        public static void Dress(Ship raft)
        {
            if (!raft)
                return;
            var body = raft.GetComponent<Rigidbody>();
            if (body && (body.constraints & Level) != Level)
            {
                var turn = raft.transform.eulerAngles.y;
                body.constraints |= Level;
                body.angularVelocity = new Vector3(0f, body.angularVelocity.y, 0f);
                raft.transform.rotation = Quaternion.Euler(0f, turn, 0f);
            }
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
        // instead of zooming; so too the click that ends it, until it's let go.
        [HarmonyPatch(typeof(Player), nameof(Player.SetControls)), HarmonyPrefix]
        private static void NoFighting(ref bool attack, ref bool attackHold, ref bool secondaryAttack, ref bool secondaryAttackHold, ref bool block, ref bool blockHold)
        {
            if (!IsPlacing && !_clickHeld)
                return;
            attack = attackHold = secondaryAttack = secondaryAttackHold = block = blockHold = false;
        }

        [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetMouseScrollWheel)), HarmonyPostfix]
        private static void NoZoom(ref float __result)
        {
            if (IsPlacing)
                __result = 0f;
        }

        // The cart's bees keep quiet (and out of sight) while it's afloat.
        [HarmonyPatch(typeof(Beehive), "UpdateBees"), HarmonyPostfix]
        private static void QuietAfloat(Beehive __instance)
        {
            if (__instance.m_beeEffect && __instance.m_beeEffect.activeSelf && CartBase.All.Any(c => c && c.Rafting && c.Beehives.Contains(__instance)))
                __instance.m_beeEffect.SetActive(false);
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
