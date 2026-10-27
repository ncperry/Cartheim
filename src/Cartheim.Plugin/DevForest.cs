using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Cartheim.Plugin
{
    // In the dev world, no trees round the game's buildings: cleared from the world's records (loaded near the player or
    // not), and from land as it first grows, so a long line of buildings is clear end to end. Trees are the forest pieces of
    // DevClearing (trees, logs, stumps, bushes), never a player's.
    [HarmonyPatch]
    internal static class DevForest
    {
        private static readonly AccessTools.FieldRef<ZDOMan, Dictionary<ZDOID, ZDO>> Objects =
            AccessTools.FieldRefAccess<ZDOMan, Dictionary<ZDOID, ZDO>>("m_objectsByID");
        private static readonly System.Reflection.MethodInfo FindObjects = AccessTools.Method(typeof(ZDOMan), "FindObjects");

        // Every record in the world, loaded near the player or not.
        public static List<ZDO> Records() => Objects(ZDOMan.instance).Values.ToList();

        // Each building's middle, and how far round it's cleared: its own reach and a margin.
        private static List<(Vector3 at, float reach)> Buildings(IEnumerable<ZDO> records)
        {
            var proxy = ZoneSystem.instance.m_locationProxyPrefab.name.GetStableHashCode();
            var reaches = ZoneSystem.instance.m_locations.Where(l => l != null && l.m_prefab.IsValid)
                .GroupBy(l => l.m_prefab.Name.GetStableHashCode())
                .ToDictionary(g => g.Key, g => Mathf.Max(g.First().m_exteriorRadius, g.First().m_interiorRadius));
            return records.Where(z => z.GetPrefab() == proxy)
                .Select(z => reaches.TryGetValue(z.GetInt(ZDOVars.s_location), out var reach) ? (z.GetPosition(), reach + CartRaft.ClearMargin) : (Vector3.zero, 0f))
                .Where(b => b.Item2 > 0f).ToList();
        }

        // The prefabs that are forest, by the same tests as a loaded one.
        private static HashSet<int> _forest;

        private static HashSet<int> Forest() =>
            _forest ?? (_forest = new HashSet<int>(ZNetScene.instance.m_prefabs.Where(p => p && p.GetComponent<ZNetView>() && CartRaft.IsForestPrefab(p))
                .Select(p => p.name.GetStableHashCode())));

        // Every building's surroundings near the player cleared, as far as the world has grown.
        public static int ClearRoundBuildings(Vector3 near, float within, out int buildings)
        {
            var all = Objects(ZDOMan.instance).Values.ToList();
            var round = Buildings(all).Where(b => Flat(b.at - near).magnitude <= within).ToList();
            buildings = round.Count;
            return Clear(all, round);
        }

        private static int Clear(IEnumerable<ZDO> records, List<(Vector3 at, float reach)> buildings)
        {
            if (buildings.Count == 0)
                return 0;
            var forest = Forest();
            var me = ZDOMan.GetSessionID();
            var gone = 0;
            foreach (var zdo in records.Where(z => z.IsValid() && forest.Contains(z.GetPrefab())).ToList())
            {
                var at = zdo.GetPosition();
                if (!buildings.Any(b => Flat(at - b.at).magnitude <= b.reach))
                    continue;
                var loaded = ZNetScene.instance.FindInstance(zdo);
                zdo.SetOwner(me);
                if (loaded)
                    ZNetScene.instance.Destroy(loaded.gameObject);
                else
                    ZDOMan.instance.DestroyZDO(zdo);
                gone++;
            }
            return gone;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        // Land growing for the first time: its trees round any building cleared at once (its own records and its
        // neighbours', for a building standing over the edge).
        [HarmonyPatch(typeof(ZoneSystem), "SpawnZone"), HarmonyPostfix]
        private static void ClearNewLand(Vector2s zoneID, ZoneSystem.SpawnMode mode, bool __result)
        {
            if (!__result || mode != ZoneSystem.SpawnMode.Full || !DevButtons.InDevWorld || !Plugin.Enabled.Value || FindObjects == null)
                return;
            var near = new List<ZDO>();
            var visited = new HashSet<ZoneSystem.SectorIndex>();
            for (var x = -1; x <= 1; x++)
                for (var y = -1; y <= 1; y++)
                    FindObjects.Invoke(ZDOMan.instance, new object[] { new Vector2s((short)(zoneID.x + x), (short)(zoneID.y + y)), near, visited });
            var gone = Clear(near, Buildings(near));
            if (gone > 0)
                Debug.Log($"[Cartheim] Dev: new land at zone {zoneID.x},{zoneID.y}: cleared {gone} trees round its buildings");
        }
    }
}
