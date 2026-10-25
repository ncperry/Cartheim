using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Cartheim.Plugin
{
    // Sleeping in a cart's bed and resting by a cart: both need a fire near the cart and no enemies about, but no roof.
    // Also the bees in a cart's beehives, kept happy.
    internal static class RestPatches
    {
        private static bool IsCartPiece(Component piece) => Plugin.Enabled.Value && piece.GetComponent<CartRider>();

        // A cart bed needs no roof over it, for setting the spawn point or for sleeping.
        [HarmonyPatch(typeof(Bed), "CheckExposure"), HarmonyPrefix]
        private static bool CartBedNeedsNoRoof(Bed __instance, ref bool __result)
        {
            if (!IsCartPiece(__instance))
                return true;
            __result = true;
            return false;
        }

        // A cart bed is warm when a fire burns near the cart (its hearth, or a fire within range), not right beside the bed.
        [HarmonyPatch(typeof(Bed), "CheckFire"), HarmonyPrefix]
        private static bool CartBedFire(Bed __instance, Player human, ref bool __result)
        {
            if (!IsCartPiece(__instance))
                return true;
            __result = CartFire.Near(__instance.transform.position);
            if (!__result)
                human.Message(MessageHud.MessageType.Center, "$msg_bednofire");
            return false;
        }

        private static readonly AccessTools.FieldRef<Player, Transform> AttachPoint = AccessTools.FieldRefAccess<Player, Transform>("m_attachPoint");

        // How far from the middle of the cart a player getting out of its bed is put down: clear of the cart's sides.
        private const float GetOutDistance = 1.4f;

        // Getting out of bed puts the player where they lay, a little higher: inside a cart, that's inside the cart's own
        // body, where they get stuck. Out of a cart's bed, they stand on the ground beside the cart instead.
        [HarmonyPatch(typeof(Player), nameof(Player.AttachStop)), HarmonyPrefix]
        private static void LeavingCartBed(Player __instance, out CartBase __state)
        {
            var point = AttachPoint(__instance);
            var bed = point ? point.GetComponentInParent<Bed>() : null;
            __state = bed && IsCartPiece(bed) ? CartFire.CartNear(bed.transform.position) : null;
        }

        [HarmonyPatch(typeof(Player), nameof(Player.AttachStop)), HarmonyPostfix]
        private static void StepOutBesideCart(Player __instance, CartBase __state)
        {
            if (!__state || __instance.IsAttached())
                return;
            var cart = __state.transform;
            // The side the player was facing, so they get out where they look.
            var side = Vector3.Dot(__instance.transform.forward, cart.right) >= 0f ? cart.right : -cart.right;
            var spot = cart.position + side * GetOutDistance;
            spot.y = ZoneSystem.instance ? ZoneSystem.instance.GetSolidHeight(spot) : spot.y;
            __instance.transform.position = spot;
            Debug.Log($"[Cartheim] Out of the cart's bed, beside the cart at {spot}");
        }

        // Beside a cart with a fire near it, the game counts the player as sheltered...
        [HarmonyPatch(typeof(Player), nameof(Player.InShelter)), HarmonyPostfix]
        private static void ShelteredByCart(Player __instance, ref bool __result)
        {
            if (!__result && __instance == Player.m_localPlayer && CartFire.RestingByCart(__instance.transform.position))
                __result = true;
        }

        // ...and as near the fire, so they rest (and grow rested) there as long as no enemy has sensed them.
        [HarmonyPatch(typeof(Player), "UpdateEnvStatusEffects"), HarmonyPrefix]
        private static void WarmedByCart(Player __instance, ref float ___m_nearFireTimer)
        {
            if (__instance == Player.m_localPlayer && CartFire.RestingByCart(__instance.transform.position))
                ___m_nearFireTimer = 0f;
        }

        // The game counts the comfort of the cart's hidden furniture by itself; the roof and the hearth aren't pieces, so their
        // comfort is added here.
        [HarmonyPatch(typeof(SE_Rested), nameof(SE_Rested.CalculateComfortLevel), typeof(bool), typeof(Vector3)), HarmonyPostfix]
        private static void CartComfort(bool inShelter, Vector3 position, ref int __result)
        {
            if (!inShelter || !Plugin.Enabled.Value)
                return;
            var cart = CartFire.CartNear(position);
            if (!cart)
                return;
            var hearth = GamePieces.Prefab("hearth")?.GetComponent<Piece>();
            var pieces = new List<Piece>();
            Piece.GetAllComfortPiecesInRadius(position, 10f, pieces);
            var bestFire = pieces.Where(p => p.m_comfortGroup == Piece.ComfortGroup.Fire).Select(p => p.GetComfort()).DefaultIfEmpty(0).Max();
            __result += cart.Upgrades.ExtraComfort(hearth ? hearth.m_comfort : 0, bestFire);
        }

        // A cart's bees are always happy: any biome, and the cart never crowds them.
        [HarmonyPatch(typeof(Beehive), "CheckBiome"), HarmonyPrefix]
        private static bool CartBeesLikeAnyBiome(Beehive __instance, ref bool __result) => HappyCartBees(__instance, ref __result);

        [HarmonyPatch(typeof(Beehive), "HaveFreeSpace"), HarmonyPrefix]
        private static bool CartBeesHaveRoom(Beehive __instance, ref bool __result) => HappyCartBees(__instance, ref __result);

        private static bool HappyCartBees(Beehive hive, ref bool result)
        {
            if (!IsCartPiece(hive))
                return true;
            result = true;
            return false;
        }

        // Switched off, the cart's hidden furniture gives no comfort.
        [HarmonyPatch(typeof(Piece), nameof(Piece.GetAllComfortPiecesInRadius)), HarmonyPostfix]
        private static void NoCartComfortWhileOff(List<Piece> pieces)
        {
            if (!Plugin.Enabled.Value)
                pieces.RemoveAll(p => p && p.GetComponent<CartRider>());
        }
    }
}
