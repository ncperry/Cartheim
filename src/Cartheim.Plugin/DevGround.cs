using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Cartheim.Plugin
{
    // In the dev world: the ground round the building the player stands at (or round them, away from any) put back as the
    // world made it, raised, lowered and painted (dug, paved,
    // cultivated) nowhere within reach.
    internal static class DevGround
    {
        private const float Reach = 15f;
        private const float BeyondBuilding = 15f;

        private static readonly AccessTools.FieldRef<TerrainComp, Heightmap> Map = AccessTools.FieldRefAccess<TerrainComp, Heightmap>("m_hmap");
        private static readonly AccessTools.FieldRef<TerrainComp, bool[]> HeightChanged = AccessTools.FieldRefAccess<TerrainComp, bool[]>("m_modifiedHeight");
        private static readonly AccessTools.FieldRef<TerrainComp, float[]> Levelled = AccessTools.FieldRefAccess<TerrainComp, float[]>("m_levelDelta");
        private static readonly AccessTools.FieldRef<TerrainComp, float[]> Smoothed = AccessTools.FieldRefAccess<TerrainComp, float[]>("m_smoothDelta");
        private static readonly AccessTools.FieldRef<TerrainComp, bool[]> PaintChanged = AccessTools.FieldRefAccess<TerrainComp, bool[]>("m_modifiedPaint");
        private static readonly AccessTools.FieldRef<TerrainComp, Color[]> Paint = AccessTools.FieldRefAccess<TerrainComp, Color[]>("m_paintMask");
        private static readonly System.Reflection.MethodInfo Save = AccessTools.Method(typeof(TerrainComp), "Save");

        public static void PutBack()
        {
            InventoryGui.instance?.Hide();
            var player = Player.m_localPlayer;
            if (!player)
                return;
            // The whole of the building's camp and a margin, wherever round it they stand.
            var building = Object.FindObjectsByType<Location>(FindObjectsSortMode.None)
                .Where(l => l && Flat(l.transform.position - player.transform.position) <= Mathf.Max(30f, l.m_exteriorRadius))
                .OrderBy(l => Flat(l.transform.position - player.transform.position)).FirstOrDefault();
            var here = building ? building.transform.position : player.transform.position;
            var reach = building ? building.m_exteriorRadius + BeyondBuilding : Reach;
            var heights = 0;
            var paints = 0;
            foreach (var comp in Object.FindObjectsByType<TerrainComp>(FindObjectsSortMode.None).Where(c => c))
            {
                var map = Map(comp);
                var changed = HeightChanged(comp);
                if (!map || changed == null)
                    continue;
                comp.GetComponent<ZNetView>()?.ClaimOwnership();
                var pitch = map.m_width + 1;
                var half = map.m_width / 2;
                var touched = false;
                for (var i = 0; i < changed.Length; i++)
                {
                    var at = map.transform.position + new Vector3((i % pitch - half) * map.m_scale, 0f, (i / pitch - half) * map.m_scale);
                    if (!changed[i] || Flat(at - here) > reach)
                        continue;
                    changed[i] = false;
                    Levelled(comp)[i] = 0f;
                    Smoothed(comp)[i] = 0f;
                    heights++;
                    touched = true;
                }
                var painted = PaintChanged(comp);
                var unpainted = new System.Collections.Generic.List<int>();
                for (var j = 0; painted != null && j < painted.Length; j++)
                {
                    if (!painted[j] || Flat(map.VertexMaskToWorld(j % pitch, j / pitch) - here) > reach)
                        continue;
                    painted[j] = false;
                    unpainted.Add(j);
                    paints++;
                    touched = true;
                }
                if (!touched)
                    continue;
                // Redrawn as the world made it, then each mark taken back to that (all of it: a cultivated mark keeps what else
                // the ground had there, so a cleared one would never show as soil again).
                map.Poke();
                foreach (var j in unpainted)
                    Paint(comp)[j] = map.GetPaintMask(j % pitch, j / pitch);
                Save.Invoke(comp, new object[] { false });
            }
            ClutterSystem.instance?.ResetGrass(here, reach);
            Debug.Log($"[Cartheim] Dev: the ground within {reach:0} m of {here} put back: {heights} heights, {paints} paint marks");
            player.Message(MessageHud.MessageType.Center, $"Ground within {reach:0} m put back as the world made it");
        }

        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }
}
