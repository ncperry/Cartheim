using System.Collections.Generic;
using System.Linq;
using BepInEx;
using UnityEngine;

namespace Cartheim.Plugin
{
    // The dev world's clearing tools, as placing modes (aim, left click, right click to stop):
    // - tearing down a building: the game's building looked at (all it spawned, inside its area) shown see-through, then
    //   taken away, nothing dropped; never what a player built, a cart or a player;
    // - clearing a forest: every tree, log, stump and bush inside a ring (its size on the scroll wheel) taken away.
    internal static partial class CartRaft
    {
        private static bool NeedsNoCart =>
            _placing == Placing.Stands || _placing == Placing.Building || _placing == Placing.TearDown || _placing == Placing.Forest;

        public static void BeginTearDown() => BeginDevMode(Placing.TearDown, new GameObject("Cartheim tearing down"));

        public static void BeginClearForest() => BeginDevMode(Placing.Forest, Ring());

        private static void BeginDevMode(Placing mode, GameObject ghost)
        {
            Cancel(null);
            InventoryGui.instance?.Hide();
            DevBuildings.Close();
            _cart = null;
            _placing = mode;
            _ghost = ghost;
            _said = null;
            _red = false;
            _inventoryGone = false;
            _target = null;
            Debug.Log($"[Cartheim] Dev tool: {mode}");
        }

        // The building looked at, and what of it is loaded here.
        private static Location _target;
        private static List<GameObject> _targetParts = new List<GameObject>();

        private static void TearingDown()
        {
            var camera = GameCamera.instance ? GameCamera.instance.transform : null;
            Location building = null;
            if (camera && Aim(camera, LayerMask.GetMask("terrain", "static_solid", "Default", "piece", "piece_nonsolid"), out var hit))
                building = Location.GetLocation(hit.point, checkDungeons: false);
            if (building != _target)
            {
                CartRuin.ShowAsTheyAre();
                _target = building;
                _targetParts = building ? PartsOf(building) : new List<GameObject>();
                if (building)
                    CartRuin.ShowThrough(_targetParts);
            }
            if (!building)
            {
                Say("Aim at a building to tear it down. Right click: done");
                return;
            }
            var name = building.name.Replace("(Clone)", "");
            Say($"Left click: tear down {name} ({_targetParts.Count} parts). Right click: done");
            if (!UnityInput.Current.GetMouseButtonDown(0))
                return;
            CartRuin.ShowAsTheyAre();
            var gone = Remove(_targetParts);
            // The building itself, so it's not made again.
            var proxy = building.GetComponentInParent<LocationProxy>();
            if (proxy)
                gone += Remove(new List<GameObject> { proxy.gameObject });
            _target = null;
            Debug.Log($"[Cartheim] Dev: tore down {name}: {gone} things");
            Player.m_localPlayer?.Message(MessageHud.MessageType.Center, $"Tore down {name}");
        }

