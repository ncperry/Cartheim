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
        // or at the Cart tab (its upgrades) before it has one. Plain E still hitches the cart, unless its tent is pitched.
        [HarmonyPatch(typeof(Vagon), nameof(Vagon.Interact)), HarmonyPrefix]
        private static bool OpenCartCrafting(Vagon __instance, Humanoid character, bool hold, bool alt, ref bool __result)
        {
            var cart = __instance.GetComponent<CartBase>();
            // With its tent pitched, the cart can't be hitched: pack the tent up first.
            if (!alt && cart && cart.TentUp)
            {
                if (!hold)
                    character.Message(MessageHud.MessageType.Center, "Pack up the tent first");
                __result = false;
                return false;
            }
            if (!alt || hold || !Plugin.Enabled.Value || character != Player.m_localPlayer)
                return true;
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

        private static float _loggedMass = -1f;

        // The cart pulls as if it carried a set, middling load (the "Pulls as if carrying" setting), whatever it holds.
        [HarmonyPatch(typeof(Vagon), "UpdateMass"), HarmonyPrefix]
        private static bool KeepSetMass(Vagon __instance, ZNetView ___m_nview)
        {
            if (!Plugin.Enabled.Value)
                return true;
            if (!___m_nview || !___m_nview.IsValid() || !___m_nview.IsOwner())
                return false;
            var mass = Cartheim.Core.CartMass.Pulled(__instance.m_baseMass, __instance.m_itemWeightMassFactor, Plugin.FeltLoad.Value);
            SetMass.Invoke(__instance, new object[] { mass });
            if (mass != _loggedMass)
            {
                _loggedMass = mass;
                var carried = __instance.m_container ? __instance.m_container.GetInventory().GetTotalWeight() : 0f;
                Debug.Log($"[Cartheim] Cart mass {mass} (base {__instance.m_baseMass}, {__instance.m_itemWeightMassFactor} per weight, " +
                          $"felt load {Plugin.FeltLoad.Value}; really carrying {carried}, which the game would make {__instance.m_baseMass + carried * __instance.m_itemWeightMassFactor})");
            }
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

        // Under a cart's pitched tent, or lying in the bed under a cart's roof, the player is under a roof: no rain soaks
        // them, and the game counts them as sheltered.
        [HarmonyPatch(typeof(Player), "UpdateCover"), HarmonyPostfix]
        private static void ShelterUnderCartTent(Player __instance, ref bool ___m_underRoof, ref float ___m_coverPercentage)
        {
            if (__instance != Player.m_localPlayer || ___m_underRoof && ___m_coverPercentage >= 0.8f)
                return;
            if (!CartFire.UnderCartTent(__instance.transform.position) && !InCartBedUnderRoof(__instance))
                return;
            ___m_underRoof = true;
            ___m_coverPercentage = 1f;
        }

        private static readonly AccessTools.FieldRef<Player, Transform> AttachPoint = AccessTools.FieldRefAccess<Player, Transform>("m_attachPoint");

        private static bool InCartBedUnderRoof(Player player)
        {
            if (!player.IsAttached())
                return false;
            var point = AttachPoint(player);
            var bed = point ? point.GetComponentInParent<Bed>() : null;
            if (!bed || !bed.GetComponent<CartRider>())
                return false;
            var cart = CartFire.CartNear(bed.transform.position);
            return cart && cart.HasRoof;
        }

        // A fire under a cart's pitched tent stays dry, so rain and wind don't put it out.
        [HarmonyPatch(typeof(Fireplace), "CheckWet"), HarmonyPostfix]
        private static void KeepFireDryUnderCartTent(Fireplace __instance, ref bool ___m_wet)
        {
            if (___m_wet && CartFire.UnderCartTent(__instance.transform.position))
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

        // A piece riding in a cart loses its wear and support check as it loads, before its cart has found it: floating in
        // the cart, the check would break it (a chest spilling what it holds). Its cart hides it within a second.
        [HarmonyPatch(typeof(WearNTear), "Awake"), HarmonyPostfix]
        private static void RidersNeedNoSupport(WearNTear __instance)
        {
            var zdo = __instance.GetComponent<ZNetView>()?.GetZDO();
            if (zdo != null && zdo.GetZDOID(CartRiders.CartKey) != ZDOID.None)
                Object.Destroy(__instance);
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
