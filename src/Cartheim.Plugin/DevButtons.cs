using System;
using System.Collections.Generic;
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
            yield return new KeyValuePair<string, Action>("Item stands", CartRaft.BeginStands);
        }

        // Nothing spawns in the dev world: the game's spawners do nothing there.
        [HarmonyPatch(typeof(SpawnSystem), "UpdateSpawning"), HarmonyPrefix]
        private static bool NoMonsters() => !InDevWorld;
    }
}
