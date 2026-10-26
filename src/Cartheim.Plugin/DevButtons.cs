using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Cartheim.Core;
using HarmonyLib;
using UnityEngine;

namespace Cartheim.Plugin
{
    // Cartheim's tools for the dev world (see DevWorld): buttons on Valheim Inventory's Dev tab, which finds this class by
    // its name; no monsters spawning there; and the player there unharmed and tireless.
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
            // The keys with modifiers first: Ctrl+F8 is F8 too.
            if (_tearDown.Value.IsDown())
            {
                if (!CartRaft.IsPlacing)
                    CartRaft.BeginTearDown();
            }
            else if (_clearForest.Value.IsDown())
            {
                if (!CartRaft.IsPlacing)
                    CartRaft.BeginClearForest();
            }
            else if (_placeBuilding.Value.IsDown() && !Held(KeyCode.LeftControl, KeyCode.RightControl, KeyCode.LeftShift, KeyCode.RightShift))
            {
                if (DevBuildings.IsOpen)
                    DevBuildings.Close();
                else if (!CartRaft.IsPlacing)
                    DevBuildings.Choose();
            }
        }

        private static bool Held(params KeyCode[] keys) => keys.Any(BepInEx.UnityInput.Current.GetKey);

        // Nothing spawns in the dev world: the game's spawners do nothing there, nor a building's own (the creature at a
        // spawn point, a nest's).
        [HarmonyPatch(typeof(SpawnSystem), "UpdateSpawning"), HarmonyPrefix]
        private static bool NoMonsters() => !InDevWorld;

        [HarmonyPatch(typeof(CreatureSpawner), "UpdateSpawner"), HarmonyPrefix]
        private static bool NoBuildingCreatures() => !InDevWorld;

        [HarmonyPatch(typeof(SpawnArea), "UpdateSpawn"), HarmonyPrefix]
        private static bool NoNestCreatures() => !InDevWorld;

        // The player in the dev world takes no harm (hits, falls, fire, poison) and spends no stamina.
        [HarmonyPatch(typeof(Character), nameof(Character.Damage)), HarmonyPrefix]
        private static bool Unharmed(Character __instance) => !(InDevWorld && __instance == Player.m_localPlayer);

        [HarmonyPatch(typeof(Player), nameof(Player.UseStamina)), HarmonyPrefix]
        private static bool Tireless(Player __instance) => !(InDevWorld && __instance == Player.m_localPlayer);
    }
}
