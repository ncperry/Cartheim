using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Cartheim.Plugin
{
    // In the dev world: the ground round the player put back as the world made it, raised, lowered and painted (dug, paved,
    // cultivated) nowhere within reach.
    internal static class DevGround
    {
        private const float Reach = 15f;

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
            var here = player.transform.position;
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
                    if (!changed[i] || Flat(at - here) > Reach)
                        continue;
                    changed[i] = false;
                    Levelled(comp)[i] = 0f;
                    Smoothed(comp)[i] = 0f;
                    heights++;
                    touched = true;
                }
                var painted = PaintChanged(comp);
                for (var j = 0; painted != null && j < painted.Length; j++)
                {
                    if (!painted[j] || Flat(map.VertexMaskToWorld(j % pitch, j / pitch) - here) > Reach)
                        continue;
                    painted[j] = false;
                    Paint(comp)[j] = Color.clear;
                    paints++;
                    touched = true;
                }
                if (!touched)
                    continue;
                Save.Invoke(comp, new object[] { false });
                map.Poke();
            }
            ClutterSystem.instance?.ResetGrass(here, Reach);
            Debug.Log($"[Cartheim] Dev: the ground within {Reach} m of {here} put back: {heights} heights, {paints} paint marks");
            player.Message(MessageHud.MessageType.Center, $"Ground within {Reach:0} m put back as the world made it");
        }

        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }
}
