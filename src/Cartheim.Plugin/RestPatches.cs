using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Cartheim.Plugin
{
    // Sleeping in a cart's bed and resting by a cart: both need a fire near the cart and no enemies about, but no roof.
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

        // Switched off, the cart's hidden furniture gives no comfort.
        [HarmonyPatch(typeof(Piece), nameof(Piece.GetAllComfortPiecesInRadius)), HarmonyPostfix]
        private static void NoCartComfortWhileOff(List<Piece> pieces)
        {
            if (!Plugin.Enabled.Value)
                pieces.RemoveAll(p => p && p.GetComponent<CartRider>());
        }
    }
}
