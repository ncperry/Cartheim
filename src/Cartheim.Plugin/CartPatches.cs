using HarmonyLib;
using UnityEngine;

namespace Cartheim.Plugin
{
    internal static class CartPatches
    {
        // Opens the cart panel; set by the plugin.
        public static System.Action<CartBase> OpenPanel;

        [HarmonyPatch(typeof(Vagon), "Awake"), HarmonyPostfix]
        private static void AddCartBase(Vagon __instance)
        {
            var nview = __instance.GetComponent<ZNetView>();
            if (nview && nview.GetZDO() != null && !__instance.GetComponent<CartBase>())
                __instance.gameObject.AddComponent<CartBase>();
        }

        // The alternate interact (Shift+E) opens the cart panel; plain E still hitches the cart.
        [HarmonyPatch(typeof(Vagon), nameof(Vagon.Interact)), HarmonyPrefix]
        private static bool OpenCartPanel(Vagon __instance, Humanoid character, bool hold, bool alt, ref bool __result)
        {
            if (!alt || hold || !Plugin.Enabled.Value || character != Player.m_localPlayer)
                return true;
            var cart = __instance.GetComponent<CartBase>();
            if (!cart)
                return true;
            OpenPanel?.Invoke(cart);
            __result = true;
            return false;
        }

        [HarmonyPatch(typeof(Vagon), nameof(Vagon.GetHoverText)), HarmonyPostfix]
        private static void ShowCartKey(Vagon __instance, ref string __result)
        {
            if (!Plugin.Enabled.Value || !__instance.GetComponent<CartBase>())
                return;
            __result += Localization.instance.Localize("\n[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>] Cart");
        }

        private static readonly AccessTools.FieldRef<CraftingStation, bool> HaveFire =
            AccessTools.FieldRefAccess<CraftingStation, bool>("m_haveFire");

        // A cart station that needs fire (the cauldron) has it when a fire burns near the cart, not right under the station.
        [HarmonyPatch(typeof(CraftingStation), "CheckFire"), HarmonyPostfix]
        private static void CartStationFire(CraftingStation __instance)
        {
            var marker = __instance.GetComponent<CartStation>();
            if (marker && marker.Cart)
                HaveFire(__instance) = CartFire.Near(marker.Cart.transform.position);
        }

        // Forge levels built into a cart soften every hit on it, the way armor softens hits on a player.
        [HarmonyPatch(typeof(WearNTear), "RPC_Damage"), HarmonyPrefix]
        private static void CartArmor(WearNTear __instance, HitData hit)
        {
            if (!Plugin.Enabled.Value || hit == null)
                return;
            var cart = __instance.GetComponent<CartBase>();
            if (!cart || cart.Upgrades.Armor <= 0f)
                return;
            var before = hit.GetTotalDamage();
            hit.ApplyArmor(cart.Upgrades.Armor);
            Debug.Log($"[Cartheim] Cart armor {cart.Upgrades.Armor}: hit {before:0.#} -> {hit.GetTotalDamage():0.#}");
        }

        // A station riding on a cart has no extensions standing around it: its level is what's built into the cart.
        [HarmonyPatch(typeof(CraftingStation), nameof(CraftingStation.GetExtentionCount)), HarmonyPostfix]
        private static void CartStationLevel(CraftingStation __instance, ref int __result)
        {
            var marker = __instance.GetComponent<CartStation>();
            if (marker && marker.Cart)
                __result = Mathf.Max(0, marker.Cart.Upgrades.Level(marker.Piece) - 1);
        }
    }
}
