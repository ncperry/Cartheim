using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Cartheim.Plugin
{
    // For designing how a cart settles into a ruin: each ruin (a game location) within reach of the player is logged once,
    // with its kind, where it stands and how it's turned, and every solid part of it in its own space (so a ruin's layout
    // is the same wherever and however the game placed it).
    internal static class CartRuinSurvey
    {
        private const float Reach = 20f;
        private const float LookEvery = 2f;

        private static readonly AccessTools.FieldRef<List<Location>> AllLocations =
            AccessTools.StaticFieldRefAccess<List<Location>>(AccessTools.Field(typeof(Location), "s_allLocations"));

        private static readonly HashSet<Location> Logged = new HashSet<Location>();
        private static float _lookedAt;

        public static void Update()
        {
            var player = Player.m_localPlayer;
            if (!player || Time.time - _lookedAt < LookEvery)
                return;
            _lookedAt = Time.time;
            foreach (var location in AllLocations().Where(l => l && !Logged.Contains(l) &&
                                                                  Vector3.Distance(l.transform.position, player.transform.position) <= Reach))
            {
                Logged.Add(location);
                Log(location);
            }
        }

        private static void Log(Location location)
        {
            var root = location.transform;
            Debug.Log($"[Cartheim] Ruin survey: {root.name} at {root.position}, turned {root.eulerAngles.y:0.0}, noBuild {location.m_noBuild}, " +
                      $"exterior radius {location.m_exteriorRadius:0.0}, interior {location.m_hasInterior}");
            // A ruin's stones are spawned as objects of their own, not under it: everything solid within its radius (but the
            // ground, creatures and loose items), each object once, in the ruin's own space.
            var terrain = LayerMask.NameToLayer("terrain");
            var colliders = Physics.OverlapSphere(root.position, location.m_exteriorRadius + 2f, ~0, QueryTriggerInteraction.Ignore)
                .Where(c => c.enabled && c.gameObject.layer != terrain && !c.GetComponentInParent<Character>() && !c.GetComponentInParent<ItemDrop>())
                .ToList();
            foreach (var group in colliders.GroupBy(c => (c.GetComponentInParent<ZNetView>() ? c.GetComponentInParent<ZNetView>().transform : c.transform.root)))
            {
                var obj = group.Key;
                var bounds = group.Select(c => c.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                var corners = Corners(bounds).Select(root.InverseTransformPoint).ToList();
                var min = corners.Aggregate(Vector3.Min);
                var max = corners.Aggregate(Vector3.Max);
                Debug.Log($"[Cartheim] Ruin survey:   {obj.name} (layer {LayerMask.LayerToName(obj.gameObject.layer)}, {group.Count()} colliders): " +
                          $"at {root.InverseTransformPoint(obj.position)}, turned {Mathf.DeltaAngle(root.eulerAngles.y, obj.eulerAngles.y):0}, " +
                          $"spans {min} to {max}");
            }
        }

        private static IEnumerable<Vector3> Corners(Bounds b)
        {
            for (var i = 0; i < 8; i++)
                yield return new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
        }

        private static string Path(Transform t, Transform root)
        {
            var names = new List<string>();
            for (; t && t != root; t = t.parent)
                names.Add(t.name);
            names.Reverse();
            return names.Count > 0 ? string.Join("/", names) : root.name;
        }
    }
}
