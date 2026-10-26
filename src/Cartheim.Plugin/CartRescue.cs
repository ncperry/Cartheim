using System.Collections.Generic;
using System.Linq;
using Cartheim.Core;
using HarmonyLib;
using UnityEngine;

namespace Cartheim.Plugin
{
    // Dying at sea: the game respawns the player on land, and a cart afloat near where they died comes ashore beside them,
    // its raft packed away, rather than leaving their whole base out on the water (see SeaRescue; not on a hardcore world,
    // unless set to). Marked as they die, while the cart is still loaded; brought over as they respawn, by then usually far
    // from it: then the cart, its riders and its raft are moved (or the raft dropped) as saved objects, and the game loads
    // the cart where they now are.
    [HarmonyPatch]
    internal static class CartRescue
    {
        // On the cart: the player it's to come ashore to.
        private const string RescueKey = "cartheim_rescue_for";
        // How far from the player it comes ashore, and how far above the ground it's set down (to drop onto its wheels).
        private const float Beside = 4f;
        private const float SetDownAbove = 1.5f;
        // How long to wait for a brought cart's bed to load, to put the spawn point back on it.
        private const float BedWait = 60f;

        private static readonly Dictionary<long, float> WaitingForBed = new Dictionary<long, float>();

        private static readonly AccessTools.FieldRef<ZDOMan, Dictionary<ZDOID, ZDO>> Objects =
            AccessTools.FieldRefAccess<ZDOMan, Dictionary<ZDOID, ZDO>>("m_objectsByID");

        private static long Me => Game.instance?.GetPlayerProfile()?.GetPlayerID() ?? 0;

        private static bool Hardcore(out string preset) =>
            ZoneSystem.instance.GetGlobalKey(GlobalKeys.Preset, out preset) && SeaRescue.Hardcore(preset);

        [HarmonyPatch(typeof(Player), nameof(Player.OnDeath)), HarmonyPrefix]
        private static void MarkCartsAfloat(Player __instance)
        {
            if (__instance != Player.m_localPlayer || !Plugin.Enabled.Value || !ZoneSystem.instance || Me == 0)
                return;
            var afloat = CartBase.All.Where(c => c && c.Rafting &&
                SeaRescue.Near(Vector3.Distance(c.transform.position, __instance.transform.position))).ToList();
            if (afloat.Count == 0)
                return;
            var hardcore = Hardcore(out var preset);
            if (!SeaRescue.Comes(Plugin.AtSeaDeath.Value, hardcore))
            {
                Debug.Log($"[Cartheim] Died at sea; {afloat.Count} cart(s) afloat stay there ({Plugin.AtSeaDeath.Value}, world preset '{preset}')");
                return;
            }
            foreach (var cart in afloat)
            {
                var view = cart.GetComponent<ZNetView>();
                if (!view.IsOwner())
                    view.ClaimOwnership();
                view.GetZDO().Set(RescueKey, Me);
                Debug.Log($"[Cartheim] Died at sea; cart {cart.Uid()} will come ashore where the player respawns (world preset '{preset}')");
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned)), HarmonyPostfix]
        private static void BringCartsAshore(Player __instance)
        {
            if (__instance != Player.m_localPlayer || ZDOMan.instance == null || Me == 0)
                return;
            var zdos = new List<ZDO>();
            var index = 0;
            while (!ZDOMan.instance.GetAllZDOsWithPrefabIterative("Cart", zdos, ref index))
            {
            }
            var brought = 0;
            foreach (var zdo in zdos.Where(z => z.GetLong(RescueKey) == Me))
            {
                var (at, facing) = Spot(__instance, brought++);
                Bring(zdo, at, facing);
            }
            if (brought > 0)
                __instance.Message(MessageHud.MessageType.Center, brought == 1 ? "Your cart came ashore beside you" : "Your carts came ashore beside you");
        }

        // Dry ground beside the player, each cart a little further round.
        private static (Vector3 at, Quaternion facing) Spot(Player player, int nth)
        {
            var water = ZoneSystem.instance.m_waterLevel;
            var origin = player.transform.position;
            for (var step = 0; step < 8; step++)
            {
                var facing = Quaternion.Euler(0f, player.transform.eulerAngles.y + (nth * 3 + step) * 45f, 0f);
                var at = origin + facing * Vector3.forward * Beside;
                if (ZoneSystem.instance.GetSolidHeight(at, out var ground, 5) && Raft.Ashore(ground, water) && Mathf.Abs(ground - origin.y) < 3f)
                    return (new Vector3(at.x, ground, at.z), facing);
            }
            return (origin, player.transform.rotation);
        }

        private static void Bring(ZDO cartZdo, Vector3 at, Quaternion facing)
        {
            var uid = cartZdo.GetLong(CartBase.UidKey);
            var me = ZDOMan.GetSessionID();
            var loaded = CartBase.All.FirstOrDefault(c => c && c.Uid() == uid);
            if (loaded)
            {
                loaded.GoAshore(at, facing);
                loaded.GetComponent<ZNetView>().GetZDO().Set(RescueKey, 0L);
            }
            else
            {
                cartZdo.SetOwner(me);
                cartZdo.Set(RescueKey, 0L);
                cartZdo.Set(CartBase.RaftingKey, false);
                cartZdo.SetPosition(at + Vector3.up * SetDownAbove);
                cartZdo.SetRotation(facing);
                foreach (var zdo in Objects(ZDOMan.instance).Values.ToList())
                {
                    if (zdo.GetLong(CartRiders.UidKey) == uid)
                    {
                        zdo.SetOwner(me);
                        zdo.SetPosition(at + Vector3.up * SetDownAbove);
                        zdo.SetRotation(facing);
                    }
                    else if (zdo.GetLong(CartBase.RaftKey) == uid)
                    {
                        zdo.SetOwner(me);
                        ZDOMan.instance.DestroyZDO(zdo);
                    }
                }
            }
            WaitingForBed[uid] = Time.time;
            Debug.Log($"[Cartheim] Cart {uid} came ashore beside the respawned player, at {at}{(loaded ? "" : " (moved from afar)")}");
        }

        // Once a brought cart and its bed have loaded, the spawn point goes back on the bed if it was there.
        public static void Update()
        {
            if (WaitingForBed.Count == 0)
                return;
            foreach (var pair in WaitingForBed.ToList())
            {
                var (uid, since) = (pair.Key, pair.Value);
                var cart = CartBase.All.FirstOrDefault(c => c && c.Uid() == uid);
                if (cart && cart.RestoreBedSpawn())
                    WaitingForBed.Remove(uid);
                else if (Time.time - since > BedWait)
                {
                    Debug.LogWarning($"[Cartheim] Cart {uid}'s bed didn't load after it came ashore; the spawn point is left as it is");
                    WaitingForBed.Remove(uid);
                }
            }
        }
    }
}
