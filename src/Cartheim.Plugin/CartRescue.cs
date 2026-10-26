using System.Collections.Generic;
using System.Linq;
using Cartheim.Core;
using HarmonyLib;
using UnityEngine;

namespace Cartheim.Plugin
{
    // Dying at sea: the game respawns the player on land, and a cart afloat near where they died washes ashore beside its
    // builder, its raft packed away, rather than leaving the whole base out on the water (see SeaRescue; not on a hardcore
    // world, unless set to). Not while anyone else is still alive near it: it stays with them.
    //
    // In multiplayer only the game of whoever died is sure to know the cart (it was near it), and only the builder's game
    // knows where the builder is. So the cart is marked as they die; that game then asks the builder's game, every so
    // often, where to put it; the builder's game, once the builder is alive (respawned, or back in the world), answers with
    // dry ground beside them; the cart, its riders and its raft are then moved (or the raft dropped) as saved objects, and
    // the builder is told. The cart is only ever moved, never made, so there's never a second one.
    [HarmonyPatch]
    internal static class CartRescue
    {
        // On the cart: who it washes ashore by, and who died near it (whose game brings it).
        private const string ComesToKey = "cartheim_rescue_for";
        private const string DiedKey = "cartheim_rescue_died";

        private const string AdriftRpc = "Cartheim_CartAdrift";
        private const string PutItRpc = "Cartheim_PutCartHere";
        private const string ArrivedRpc = "Cartheim_CartWashedAshore";

        // How far from the builder it comes ashore, and how far above the ground it's set down (to drop onto its wheels).
        private const float Beside = 4f;
        private const float SetDownAbove = 1.5f;
        // How often the builder is asked again (they may be dead, or not in the world yet), and how often marked carts are
        // looked for.
        private const float AskEvery = 15f;
        private const float LookEvery = 2f;
        // How long to wait for a brought cart's bed to load, to put the spawn point back on it.
        private const float BedWait = 60f;

        // In the bringing game: when each cart's builder was last asked.
        private static readonly Dictionary<long, float> Asked = new Dictionary<long, float>();
        // In the builder's game: carts to answer for once the builder is alive, and who asked.
        private static readonly Dictionary<long, long> ToAnswer = new Dictionary<long, long>();
        private static readonly Dictionary<long, float> WaitingForBed = new Dictionary<long, float>();
        private static float _lookedAt;
        private static ZRoutedRpc _registeredWith;

        private static readonly AccessTools.FieldRef<ZDOMan, Dictionary<ZDOID, ZDO>> Objects =
            AccessTools.FieldRefAccess<ZDOMan, Dictionary<ZDOID, ZDO>>("m_objectsByID");
        // The game's table of routed RPCs (its value type is private): an old copy's handlers are taken out of it.
        private static readonly System.Reflection.FieldInfo FunctionsField = AccessTools.Field(typeof(ZRoutedRpc), "m_functions");
        private static System.Collections.IDictionary Functions(ZRoutedRpc rpc) => (System.Collections.IDictionary)FunctionsField.GetValue(rpc);

        private static long Me => Game.instance?.GetPlayerProfile()?.GetPlayerID() ?? 0;

        private static bool Hardcore(out string preset) =>
            ZoneSystem.instance.GetGlobalKey(GlobalKeys.Preset, out preset) && SeaRescue.Hardcore(preset);