        // All that's loaded inside a building's area that isn't a player's, a cart, a creature, or the land's own trees and
        // rocks.
        private static List<GameObject> PartsOf(Location building)
        {
            var at = building.transform.position;
            var radius = building.m_exteriorRadius;
            return Object.FindObjectsByType<ZNetView>(FindObjectsSortMode.None)
                .Where(v => v && v.IsValid() && Flat(v.transform.position - at).magnitude <= radius)
                .Where(v => !v.GetComponent<Character>() && !v.GetComponent<Vagon>() && !v.GetComponent<LocationProxy>()
                            && !v.GetComponent<TreeBase>() && !v.GetComponent<TreeLog>() && !v.GetComponent<MineRock>() && !v.GetComponent<MineRock5>()
                            && !v.name.StartsWith("_") && !(v.GetComponent<Piece>() is Piece piece && piece.GetCreator() != 0L))
                .Select(v => v.gameObject).ToList();
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        // Taken away as the owner (so for everyone), nothing dropped.
        private static int Remove(List<GameObject> things)
        {
            var gone = 0;
            foreach (var thing in things.Where(t => t))
            {
                var view = thing.GetComponent<ZNetView>();
                if (!view || !view.IsValid())
                    continue;
                view.ClaimOwnership();
                ZNetScene.instance.Destroy(thing);
                gone++;
            }
            return gone;
        }


        // The trees, logs, stumps and bushes within a radius of a point, taken away; how many.
        public static int ClearForestAt(Vector3 at, float radius)
        {
            var trees = Object.FindObjectsByType<ZNetView>(FindObjectsSortMode.None)
                .Where(v => v && v.IsValid() && Flat(v.transform.position - at).magnitude <= radius && IsForest(v))
                .Select(v => v.gameObject).ToList();
            return Remove(trees);
        }

        // Round every one of the game's buildings loaded near the player, the trees cleared: its area and a margin.
        private static readonly HarmonyLib.AccessTools.FieldRef<List<Location>> AllLocations =
            HarmonyLib.AccessTools.StaticFieldRefAccess<List<Location>>(HarmonyLib.AccessTools.Field(typeof(Location), "s_allLocations"));

        public const float ClearMargin = 5f;

        public static void ClearRoundBuildings()
        {
            InventoryGui.instance?.Hide();
            var player = Player.m_localPlayer;
            if (!player)
                return;
            var buildings = AllLocations().Where(l => l && Vector3.Distance(l.transform.position, player.transform.position) <= 400f).ToList();
            var gone = buildings.Sum(l => ClearForestAt(l.transform.position, l.m_exteriorRadius + ClearMargin));
            Debug.Log($"[Cartheim] Dev: cleared {gone} trees round {buildings.Count} buildings");
            player.Message(MessageHud.MessageType.Center, $"Cleared {gone} trees round {buildings.Count} buildings");
        }
        private static float _forestRadius = 15f;
        private const float ForestStep = 2.5f;

        private static void ClearingForest()
        {
            _forestRadius = Mathf.Clamp(_forestRadius + UnityInput.Current.mouseScrollDelta.y * ForestStep, 5f, 50f);
            var camera = GameCamera.instance ? GameCamera.instance.transform : null;
            if (!camera || !Aim(camera, LayerMask.GetMask("terrain", "static_solid", "Default"), out var hit))
            {
                _ghost.SetActive(false);
                Say("Aim at the ground");
                return;
            }
            _ghost.SetActive(true);
            _ghost.transform.position = hit.point;
            _ghost.transform.localScale = Vector3.one * _forestRadius;
            Say($"Left click: clear the trees within {_forestRadius:0} m. Scroll: bigger or smaller. Right click: done");
            if (!UnityInput.Current.GetMouseButtonDown(0))
                return;
            var gone = ClearForestAt(hit.point, _forestRadius);
            Debug.Log($"[Cartheim] Dev: cleared {gone} trees, logs, stumps and bushes within {_forestRadius:0} m of {hit.point}");
            Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, $"Cleared {gone}");
        }

        private static readonly string[] ForestNames = { "stub", "bush", "shrub", "log" };

        private static bool IsForest(ZNetView thing) =>
            !(thing.GetComponent<Piece>() is Piece piece && piece.GetCreator() != 0L) &&
            (thing.GetComponent<TreeBase>() || thing.GetComponent<TreeLog>() ||
             thing.GetComponent<Destructible>() is Destructible d && d.m_destructibleType == DestructibleType.Tree ||
             ForestNames.Any(n => thing.name.IndexOf(n, System.StringComparison.OrdinalIgnoreCase) >= 0));

        // A ring a metre across, lying on the ground, scaled to the size cleared.
        private static GameObject Ring()
        {
            var ring = new GameObject("Cartheim clearing ring");
            var line = ring.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.widthMultiplier = 0.08f;
            const int points = 64;
            line.positionCount = points;
            for (var i = 0; i < points; i++)
            {
                var angle = i * Mathf.PI * 2f / points;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), 0.1f, Mathf.Sin(angle)));
            }
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = line.endColor = new Color(1f, 0.55f, 0.1f, 0.9f);
            return ring;
        }
    }
}
