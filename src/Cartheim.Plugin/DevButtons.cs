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
            yield return new KeyValuePair<string, Action>("Clear round buildings", CartRaft.ClearRoundBuildings);
            yield return new KeyValuePair<string, Action>("Fix placed buildings", DevBuildings.PutBackOnGround);
            yield return new KeyValuePair<string, Action>("Record camp", DevCampRecord.Record);
            yield return new KeyValuePair<string, Action>("Copy building to the side", DevCampRecord.CopyToTheSide);
            yield return new KeyValuePair<string, Action>("Write building templates", DevTemplates.Dump);
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

        // The dev world stays at midday (as the game's own time-of-day pin holds it); anywhere else, the day turns.
        private static bool _heldAtNoon;

        private static void KeepDaytime()
        {
            var env = EnvMan.instance;
            if (!env)
                return;
            var hold = InDevWorld && Plugin.Enabled.Value;
            if (hold)
            {
                env.m_debugTimeOfDay = true;
                env.m_debugTime = 0.5f;
                _heldAtNoon = true;
            }
            else if (_heldAtNoon)
            {
                env.m_debugTimeOfDay = false;
                _heldAtNoon = false;
            }
        }

        public static void Update()
        {
            KeepDaytime();
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

        // Building in the dev world: everything buildable by the end of the Plains shows in the build menus (whether its
        // materials are known or not), and costs nothing to place. It still needs its station near, as anywhere.
        private static readonly Dictionary<string, Biome> PieceTiers = new Dictionary<string, Biome>();

        private static Biome TierOf(Piece piece)
        {
            if (PieceTiers.TryGetValue(piece.name, out var tier))
                return tier;
            var materials = piece.m_resources.Where(r => r.m_resItem && r.m_amount > 0).Select(r => r.m_resItem.name).ToList();
            tier = BiomeCart.TierOf(materials);
            PieceTiers[piece.name] = tier;
            if (tier == Biome.Later)
                Debug.Log($"[Cartheim] Dev: {piece.name} kept out of the build menus, past the Plains (or of materials not known): " +
                          string.Join(", ", materials.Where(m => BiomeCart.TierOf(m) == Biome.Later)));
            return tier;
        }

        // The serving tray's feasts, every one (any biome's), as well.
        private const string ServingTray = "Feaster";
        private static HashSet<string> _feasts;

        private static bool OnTheServingTray(Piece piece)
        {
            if (_feasts == null)
            {
                var table = GamePieces.Item(ServingTray)?.m_itemData.m_shared.m_buildPieces;
                // Not yet loaded: asked again next time.
                if (!table)
                    return false;
                _feasts = new HashSet<string>(table.m_pieces.Where(p => p).Select(p => p.name));
                Debug.Log($"[Cartheim] Dev: the serving tray's pieces: {string.Join(", ", _feasts)}");
            }
            return _feasts.Contains(piece.name);
        }

        [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), typeof(Piece), typeof(Player.RequirementMode)), HarmonyPrefix]
        private static bool UpToThePlains(Player __instance, Piece piece, Player.RequirementMode mode, ref bool __result)
        {
            if (!InDevWorld || __instance != Player.m_localPlayer || !piece)
                return true;
            __result = (TierOf(piece) <= Biome.Plains || OnTheServingTray(piece)) &&
                       (mode != Player.RequirementMode.CanBuild || !piece.m_craftingStation ||
                        CraftingStation.HaveBuildStationInRange(piece.m_craftingStation.m_name, __instance.transform.position) ||
                        ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoWorkbench));
            return false;
        }

        // While placing a piece, nothing is taken for it (crafting still costs as it does).
        private static bool _placing;

        [HarmonyPatch(typeof(Player), "UpdatePlacement"), HarmonyPrefix]
        private static void Placing() => _placing = true;

        [HarmonyPatch(typeof(Player), "UpdatePlacement"), HarmonyFinalizer]
        private static void Placed() => _placing = false;

        [HarmonyPatch(typeof(Player), nameof(Player.ConsumeResources)), HarmonyPrefix]
        private static bool FreeToBuild(Player __instance) => !(_placing && InDevWorld && __instance == Player.m_localPlayer);

        // The player in the dev world takes no harm (hits, falls, fire, poison) and spends no stamina.
        [HarmonyPatch(typeof(Character), nameof(Character.Damage)), HarmonyPrefix]
        private static bool Unharmed(Character __instance) => !(InDevWorld && __instance == Player.m_localPlayer);

        // Poison, burning and the like hurt without a hit, straight through this.
        [HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage)), HarmonyPrefix]
        private static bool UnharmedByAnything(Character __instance) => !(InDevWorld && __instance == Player.m_localPlayer);

        [HarmonyPatch(typeof(Player), nameof(Player.UseStamina)), HarmonyPrefix]
        private static bool Tireless(Player __instance) => !(InDevWorld && __instance == Player.m_localPlayer);
    }
}
