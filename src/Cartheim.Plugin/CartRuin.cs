using System.Collections.Generic;
using System.Linq;
using Cartheim.Core;
using HarmonyLib;
using Splatform;
using UnityEngine;

namespace Cartheim.Plugin
{
    // A cart settled into a ruin (see RuinCamp): from the cart's tabs, within reach of a ruin the mod knows, a ghost of
    // everything shows over the ruin, and a click settles the cart into its place there, adding a wood door in the ruin's
    // doorway, a wood fence round the cart, the cart's tent as a tarp over the ruin (if it has a tent; see CartBase), and
    // a garden beside it (if the player has a cultivator, on them or in the cart): its ground levelled and cultivated,
    // nothing planted. All free, all real pieces, marked as the camp's: the hammer can't take them apart, they drop
    // nothing when broken, and they go when the cart returns to cart form.
    [HarmonyPatch]
    internal static class CartRuin
    {
        private const string CampOfKey = "cartheim_camp_of";
        private const string DoorName = "$piece_wooddoor";
        private const string FenceName = "$piece_woodfence";
        // How much room a garden needs round it, beyond its fence, to be set out there.
        private const float GardenRoom = 0.3f;

        private static readonly AccessTools.FieldRef<List<Location>> AllLocations =
            AccessTools.StaticFieldRefAccess<List<Location>>(AccessTools.Field(typeof(Location), "s_allLocations"));
        private static readonly AccessTools.FieldRef<ZDOMan, Dictionary<ZDOID, ZDO>> Objects =
            AccessTools.FieldRefAccess<ZDOMan, Dictionary<ZDOID, ZDO>>("m_objectsByID");

        // The nearest ruin the mod knows within reach of the cart.
        public static Location Near(CartBase cart) =>
            cart ? AllLocations().Where(l => l && RuinCamp.For(l.name) != null && RuinCamp.Near(Vector3.Distance(l.transform.position, cart.transform.position)))
                .OrderBy(l => Vector3.Distance(l.transform.position, cart.transform.position)).FirstOrDefault() : null;

        // A garden by a ruin: for a player with a cultivator, on them or in the cart.
        private static bool WithGarden(Player player) =>
            player && GamePieces.Carried(player, new[] { new ItemCount("Cultivator", 1) }).TryGetValue("Cultivator", out var have) && have > 0;

        private static Vector3 World(Location ruin, float x, float z)
        {
            var p = ruin.transform.position + Quaternion.Euler(0f, ruin.transform.eulerAngles.y, 0f) * new Vector3(x, 0f, z);
            return new Vector3(p.x, CartGarden.Ground(p), p.z);
        }

        private static Quaternion Turn(Location ruin, float yaw) => Quaternion.Euler(0f, ruin.transform.eulerAngles.y + yaw, 0f);

        // The first of the ruin's garden spots with nothing in the way (rocks and trees, which differ from ruin to ruin).
        private static (Vector3 at, Quaternion facing)? GardenSpot(Location ruin, RuinCamp camp)
        {
            var mask = LayerMask.GetMask("static_solid", "piece", "vehicle");
            foreach (var (x, z, yaw) in camp.Gardens)
            {
                var at = World(ruin, x, z);
                var facing = Turn(ruin, yaw);
                var half = new Vector3(Garden.Width / 2f + GardenRoom, 1.1f, Garden.Depth / 2f + GardenRoom);
                if (!Physics.CheckBox(at + Vector3.up * 1.4f, half, facing, mask, QueryTriggerInteraction.Ignore))
                    return (at, facing);
            }
            return null;
        }

        // Everything the camp adds, where it goes: the door, then the fence, each piece with how it's turned.
        private static IEnumerable<(CartGarden.Kind kind, Vector3 at, Quaternion facing)> Pieces(Location ruin, RuinCamp camp)
        {
            var door = CartGarden.HammerPiece(DoorName);
            var fence = CartGarden.HammerPiece(FenceName);
            if (door.Prefab)
            {
                var facing = Turn(ruin, camp.DoorYaw + door.Turn);
                var at = World(ruin, camp.DoorX, camp.DoorZ);
                // Its middle in the doorway (a door's pivot is at its hinge).
                yield return (door, at - facing * door.Middle, facing);
            }
            if (fence.Prefab)
                foreach (var run in camp.Fence)
                    foreach (var (x, z, yaw) in FenceRun.Pieces(run.from, run.to, fence.Length))
                        yield return (fence, World(ruin, x, z), Turn(ruin, yaw + fence.Turn));
        }

