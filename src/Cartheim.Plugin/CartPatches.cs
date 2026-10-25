using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace Cartheim.Plugin
{
    internal static class CartPatches
    {
        [HarmonyPatch(typeof(Vagon), "Awake"), HarmonyPostfix]
        private static void AddCartBase(Vagon __instance)
        {
            var nview = __instance.GetComponent<ZNetView>();
            if (nview && nview.GetZDO() != null && !__instance.GetComponent<CartBase>())
                __instance.gameObject.AddComponent<CartBase>();
        }

        // The alternate interact (Shift+E) opens the cart's crafting screen with all its storage open: at its workbench,
        // or at the Cart tab (its upgrades) before it has one. Plain E still hitches the cart.
        [HarmonyPatch(typeof(Vagon), nameof(Vagon.Interact)), HarmonyPrefix]
        private static bool OpenCartCrafting(Vagon __instance, Humanoid character, bool hold, bool alt, ref bool __result)
        {
            if (!alt || hold || !Plugin.Enabled.Value || character != Player.m_localPlayer)
                return true;
            var cart = __instance.GetComponent<CartBase>();
            var station = cart ? cart.Station(GamePieces.WorkbenchPiece) : null;
            if (!station && cart)
                station = cart.UpgradeStation;
            if (!station)
                return true;
            CartCrafting.Use(station);
            CartStorage.Open(cart);
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

        private static readonly System.Reflection.MethodInfo SetMass = AccessTools.Method(typeof(Vagon), "SetMass");

        // A full cart pulls like an empty one: its mass never grows with what it carries.
        [HarmonyPatch(typeof(Vagon), "UpdateMass"), HarmonyPrefix]
        private static bool KeepEmptyMass(Vagon __instance, ZNetView ___m_nview)
        {
            if (!Plugin.Enabled.Value)
                return true;
            if (___m_nview && ___m_nview.IsValid() && ___m_nview.IsOwner())
                SetMass.Invoke(__instance, new object[] { __instance.m_baseMass });
            return false;
        }

        // The cart's load (the sacks and crates on it) shows how much is built into it, not how many slots are filled.
        [HarmonyPatch(typeof(Vagon), "UpdateLoadVisualization"), HarmonyPrefix]
        private static bool ShowUpgradesAsLoad(Vagon __instance)
        {
            var cart = __instance.GetComponent<CartBase>();
            if (!Plugin.Enabled.Value || !cart)
                return true;
            // A cart showing its built pieces shows them alone.
            var fullness = cart.HasLooks ? 0f : cart.Upgrades.Fullness;
            foreach (var load in __instance.m_loadVis)
                load.m_gameobject.SetActive(fullness > 0f && fullness >= load.m_minPercentage);
            return false;
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

        // Near a roofed cart the player is under a roof: no rain soaks them, and the game counts them as sheltered.
        [HarmonyPatch(typeof(Player), "UpdateCover"), HarmonyPostfix]
        private static void ShelterUnderCartRoof(Player __instance, ref bool ___m_underRoof, ref float ___m_coverPercentage)
        {
            if (__instance != Player.m_localPlayer || ___m_underRoof && ___m_coverPercentage >= 0.8f)
                return;
            if (!CartFire.UnderCartRoof(__instance.transform.position))
                return;
            ___m_underRoof = true;
            ___m_coverPercentage = 1f;
        }

        // A fire near a roofed cart stays dry, so rain and wind don't put it out.
        [HarmonyPatch(typeof(Fireplace), "CheckWet"), HarmonyPostfix]
        private static void KeepFireDryUnderCartRoof(Fireplace __instance, ref bool ___m_wet)
        {
            if (___m_wet && CartFire.UnderCartRoof(__instance.transform.position))
                ___m_wet = false;
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

        private static readonly MethodInfo GetNetView = AccessTools.Method(typeof(Component), nameof(Component.GetComponent), new System.Type[0]).MakeGenericMethod(typeof(ZNetView));

        // Finishing a craft asks the station's network object whether it was cheated in; a cart station has none of its
        // own, so it answers with the cart's.
        [HarmonyPatch(typeof(InventoryGui), "DoCrafting"), HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> CartStationNetView(IEnumerable<CodeInstruction> code)
        {
            var replaced = 0;
            foreach (var instruction in code)
            {
                if (instruction.Calls(GetNetView))
                {
                    replaced++;
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CartPatches), nameof(NetViewOf)));
                }
                else
                    yield return instruction;
            }
            if (replaced == 0)
                Debug.LogWarning("[Cartheim] Found no station network check in DoCrafting; crafting at a cart may fail");
        }

        private static ZNetView NetViewOf(Component station)
        {
            var own = station.GetComponent<ZNetView>();
            return own ? own : station.GetComponentInParent<ZNetView>();
        }

        // The pieces standing in the cart's bed (the bed itself) sit inside the cart's own collider, which the player's look
        // lands on first; looking at the cart where one of its pieces is behind that, hover the piece.
        [HarmonyPatch(typeof(Player), "FindHoverObject"), HarmonyPostfix]
        private static void HoverCartPiece(Player __instance, ref GameObject hover)
        {
            if (!Plugin.Enabled.Value || !hover || !GameCamera.instance || hover.GetComponent<CartPieceHover>() || !hover.GetComponentInParent<CartBase>())
                return;
            var look = GameCamera.instance.transform;
            GameObject piece = null;
            var nearest = float.MaxValue;
            foreach (var hit in Physics.RaycastAll(look.position, look.forward, 50f, ~0, QueryTriggerInteraction.Collide))
                if (hit.distance < nearest && hit.collider.GetComponent<CartPieceHover>() &&
                    Vector3.Distance(__instance.m_eye.position, hit.point) < __instance.m_maxInteractDistance)
                {
                    nearest = hit.distance;
                    piece = hit.collider.gameObject;
                }
            if (piece)
                hover = piece;
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
