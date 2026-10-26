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
            yield return new KeyValuePair<string, Action>("Tear down", CartRaft.BeginTearDown);
            yield return new KeyValuePair<string, Action>("Clear forest", CartRaft.BeginClearForest);
        }

        // In the dev world, a key for placing a building (opens the biome panel; again, closes it).
        private static ConfigEntry<KeyboardShortcut> _placeBuilding;
        private static ConfigEntry<KeyboardShortcut> _tearDown;
        private static ConfigEntry<KeyboardShortcut> _clearForest;

        public static void Bind(ConfigFile config)
        {
            _placeBuilding = config.Bind("Dev", "Place a building", new KeyboardShortcut(KeyCode.F8),
                "In the dev world: opens the panel for placing one of the game's buildings, by biome.");
            _tearDown = config.Bind("Dev", "Tear down a building", new KeyboardShortcut(KeyCode.F8, KeyCode.LeftShift),
                "In the dev world: tear down the building you aim at (all it spawned), nothing dropped.");
            _clearForest = config.Bind("Dev", "Clear a forest", new KeyboardShortcut(KeyCode.F8, KeyCode.LeftControl),
                "In the dev world: clear the trees, logs, stumps and bushes in a ring where you aim.");
        }

        public static void Update()
        {
            if (_placeBuilding == null || !InDevWorld || !Plugin.Enabled.Value)
                return;
            if (_placeBuilding.Value.IsDown())
            {
                if (DevBuildings.IsOpen)
                    DevBuildings.Close();
                else if (!CartRaft.IsPlacing)
                    DevBuildings.Choose();
            }
            else if (_tearDown.Value.IsDown() && !CartRaft.IsPlacing)
                CartRaft.BeginTearDown();
            else if (_clearForest.Value.IsDown() && !CartRaft.IsPlacing)
                CartRaft.BeginClearForest();
        }

        // Nothing spawns in the dev world: the game's spawners do nothing there.
        [HarmonyPatch(typeof(SpawnSystem), "UpdateSpawning"), HarmonyPrefix]
        private static bool NoMonsters() => !InDevWorld;
    }
}
