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
            foreach (var collider in root.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger))
            {
                var b = collider.bounds;
                var min = root.InverseTransformPoint(b.min);
                var max = root.InverseTransformPoint(b.max);
                var mesh = (collider as MeshCollider)?.sharedMesh?.name;
                Debug.Log($"[Cartheim] Ruin survey:   {Path(collider.transform, root)} ({collider.GetType().Name}{(mesh != null ? " " + mesh : "")}, " +
                          $"layer {LayerMask.LayerToName(collider.gameObject.layer)}): local {collider.transform.localPosition}, turned {collider.transform.localEulerAngles.y:0}, " +
                          $"spans {Vector3.Min(min, max)} to {Vector3.Max(min, max)}");
            }
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
