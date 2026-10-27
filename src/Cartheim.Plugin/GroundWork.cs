using System.Collections.Generic;
using System.Linq;
using Cartheim.Core;
using HarmonyLib;
using UnityEngine;

namespace Cartheim.Plugin
{
    // The ground's own record of how it's been changed (raised, lowered, painted), worked on directly: flattened round a
    // building at its base (see FlatGround), or put back as the world made it.
    internal static class GroundWork
    {
        internal static readonly AccessTools.FieldRef<TerrainComp, Heightmap> Map = AccessTools.FieldRefAccess<TerrainComp, Heightmap>("m_hmap");
        internal static readonly AccessTools.FieldRef<TerrainComp, bool[]> HeightChanged = AccessTools.FieldRefAccess<TerrainComp, bool[]>("m_modifiedHeight");
        internal static readonly AccessTools.FieldRef<TerrainComp, float[]> Levelled = AccessTools.FieldRefAccess<TerrainComp, float[]>("m_levelDelta");
        internal static readonly AccessTools.FieldRef<TerrainComp, float[]> Smoothed = AccessTools.FieldRefAccess<TerrainComp, float[]>("m_smoothDelta");
        internal static readonly AccessTools.FieldRef<TerrainComp, bool[]> PaintChanged = AccessTools.FieldRefAccess<TerrainComp, bool[]>("m_modifiedPaint");
        internal static readonly AccessTools.FieldRef<TerrainComp, Color[]> Paint = AccessTools.FieldRefAccess<TerrainComp, Color[]>("m_paintMask");
        private static readonly System.Reflection.MethodInfo SaveMethod = AccessTools.Method(typeof(TerrainComp), "Save");

        internal static void Save(TerrainComp comp) => SaveMethod.Invoke(comp, new object[] { false });

        // A height vertex's place in the world.
        internal static Vector3 Vertex(Heightmap map, int i)
        {
            var pitch = map.m_width + 1;
            var half = map.m_width / 2;
            return map.transform.position + new Vector3((i % pitch - half) * map.m_scale, 0f, (i / pitch - half) * map.m_scale);
        }

        // The ground in the area round a building made flat at the building's base, blending back to its own round it:
        // how many points of it were changed.
        public static int Flatten(Location building, FlatGround.Area area)
        {
            var origin = building.transform.position;
            var toBuilding = Quaternion.Inverse(Quaternion.Euler(0f, building.transform.eulerAngles.y, 0f));
            var reach = Mathf.Sqrt(area.Width * area.Width + area.Depth * area.Depth) / 2f + new Vector2(area.X, area.Z).magnitude + FlatGround.Margin;
            var changed = 0;
            // Each piece of the ground there, with a record of its own changes (made if it has none yet).
            var grounds = new List<Heightmap>();
            Heightmap.FindHeightmap(origin, reach, grounds);
            foreach (var map in grounds)
            {
                var comp = map.GetAndCreateTerrainCompiler();
                var view = comp ? comp.GetComponent<ZNetView>() : null;
                if (!view || !view.IsValid())
                    continue;
                if (!view.IsOwner())
                    view.ClaimOwnership();
                var heights = HeightChanged(comp);
                if (heights == null)
                    continue;
                var touched = false;
                for (var i = 0; i < heights.Length; i++)
                {
                    var at = Vertex(map, i);
                    var local = toBuilding * (at - origin);
                    var pitch = map.m_width + 1;
                    var now = map.transform.position.y + map.GetHeight(i % pitch, i / pitch);
                    // Its own height: as it is, less what's been done to it.
                    var natural = heights[i] ? now - Levelled(comp)[i] - Smoothed(comp)[i] : now;
                    var flat = FlatGround.Height(area, local.x, local.z, natural, origin.y);
                    if (flat == null)
                        continue;
                    heights[i] = true;
                    Levelled(comp)[i] = Mathf.Clamp(flat.Value - natural, -8f, 8f);
                    Smoothed(comp)[i] = 0f;
                    changed++;
                    touched = true;
                }
                if (!touched)
                    continue;
                Save(comp);
                map.Poke();
            }
            ClutterSystem.instance?.ResetGrass(origin, reach);
            Debug.Log($"[Cartheim] The ground round the {building.name.Replace("(Clone)", "")} flattened at its base ({origin.y:0.00}): " +
                      $"{area.Width:0.0} x {area.Depth:0.0} m about {area.X:0.0},{area.Z:0.0} in its space, {changed} points");
            return changed;
        }
    }
}