        // The ghost of it all, standing over the ruin: the cart in its place, the door, the fence, the tarp and the garden.
        public static GameObject Ghost(CartBase cart, Location ruin, Player player)
        {
            var camp = RuinCamp.For(ruin.name);
            if (camp == null || !CartGarden.Ready())
                return null;
            var root = new GameObject("Cartheim settling into a ruin");
            var cartGhost = CartRaft.Ghost("Cart");
            if (cartGhost)
            {
                var renderers = cartGhost.GetComponentsInChildren<Renderer>();
                var lift = renderers.Length > 0 ? cartGhost.transform.position.y - renderers.Min(r => r.bounds.min.y) : 0f;
                cartGhost.transform.SetPositionAndRotation(World(ruin, camp.CartX, camp.CartZ) + Vector3.up * lift, Turn(ruin, camp.CartYaw));
                cartGhost.transform.SetParent(root.transform, true);
            }
            foreach (var (kind, at, facing) in Pieces(ruin, camp))
            {
                var piece = CartRaft.Ghost(kind.Prefab.name);
                if (!piece)
                    continue;
                piece.transform.SetPositionAndRotation(at + Vector3.up * kind.Lift, facing);
                piece.transform.SetParent(root.transform, true);
            }
            if (cart.HasTent)
            {
                var tarp = TentAssets.Tarp(ruin.transform.position, Quaternion.Euler(0f, ruin.transform.eulerAngles.y, 0f),
                    new Vector3(camp.TarpMinX, camp.TarpLow, camp.TarpMinZ), new Vector3(camp.TarpMaxX, camp.TarpHigh, camp.TarpMaxZ), ghost: true);
                if (tarp)
                    tarp.transform.SetParent(root.transform, true);
            }
            if (WithGarden(player) && GardenSpot(ruin, camp) is var spot && spot.HasValue)
            {
                CartGarden.Unplanted();
                var garden = CartGarden.Ghost();
                if (garden)
                {
                    CartGarden.Pose(garden, spot.Value.at, spot.Value.facing);
                    garden.transform.SetParent(root.transform, true);
                }
            }
            return root;
        }

        // The cart whose camp pieces are being built.
        private static long _campOf;

        public static void Settle(CartBase cart, Location ruin, Player player)
        {
            var camp = RuinCamp.For(ruin ? ruin.name : null);
            if (camp == null || !cart || !player)
                return;
            cart.Settle(camp, ruin.transform.position, ruin.transform.eulerAngles.y);
            _campOf = cart.Uid();
            try
            {
                foreach (var (kind, at, facing) in Pieces(ruin, camp))
                {
                    var built = Object.Instantiate(kind.Prefab, at + Vector3.up * kind.Lift, facing);
                    built.GetComponent<Piece>()?.SetCreator(player.GetPlayerID(), PlatformManager.DistributionPlatform.LocalUser.PlatformUserID);
                    Mark(built);
                }
                var spot = WithGarden(player) ? GardenSpot(ruin, camp) : null;
                if (spot.HasValue)
                    CartGarden.PlaceForCamp(spot.Value.at, spot.Value.facing, player);
                else if (WithGarden(player))
                    player.Message(MessageHud.MessageType.Center, "No room for a garden beside this ruin");
            }
            finally
            {
                _campOf = 0;
            }
            player.Message(MessageHud.MessageType.Center, "The cart is settled into the ruin");
        }

        // A piece added by a camp: marked as the cart's, and not to be taken apart with the hammer.
        public static void Mark(GameObject built)
        {
            var view = built.GetComponent<ZNetView>();
            if (view && view.IsValid() && _campOf != 0)
                view.GetZDO().Set(CampOfKey, _campOf);
            var piece = built.GetComponent<Piece>();
            if (piece)
                piece.m_canBeRemoved = false;
        }

        private static bool IsCamp(Component piece)
        {
            var zdo = piece ? piece.GetComponent<ZNetView>()?.GetZDO() : null;
            return zdo != null && zdo.GetLong(CampOfKey, 0L) != 0;
        }

        // Everything a cart's camp added, gone (loaded or not), with nothing dropped.
        public static void TearDown(long cartUid)
        {
            if (cartUid == 0 || ZDOMan.instance == null)
                return;
            var me = ZDOMan.GetSessionID();
            var gone = 0;
            foreach (var zdo in Objects(ZDOMan.instance).Values.Where(z => z.GetLong(CampOfKey, 0L) == cartUid).ToList())
            {
                zdo.SetOwner(me);
                var instance = ZNetScene.instance ? ZNetScene.instance.FindInstance(zdo) : null;
                if (instance)
                    ZNetScene.instance.Destroy(instance.gameObject);
                else
                    ZDOMan.instance.DestroyZDO(zdo);
                gone++;
            }
            Debug.Log($"[Cartheim] Cart {cartUid}'s camp taken down: {gone} pieces");
        }

        // A camp's pieces can't be taken apart with the hammer, and give nothing back when broken.
        [HarmonyPatch(typeof(Piece), nameof(Piece.CanBeRemoved)), HarmonyPostfix]
        private static void NotByHammer(Piece __instance, ref bool __result)
        {
            if (__result && IsCamp(__instance))
            {
                __instance.m_canBeRemoved = false;
                __result = false;
            }
        }

        [HarmonyPatch(typeof(Piece), nameof(Piece.DropResources)), HarmonyPrefix]
        private static bool NothingBack(Piece __instance) => !IsCamp(__instance);

        // Loaded again, a camp's piece still can't be taken apart.
        [HarmonyPatch(typeof(Piece), "Awake"), HarmonyPostfix]
        private static void StillNotByHammer(Piece __instance)
        {
            if (IsCamp(__instance))
                __instance.m_canBeRemoved = false;
        }
    }
}