        [HarmonyPatch(typeof(Player), nameof(Player.OnDeath)), HarmonyPrefix]
        private static void MarkCartsAdrift(Player __instance)
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
                var othersNear = Player.GetAllPlayers().Count(p => p && p != __instance && !p.IsDead() &&
                    SeaRescue.Near(Vector3.Distance(p.transform.position, cart.transform.position)));
                if (!SeaRescue.Adrift(othersNear))
                {
                    Debug.Log($"[Cartheim] Died at sea; cart {cart.Uid()} stays with the {othersNear} player(s) still near it");
                    continue;
                }
                var view = cart.GetComponent<ZNetView>();
                if (!view.IsOwner())
                    view.ClaimOwnership();
                var zdo = view.GetZDO();
                var comesTo = SeaRescue.ComesTo(zdo.GetLong(ZDOVars.s_creator), Me);
                zdo.Set(ComesToKey, comesTo);
                zdo.Set(DiedKey, Me);
                Debug.Log($"[Cartheim] Died at sea; cart {cart.Uid()} is adrift, to wash ashore by player {comesTo} (world preset '{preset}')");
            }
        }

        // Respawned (or back in the world): answers for carts waiting on this player.
        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned)), HarmonyPostfix]
        private static void Respawned(Player __instance)
        {
            if (__instance == Player.m_localPlayer)
                AnswerWaiting();
        }

        public static void Update()
        {
            Register();
            if (Time.time - _lookedAt >= LookEvery)
            {
                _lookedAt = Time.time;
                AskForMarked();
                AnswerWaiting();
            }
            RestoreBedSpawns();
        }

        // Registered with each world's RPCs, replacing an older copy's (after a hot reload).
        private static void Register()
        {
            var rpc = ZRoutedRpc.instance;
            if (rpc == null || rpc == _registeredWith)
                return;
            _registeredWith = rpc;
            foreach (var name in new[] { AdriftRpc, PutItRpc, ArrivedRpc })
                Functions(rpc).Remove(name.GetStableHashCode());
            rpc.Register<long, long>(AdriftRpc, RPC_Adrift);
            rpc.Register<long, Vector3, Quaternion>(PutItRpc, RPC_PutIt);
            rpc.Register<long>(ArrivedRpc, RPC_Arrived);
        }

        public static void Unregister()
        {
            if (_registeredWith != null)
                foreach (var name in new[] { AdriftRpc, PutItRpc, ArrivedRpc })
                    Functions(_registeredWith).Remove(name.GetStableHashCode());
            _registeredWith = null;
        }

        private static IEnumerable<ZDO> MarkedCarts()
        {
            if (ZDOMan.instance == null)
                return Enumerable.Empty<ZDO>();
            var zdos = new List<ZDO>();
            var index = 0;
            while (!ZDOMan.instance.GetAllZDOsWithPrefabIterative("Cart", zdos, ref index))
            {
            }
            return zdos.Where(z => z.GetLong(ComesToKey) != 0);
        }

        // Whether a player is in the world, by their characters in the player list.
        private static bool Online(long player) =>
            player == Me || ZNet.instance.GetPlayerList().Any(p => ZDOMan.instance.GetZDO(p.m_characterID)?.GetLong(ZDOVars.s_playerID) == player);

        private static bool Brings(ZDO cart)
        {
            var died = cart.GetLong(DiedKey);
            return SeaRescue.Brings(died == Me && Me != 0, Online(died), ZNet.instance.IsServer());
        }

        // The bringing game asks the builder's game, every so often, where the cart is to go.
        private static void AskForMarked()
        {
            if (!ZNet.instance || ZRoutedRpc.instance == null)
                return;
            foreach (var cart in MarkedCarts().Where(Brings))
            {
                var uid = cart.GetLong(CartBase.UidKey);
                if (!cart.GetBool(CartBase.RaftingKey))
                {
                    Unmark(cart, "it's ashore already");
                    continue;
                }
                if (SeaRescue.WithSomeoneElse(cart.GetOwner(), ZDOMan.GetSessionID()) ||
                    Asked.TryGetValue(uid, out var at) && Time.time - at < AskEvery)
                    continue;
                Asked[uid] = Time.time;
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, AdriftRpc, cart.GetLong(ComesToKey), uid);
            }
        }

        private static void Unmark(ZDO cart, string why)
        {
            if (cart.GetOwner() != 0 && !cart.IsOwner())
                return;
            cart.SetOwner(ZDOMan.GetSessionID());
            cart.Set(ComesToKey, 0L);
            cart.Set(DiedKey, 0L);
            Asked.Remove(cart.GetLong(CartBase.UidKey));
            Debug.Log($"[Cartheim] Cart {cart.GetLong(CartBase.UidKey)} no longer adrift: {why}");
        }

        // The builder's game: asked where a cart is to go; answers once the builder is alive.
        private static void RPC_Adrift(long sender, long comesTo, long uid)
        {
            if (comesTo != Me || Me == 0)
                return;
            if (!ToAnswer.ContainsKey(uid))
                Debug.Log($"[Cartheim] Cart {uid} is adrift; it washes ashore by this player once they're alive");
            ToAnswer[uid] = sender;
            AnswerWaiting();
        }

        private static void AnswerWaiting()
        {
            var player = Player.m_localPlayer;
            if (ToAnswer.Count == 0 || !player || player.IsDead() || player.IsTeleporting() || !ZoneSystem.instance)
                return;
            foreach (var pair in ToAnswer.ToList())
            {
                var (at, facing) = Spot(player);
                ZRoutedRpc.instance.InvokeRoutedRPC(pair.Value, PutItRpc, pair.Key, at, facing);
                ToAnswer.Remove(pair.Key);
            }
        }

        // Dry ground beside the player, if there's any near.
        private static (Vector3 at, Quaternion facing) Spot(Player player)
        {
            var water = ZoneSystem.instance.m_waterLevel;
            var origin = player.transform.position;
            for (var step = 0; step < 8; step++)
            {
                var facing = Quaternion.Euler(0f, player.transform.eulerAngles.y + step * 45f, 0f);
                var at = origin + facing * Vector3.forward * Beside;
                if (ZoneSystem.instance.GetSolidHeight(at, out var ground, 5) && Raft.Ashore(ground, water) && Mathf.Abs(ground - origin.y) < 3f)
                    return (new Vector3(at.x, ground, at.z), facing);
            }
            return (origin, player.transform.rotation);
        }

        // The bringing game: told where, moves the cart there, if it's still adrift and nobody else is with it.
        private static void RPC_PutIt(long sender, long uid, Vector3 at, Quaternion facing)
        {
            var cart = MarkedCarts().FirstOrDefault(z => z.GetLong(CartBase.UidKey) == uid);
            if (cart == null || !Brings(cart))
                return;
            if (!cart.GetBool(CartBase.RaftingKey))
            {
                Unmark(cart, "it's ashore already");
                return;
            }
            if (SeaRescue.WithSomeoneElse(cart.GetOwner(), ZDOMan.GetSessionID()))
            {
                Debug.Log($"[Cartheim] Cart {uid} isn't brought: another player is with it");
                return;
            }
            Bring(cart, uid, at, facing);
            Unmark(cart, "it washed ashore");
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, ArrivedRpc, uid);
        }

        // The cart, every piece riding in it, set down there as saved objects (the game loads them there); its raft gone. A
        // cart this game still has loaded is put ashore as by hand first.
        private static void Bring(ZDO cartZdo, long uid, Vector3 at, Quaternion facing)
        {
            var me = ZDOMan.GetSessionID();
            var loaded = CartBase.All.FirstOrDefault(c => c && c.Uid() == uid);
            if (loaded)
                loaded.GoAshore(at, facing);
            cartZdo.SetOwner(me);
            cartZdo.Set(CartBase.RaftingKey, false);
            cartZdo.SetPosition(at + Vector3.up * SetDownAbove);
            cartZdo.SetRotation(facing);
            var riders = 0;
            foreach (var zdo in Objects(ZDOMan.instance).Values.ToList())
            {
                if (zdo.GetLong(CartRiders.UidKey) == uid)
                {
                    zdo.SetOwner(me);
                    zdo.SetPosition(at + Vector3.up * SetDownAbove);
                    zdo.SetRotation(facing);
                    riders++;
                }
                else if (zdo.GetLong(CartBase.RaftKey) == uid && zdo.IsValid())
                {
                    zdo.SetOwner(me);
                    ZDOMan.instance.DestroyZDO(zdo);
                }
            }
            Debug.Log($"[Cartheim] Cart {uid} washed ashore at {at} with its {riders} riding pieces{(loaded ? " (it was loaded here)" : "")}");
        }

        // The builder's game: the cart is beside them.
        private static void RPC_Arrived(long sender, long uid)
        {
            Player.m_localPlayer?.Message(MessageHud.MessageType.Center, "Your cart washed ashore beside you");
            WaitingForBed[uid] = Time.time;
        }

        // Once a brought cart and its bed have loaded, the spawn point goes back on the bed if it was there.
        private static void RestoreBedSpawns()
        {
            foreach (var pair in WaitingForBed.ToList())
            {
                var cart = CartBase.All.FirstOrDefault(c => c && c.Uid() == pair.Key);
                if (cart && cart.RestoreBedSpawn())
                    WaitingForBed.Remove(pair.Key);
                else if (Time.time - pair.Value > BedWait)
                {
                    Debug.LogWarning($"[Cartheim] Cart {pair.Key}'s bed didn't load after it washed ashore; the spawn point is left as it is");
                    WaitingForBed.Remove(pair.Key);
                }
            }
        }
    }
}
