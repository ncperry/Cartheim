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

        // How far from the middle of the cart a player getting out of its bed is put down: clear of its sides, or its back.
        private const float GetOutDistance = 1.6f;
        private const float GetOutBehind = 2f;

        // Getting out of bed puts the player where they lay, a little higher: inside a cart, that's inside the cart's own
        // body, where they get stuck. Out of a cart's bed, they stand under its pitched tent, or else on the ground beside the
        // cart.
        [HarmonyPatch(typeof(Player), nameof(Player.AttachStop)), HarmonyPrefix]
        private static void LeavingCartBed(Player __instance, out CartBase __state)
        {
            var point = AttachPoint(__instance);
            var bed = point ? point.GetComponentInParent<Bed>() : null;
            __state = bed && IsCartPiece(bed) ? CartFire.CartNear(bed.transform.position) : null;
            // Settled in a ruin, the bed stands in the room by itself: they get out of it as out of any bed.
            if (__state && __state.Camp?.HasRoom == true)
                __state = null;
        }

        [HarmonyPatch(typeof(Player), nameof(Player.AttachStop)), HarmonyPostfix]
        private static void StepOutBesideCart(Player __instance, CartBase __state)
        {
            if (!__state || __instance.IsAttached())
                return;
            PutBeside(__instance, __state, "Out of the cart's bed");
        }

        // A player who's got into a roofed cart's body (jumping in over its side) can't jump out under the roof: after a
        // moment in there, not in its bed, they're put down beside it as from its bed.
        private const float InsideBefore = 1f;
        private static CartBase _insideOf;
        private static float _insideSince;

        internal static void LiftOutIfInside(CartBase cart)
        {
            var player = Player.m_localPlayer;
            var inside = player && cart.HasRoof && !player.IsAttached() && !player.IsDead() && Inside(cart.transform, player.transform.position);
            if (!inside)
            {
                if (_insideOf == cart)
                    _insideOf = null;
                return;
            }
            if (_insideOf != cart)
            {
                _insideOf = cart;
                _insideSince = Time.time;
            }
            else if (Time.time - _insideSince >= InsideBefore)
            {
                _insideOf = null;
                PutBeside(player, cart, "Stuck in the cart's body");
            }
        }

        // Within the cart's body, above its floor: its bed, between its sides, under its roof.
        private static bool Inside(Transform cart, Vector3 feet)
        {
            var local = cart.InverseTransformPoint(feet);
            return Mathf.Abs(local.x) < 0.8f && local.z > -1.3f && local.z < 1.3f && local.y > 0.3f && local.y < 2f;
        }

        private static void PutBeside(Player player, CartBase cartBase, string why)
        {
            var cart = cartBase.transform;
            // The side the player was facing first, so they get out where they look; then the other side, then behind.
            var facing = Vector3.Dot(player.transform.forward, cart.right) >= 0f ? cart.right : -cart.right;
            var spots = new[] { facing * GetOutDistance, -facing * GetOutDistance, -cart.forward * GetOutBehind };
            var spot = cart.position + spots[0];
            // With the tent pitched, they wake up under it.
            if (cartBase.TentFloor is Vector3 tent)
                spot = new Vector3(tent.x, StandingHeight(tent, tent.y + LookFromAboveTent), tent.z);
            else
                foreach (var offset in spots)
                {
                    var candidate = cart.position + offset;
                    candidate.y = StandingHeight(candidate, cart.position.y + LookFromAboveCart);
                    if (Clear(candidate, cart))
                    {
                        spot = candidate;
                        break;
                    }
                }
            // Moved through the body too: moving only the transform, the physics body can put the player back.
            player.transform.position = spot;
            var body = Body(player);
            if (body)
            {
                body.position = spot;
                body.linearVelocity = Vector3.zero;
            }
            Debug.Log($"[Cartheim] {why} (cart at {cart.position}), put beside it at {spot}");
            cartBase.StartCoroutine(ReportWhereTheyStand(player, spot));
        }

        // Where to look down from for what to stand on: above the cart's middle, under any tree; above the tent's floor,
        // under its canopy.
        private const float LookFromAboveCart = 2f;
        private const float LookFromAboveTent = 1f;

        // The height of what a player would stand on there: rock, a floor or the ground, whichever is highest below where
        // it's looked for from. The ground alone runs on under rocks, which put them inside one.
        private static float StandingHeight(Vector3 at, float lookFrom)
        {
            if (!ZoneSystem.instance)
                return at.y;
            var ground = ZoneSystem.instance.GetGroundHeight(at);
            return ZoneSystem.instance.GetSolidHeight(new Vector3(at.x, lookFrom, at.z), out var solid, 0) ? Mathf.Max(ground, solid) : ground;
        }

        private static readonly AccessTools.FieldRef<Character, Rigidbody> Body = AccessTools.FieldRefAccess<Character, Rigidbody>("m_body");

        // Somewhere a player can stand without being inside the cart: none of the cart's own solid colliders there.
        private static bool Clear(Vector3 feet, Transform cart)
        {
            foreach (var collider in Physics.OverlapCapsule(feet + Vector3.up * 0.5f, feet + Vector3.up * 1.6f, 0.4f, ~0, QueryTriggerInteraction.Ignore))
                if (collider.transform.IsChildOf(cart))
                    return false;
            return true;
        }

        // Half a second later: where the player actually is, in case something moved them back.
        private static System.Collections.IEnumerator ReportWhereTheyStand(Player player, Vector3 putAt)
        {
            yield return new WaitForSeconds(0.5f);
            if (player)
                Debug.Log($"[Cartheim] Half a second out of the cart's bed, the player stands at {player.transform.position} ({Vector3.Distance(player.transform.position, putAt):0.00} m from where they were put)");
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
            __result += cart.Upgrades.ExtraComfort(hearth ? hearth.m_comfort : 0, bestFire, cart.EngineBurning);
        }

        private static readonly AccessTools.FieldRef<Fermenter, bool> Exposed = AccessTools.FieldRefAccess<Fermenter, bool>("m_exposed");
        private static readonly AccessTools.FieldRef<Fermenter, bool> HasRoof = AccessTools.FieldRefAccess<Fermenter, bool>("m_hasRoof");
        private static readonly System.Reflection.MethodInfo ResetFermentation =
            AccessTools.Method(typeof(Fermenter), "ResetFermentationTimer") ?? throw new System.MissingMethodException("Fermenter", "ResetFermentationTimer");

        // A cart's fermenter is under cover when the cart has a roof (the cart's roof is only looks, so the game's own cover
        // check finds the sky). Without one it waits, as any fermenter in the open does.
        [HarmonyPatch(typeof(Fermenter), "UpdateCover"), HarmonyPrefix]
        private static bool CoveredByCartRoof(Fermenter __instance)
        {
            if (!IsCartPiece(__instance))
                return true;
            var cart = CartFire.CartNear(__instance.transform.position);
            var covered = cart && cart.HasRoof;
            Exposed(__instance) = !covered;
            HasRoof(__instance) = covered;
            // As the game does: in the open, brewing starts over.
            var nview = __instance.GetComponent<ZNetView>();
            if (!covered && nview && nview.IsValid() && nview.IsOwner())
                ResetFermentation.Invoke(__instance, null);
            return false;
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
