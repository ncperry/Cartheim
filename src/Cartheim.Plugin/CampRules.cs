using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Cartheim.Plugin
{
    // While a cart's camp stands at a building:
    // - everything built there, the camp's pieces and the building's own, takes no harm and can't be taken down by hammer;
    // - the food set out on its table (not the berries) is to look at only: not eaten, not taken, nothing said of it;
    // - the building's wild bees don't sting, nor for a minute after the camp's packed up;
    // - a night slept there leaves the wild beehive with honey to take.
    [HarmonyPatch]
    internal static class CampRules
    {
        // Where camps stand now: the building's middle, and how far round it the camp reaches.
        private static IEnumerable<(Vector3 at, float reach, CartBase cart)> Camps() =>
            CartBase.All.Where(c => c && c.InRuin).Select(c => (c.RuinPosition, c.Camp.Area + 2f, c));

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private static CartBase CampAt(Vector3 point) =>
            Camps().Where(c => Flat(point - c.at).magnitude <= c.reach).Select(c => c.cart).FirstOrDefault();

        // Indestructible while the camp stands: no harm, and the hammer can't take it.
        [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Damage)), HarmonyPrefix]
        private static bool NoHarmAtCamp(WearNTear __instance) => !CampAt(__instance.transform.position);

        [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.ApplyDamage)), HarmonyPrefix]
        private static bool NoWearAtCamp(WearNTear __instance, ref bool __result)
        {
            if (!CampAt(__instance.transform.position))
                return true;
            __result = false;
            return false;
        }

        [HarmonyPatch(typeof(Piece), nameof(Piece.CanBeRemoved)), HarmonyPostfix]
        private static void NotTakenAtCamp(Piece __instance, ref bool __result)
        {
            if (__result && CampAt(__instance.transform.position))
                __result = false;
        }

        // The table's food: not eaten, not taken, nothing said of it.
        [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.Interact)), HarmonyPrefix]
        private static bool NotTaken(ItemDrop __instance, ref bool __result) => Untouched(__instance, ref __result);

        [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.GetHoverText)), HarmonyPrefix]
        private static bool NotNamed(ItemDrop __instance, ref string __result) => Unsaid(__instance, ref __result);

        [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.GetHoverName)), HarmonyPrefix]
        private static bool NotNamedEither(ItemDrop __instance, ref string __result) => Unsaid(__instance, ref __result);

        [HarmonyPatch(typeof(Feast), nameof(Feast.Interact)), HarmonyPrefix]
        private static bool FeastNotEaten(Feast __instance, ref bool __result) => Untouched(__instance, ref __result);

        [HarmonyPatch(typeof(Feast), nameof(Feast.UseItem)), HarmonyPrefix]
        private static bool FeastNotServed(Feast __instance, ref bool __result) => Untouched(__instance, ref __result);

        [HarmonyPatch(typeof(Feast), nameof(Feast.GetHoverText)), HarmonyPrefix]
        private static bool FeastNotNamed(Feast __instance, ref string __result) => Unsaid(__instance, ref __result);

        [HarmonyPatch(typeof(Feast), nameof(Feast.GetHoverName)), HarmonyPrefix]
        private static bool FeastNotNamedEither(Feast __instance, ref string __result) => Unsaid(__instance, ref __result);

        private static bool Untouched(Component thing, ref bool result)
        {
            if (!CartRuin.IsUntouchable(thing))
                return true;
            result = false;
            return false;
        }

        private static bool Unsaid(Component thing, ref string result)
        {
            if (!CartRuin.IsUntouchable(thing))
                return true;
            result = "";
            return false;
        }

        // A wild beehive (the building's own) at a camp, or where one stood up to a minute ago, doesn't sting.
        private const string WildBees = "Beehive";
        private const float CalmFor = 60f;
        private static readonly List<(Vector3 at, float reach, float until)> Calmed = new List<(Vector3, float, float)>();

        public static void CalmAfterPacking(Vector3 at, float reach) => Calmed.Add((at, reach, Time.time + CalmFor));

        private static bool Calm(Vector3 point)
        {
            Calmed.RemoveAll(c => Time.time > c.until);
            return CampAt(point) || Calmed.Any(c => Flat(point - c.at).magnitude <= c.reach);
        }

        [HarmonyPatch(typeof(Aoe), "OnHit"), HarmonyPrefix]
        private static bool NoStings(Aoe __instance, Collider collider, ref bool __result)
        {
            var hive = __instance.GetComponentInParent<ZNetView>();
            if (!hive || Utils.GetPrefabName(hive.gameObject) != WildBees || !(collider.GetComponentInParent<Player>() is Player) || !Calm(__instance.transform.position))
                return true;
            __result = false;
            return false;
        }

        // Slept at a recorded camp: its wild beehive has honey to take in the morning.
        private const string HoneyKey = "cartheim_camp_honey";
        public const int HoneyAfterSleep = 4;

        [HarmonyPatch(typeof(Player), nameof(Player.SetSleeping)), HarmonyPrefix]
        private static void Waking(Player __instance, bool sleep, out bool __state) => __state = !sleep && __instance.IsSleeping();

        [HarmonyPatch(typeof(Player), nameof(Player.SetSleeping)), HarmonyPostfix]
        private static void Woken(Player __instance, bool __state)
        {
            if (!__state || __instance != Player.m_localPlayer)
                return;
            var cart = CampAt(__instance.transform.position);
            if (!cart || cart.Camp.Recorded == null)
                return;
            cart.GetComponent<ZNetView>().GetZDO().Set(HoneyKey, HoneyAfterSleep);
            Debug.Log($"[Cartheim] Slept at the camp: {HoneyAfterSleep} honey in its beehive");
        }

        // Every second, the wild beehives at recorded camps given honey to take (and taken back from those no longer at one).
        private static float _next;

        public static void Update()
        {
            if (Time.time < _next || !Plugin.Enabled.Value)
                return;
            _next = Time.time + 1f;
            // Found by their bees (a wild hive is no destructible thing, but built like a piece).
            foreach (var hive in Object.FindObjectsByType<Aoe>(FindObjectsSortMode.None)
                         .Select(a => a ? a.GetComponentInParent<ZNetView>() : null).Where(v => v && Utils.GetPrefabName(v.gameObject) == WildBees).Distinct())
            {
                var cart = CampAt(hive.transform.position);
                var honey = hive.GetComponent<CampHoney>();
                if (cart && cart.Camp.Recorded != null && !honey)
                    hive.gameObject.AddComponent<CampHoney>().Cart = cart;
                else if ((!cart || cart.Camp.Recorded == null) && honey)
                    Object.Destroy(honey);
            }
        }

        internal static int Honey(CartBase cart) => cart ? cart.GetComponent<ZNetView>().GetZDO().GetInt(HoneyKey) : 0;

        internal static void TakeHoney(CartBase cart) => cart.GetComponent<ZNetView>().GetZDO().Set(HoneyKey, 0);
    }

    // A camp's wild beehive, with honey to take after a night there.
    internal class CampHoney : MonoBehaviour, Hoverable, Interactable
    {
        public CartBase Cart;

        public string GetHoverText()
        {
            var honey = CampRules.Honey(Cart);
            return honey > 0 ? Localization.instance.Localize($"Beehive\n[<color=yellow><b>$KEY_Use</b></color>] Take honey ({honey})") : "Beehive";
        }

        public string GetHoverName() => "Beehive";

        public float GetHoverOffset() => 0f;

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            var honey = CampRules.Honey(Cart);
            if (hold || honey <= 0 || !(user is Player player))
                return false;
            player.GetInventory().AddItem("Honey", honey, 1, 0, 0L, "", cheated: false);
            CampRules.TakeHoney(Cart);
            player.Message(MessageHud.MessageType.Center, $"Took {honey} honey");
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;
    }
}
