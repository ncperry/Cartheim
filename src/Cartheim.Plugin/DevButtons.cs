using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Cartheim.Core;
using HarmonyLib;
using UnityEngine;

namespace Cartheim.Plugin
{
    // Cartheim's tools for the dev world (see DevWorld): buttons on Valheim Inventory's Dev tab, which finds this class by
    // its name; and no monsters spawning there.
    [HarmonyPatch]
    public static class DevButtons
    {
        public static bool InDevWorld => ZNet.instance && DevWorld.Is(ZNet.instance.GetWorldName());

        public static IEnumerable<KeyValuePair<string, Action>> Buttons()
        {
            if (!InDevWorld || !Plugin.Enabled.Value)
                yield break;
            foreach (var biome in new[] { Biome.BlackForest, Biome.Swamp, Biome.Mountains, Biome.Plains })
                yield return new KeyValuePair<string, Action>($"{DevCarts.Named(biome)} cart", () => DevCarts.Spawn(biome));
            yield return new KeyValuePair<string, Action>("Place a building", DevBuildings.Choose);
            yield return new KeyValuePair<string, Action>("Item stands", CartRaft.BeginStands);
        }

        // In the dev world, a key for placing a building (opens the biome panel; again, closes it).
        private static ConfigEntry<KeyboardShortcut> _placeBuilding;

        public static void Bind(ConfigFile config) =>
            _placeBuilding = config.Bind("Dev", "Place a building", new KeyboardShortcut(KeyCode.F8),
                "In the dev world: opens the panel for placing one of the game's buildings, by biome.");

        public static void Update()
        {
            if (_placeBuilding == null || !_placeBuilding.Value.IsDown() || !InDevWorld || !Plugin.Enabled.Value)
                return;
            if (DevBuildings.IsOpen)
                DevBuildings.Close();
            else if (!CartRaft.IsPlacing)
                DevBuildings.Choose();
        }

        // Nothing spawns in the dev world: the game's spawners do nothing there.
        [HarmonyPatch(typeof(SpawnSystem), "UpdateSpawning"), HarmonyPrefix]
        private static bool NoMonsters() => !InDevWorld;
    }
}
