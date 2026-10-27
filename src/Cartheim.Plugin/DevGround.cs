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

        private static Heightmap Map(TerrainComp comp) => GroundWork.Map(comp);
        private static bool[] HeightChanged(TerrainComp comp) => GroundWork.HeightChanged(comp);
        private static float[] Levelled(TerrainComp comp) => GroundWork.Levelled(comp);
        private static float[] Smoothed(TerrainComp comp) => GroundWork.Smoothed(comp);
        private static bool[] PaintChanged(TerrainComp comp) => GroundWork.PaintChanged(comp);
        private static Color[] Paint(TerrainComp comp) => GroundWork.Paint(comp);

        public static void PutBack()
        {
            InventoryGui.instance?.Hide();
            var player = Player.m_localPlayer;
            if (!player)
                return;
            // The whole of the building's camp and a margin, wherever round it they stand.
            var building = BuildingAt(player);
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
                GroundWork.Save(comp);
            }
            ClutterSystem.instance?.ResetGrass(here, reach);
            building?.GetComponentInParent<LocationProxy>()?.GetComponent<ZNetView>()?.GetZDO()?.Set(FlattenedKey, "");
            Debug.Log($"[Cartheim] Dev: the ground within {reach:0} m of {here} put back: {heights} heights, {paints} paint marks");
            player.Message(MessageHud.MessageType.Center, $"Ground within {reach:0} m put back as the world made it");
        }

        // The ground round the building the player stands at made flat at its base, out past the building by a margin
        // each way; the building keeps the area, for its camp's record (see DevCampRecord).
        private const float FlatBeyondBuilding = 4f;
        internal const string FlattenedKey = "cartheim_flattened";

        public static void Flatten()
        {
            InventoryGui.instance?.Hide();
            var player = Player.m_localPlayer;
            var building = player ? BuildingAt(player) : null;
            if (!building)
            {
                player?.Message(MessageHud.MessageType.Center, "Stand at a building to flatten the ground round it");
                return;
            }
            var side = 2f * (building.m_exteriorRadius + FlatBeyondBuilding);
            var area = new Cartheim.Core.FlatGround.Area(0f, 0f, side, side);
            var points = GroundWork.Flatten(building, area);
            building.GetComponentInParent<LocationProxy>()?.GetComponent<ZNetView>()?.GetZDO()?.Set(FlattenedKey, Cartheim.Core.FlatGround.Line(area));
            player.Message(MessageHud.MessageType.Center, $"Ground flattened {side:0} m square round the building, at its base ({points} points)");
        }

        // The building the player stands at (its area holds them), else the nearest within 30 m.
        private static Location BuildingAt(Player player) =>
            Object.FindObjectsByType<Location>(FindObjectsSortMode.None)
                .Where(l => l && Flat(l.transform.position - player.transform.position) <= Mathf.Max(30f, l.m_exteriorRadius))
                .OrderBy(l => Flat(l.transform.position - player.transform.position)).FirstOrDefault();

        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }
}
