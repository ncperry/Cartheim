using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cartheim.Plugin
{
    // The game's buildings and ruins, placed by hand in the dev world: picked by the biome they're found in, then put down
    // as a see-through ghost where the player looks, however big it is (see CartRaft.BeginBuilding), turned with the scroll
    // wheel, and spawned
    // as the game spawns its own, kept in the world.
    internal static class DevBuildings
    {
        private static GameObject _panel;
        public static bool IsOpen => _panel;
        private static GameObject _list;
        private static Heightmap.Biome _biome = Heightmap.Biome.BlackForest;
        // Clicking a building places its whole family in a line instead (WoodHouse1 to 13).
        private static bool _lineUp;
        private static string LineUpLabel => _lineUp ? "Line up the family: On" : "Line up the family: Off";

        private static readonly Heightmap.Biome[] Biomes =
        {
            Heightmap.Biome.Meadows, Heightmap.Biome.BlackForest, Heightmap.Biome.Swamp, Heightmap.Biome.Mountain,
            Heightmap.Biome.Plains, Heightmap.Biome.Mistlands, Heightmap.Biome.AshLands, Heightmap.Biome.DeepNorth, Heightmap.Biome.Ocean,
        };

        private const float Width = 760f;
        private const float Height = 620f;

        public static void Choose()
        {
            Close();
            if (GUIManager.CustomGUIFront == null || !ZoneSystem.instance)
                return;
            InventoryGui.instance?.Hide();
            var middle = new Vector2(0.5f, 0.5f);
            _panel = GUIManager.Instance.CreateWoodpanel(GUIManager.CustomGUIFront.transform, middle, middle, Vector2.zero, Width, Height, draggable: true);
            var title = GUIManager.Instance.CreateText("Place a building", _panel.transform, middle, middle, new Vector2(0f, Height / 2f - 30f),
                GUIManager.Instance.AveriaSerifBold, 22, GUIManager.Instance.ValheimOrange, true, Color.black, Width - 40f, 30f, false);
            title.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            for (var i = 0; i < Biomes.Length; i++)
            {
                var biome = Biomes[i];
                var column = i % 5;
                var row = i / 5;
                Button(_panel.transform, Named(biome), new Vector2(-Width / 2f + 90f + column * 145f, Height / 2f - 75f - row * 40f), 135f, () =>
                {
                    _biome = biome;
                    Fill();
                });
            }
            Button(_panel.transform, "Cancel", new Vector2(110f, -Height / 2f + 30f), 140f, Close);
            Text lineUp = null;
            lineUp = Button(_panel.transform, "", new Vector2(-110f, -Height / 2f + 30f), 260f, () =>
            {
                _lineUp = !_lineUp;
                lineUp.text = LineUpLabel;
            }).GetComponentInChildren<Text>();
            lineUp.text = LineUpLabel;
            Fill();
            GUIManager.BlockInput(true);
        }

        // The biome's buildings, each a button that picks it up as a ghost to place.
        private static void Fill()
        {
            if (_list)
                Object.Destroy(_list);
            var middle = new Vector2(0.5f, 0.5f);
            _list = new GameObject("Buildings", typeof(RectTransform));
            _list.transform.SetParent(_panel.transform, false);
            // Only what the world generator places (some are switched on but never placed), with how many this world has.
            var inWorld = ZoneSystem.instance.m_locationInstances.Values.Where(i => i.m_location != null && i.m_location.m_prefab.IsValid)
                .GroupBy(i => i.m_location.m_prefab.Name).ToDictionary(g => g.Key, g => g.Count());
            var names = Locations().Where(l => (l.m_biome & _biome) != 0 && l.m_quantity > 0).Select(l => l.m_prefab.Name).Distinct().OrderBy(n => n).ToList();
            const int perColumn = 11;
            for (var i = 0; i < names.Count && i < perColumn * 3; i++)
            {
                var name = names[i];
                var column = i / perColumn;
                var row = i % perColumn;
                var count = inWorld.TryGetValue(name, out var n) ? n : 0;
                Button(_list.transform, $"{name} ({count})", new Vector2(-Width / 2f + 135f + column * 245f, Height / 2f - 170f - row * 36f), 235f, () =>
                {
                    Close();
                    if (_lineUp)
                        LineUp(name);
                    else
                        CartRaft.BeginBuilding(name);
                });
            }
            if (names.Count == 0)
                GUIManager.Instance.CreateText($"No buildings in the {Named(_biome)}", _list.transform, middle, middle, Vector2.zero,
                    GUIManager.Instance.AveriaSerifBold, 16, Color.white, true, Color.black, Width - 40f, 30f, false);
            Debug.Log($"[Cartheim] Dev buildings in the {Named(_biome)} (how many in this world): {string.Join(", ", names.Select(nm => $"{nm} ({(inWorld.TryGetValue(nm, out var c) ? c : 0)})"))}");
        }

        public static IEnumerable<ZoneSystem.ZoneLocation> Locations() =>
            ZoneSystem.instance ? ZoneSystem.instance.m_locations.Where(l => l != null && l.m_enable && l.m_prefab.IsValid) : Enumerable.Empty<ZoneSystem.ZoneLocation>();

        public static ZoneSystem.ZoneLocation Location(string name) => Locations().FirstOrDefault(l => l.m_prefab.Name == name);

        private static string Named(Heightmap.Biome biome) =>
            biome == Heightmap.Biome.BlackForest ? "Black Forest" : biome == Heightmap.Biome.AshLands ? "Ashlands" :
            biome == Heightmap.Biome.DeepNorth ? "Deep North" : biome == Heightmap.Biome.Mountain ? "Mountains" : biome.ToString();


        // The building's whole family in a line ahead of the player, in number order, each clear of the last, all facing back
        // towards them, on the ground.
        private const float LineStart = 10f;
        // Room between them to walk round each and leave a cart by it.
        private const float LineGap = 10f;

        private static void LineUp(string name)
        {
            var player = Player.m_localPlayer;
            if (!player)
                return;
            var family = Cartheim.Core.BuildingFamily.Members(name, Locations().Where(l => l.m_quantity > 0).Select(l => l.m_prefab.Name));
            var radii = family.Select(n => Location(n)).Select(l => Mathf.Max(l.m_exteriorRadius, l.m_interiorRadius)).ToList();
            var along = Cartheim.Core.BuildingFamily.Spacing(radii, LineStart, LineGap);
            var start = player.transform.position;
            // Where the player looks (the camera), not where their body happens to face.
            var look = GameCamera.instance ? GameCamera.instance.transform.forward : player.transform.forward;
            var forward = Vector3.ProjectOnPlane(look, Vector3.up).normalized;
            var facing = Quaternion.LookRotation(-forward, Vector3.up);
            var spots = along.Select(a => start + forward * a).ToList();
            // All on dry land, or none: where the line would first run into water, it stops before anything's placed.
            var fits = Cartheim.Core.BuildingFamily.Fits(spots.Select((s, i) => Dry(s, radii[i])));
            if (fits < family.Count)
            {
                var wet = along[fits] - radii[fits];
                Debug.Log($"[Cartheim] Dev: line of {family.Count} {Cartheim.Core.BuildingFamily.Of(name)} doesn't fit: water at {wet:0} m, after {fits}");
                player.Message(MessageHud.MessageType.Center, $"Not enough dry land: only {fits} of {family.Count} fit before water {wet:0} m ahead. Nothing placed");
                return;
            }
            for (var i = 0; i < family.Count; i++)
            {
                var at = spots[i];
                at.y = Ground(at);
                // The trees cleared from where it'll stand and round it first.
                CartRaft.ClearForestAt(at, radii[i] + CartRaft.ClearMargin);
                Place(family[i], at, facing);
            }
            Debug.Log($"[Cartheim] Dev: lined up {family.Count} of {Cartheim.Core.BuildingFamily.Of(name)}, {along.LastOrDefault() + radii.LastOrDefault():0} m long");
            player.Message(MessageHud.MessageType.Center, $"Lined up {family.Count} {Cartheim.Core.BuildingFamily.Of(name)}, {along.LastOrDefault() + radii.LastOrDefault():0} m");
        }


        // The ground's height there: as it is, where it's loaded near the player; else as the world was made (a long line
        // runs past what's loaded).
        private static float Ground(Vector3 at) =>
            ZoneSystem.instance.GetGroundHeight(at, out var height) ? height : WorldGenerator.instance.GetHeight(at.x, at.z);
        // Dry land under a building's middle and all round its edge.
        private static bool Dry(Vector3 at, float radius)
        {
            var water = ZoneSystem.instance.m_waterLevel;
            for (var i = 0; i <= 8; i++)
            {
                var angle = i * Mathf.PI / 4f;
                var spot = i == 8 ? at : at + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                if (Ground(spot) < water + 0.2f)
                    return false;
            }
            return true;
        }


        // Every building in the dev world standing well above or below the ground the world was made with (placed before
        // the ground was known that far out) taken down and put back on it: the same building, where it was and turned as
        // it was. Only its own parts go (those its template spawns); then the trees round it are cleared.
        public static void PutBackOnGround()
        {
            InventoryGui.instance?.Hide();
            var player = Player.m_localPlayer;
            if (!player || !ZoneSystem.instance || !DevButtons.InDevWorld)
                return;
            var proxy = ZoneSystem.instance.m_locationProxyPrefab.name.GetStableHashCode();
            var byHash = Locations().GroupBy(l => l.m_prefab.Name.GetStableHashCode()).ToDictionary(g => g.Key, g => g.First());
            var records = DevForest.Records();
            var me = ZDOMan.GetSessionID();
            var mended = new List<string>();
            foreach (var building in records.Where(z => z.IsValid() && z.GetPrefab() == proxy).ToList())
            {
                if (!byHash.TryGetValue(building.GetInt(ZDOVars.s_location), out var location))
                    continue;
                // Not those the game sets off the ground on purpose (afloat, sunk), nor any far away.
                if (location.m_snapToWater || location.m_prefab.Name.Contains("_sunk")
                    || Vector3.Distance(building.GetPosition(), player.transform.position) > 1000f)
                    continue;
                var at = building.GetPosition();
                var ground = WorldGenerator.instance.GetHeight(at.x, at.z);
                if (!Cartheim.Core.DevWorld.OffTheGround(at.y, ground))
                    continue;
                var turn = building.GetRotation();
                var reach = Mathf.Max(location.m_exteriorRadius, location.m_interiorRadius);
                var parts = PartsOf(location);
                var gone = 0;
                foreach (var part in records.Where(z => z.IsValid() && parts.Contains(z.GetPrefab())
                                                       && new Vector2(z.GetPosition().x - at.x, z.GetPosition().z - at.z).magnitude <= reach).ToList()
                             .Append(building))
                {
                    var loaded = ZNetScene.instance.FindInstance(part);
                    part.SetOwner(me);
                    if (loaded)
                        ZNetScene.instance.Destroy(loaded.gameObject);
                    else
                        ZDOMan.instance.DestroyZDO(part);
                    gone++;
                }
                Place(location.m_prefab.Name, new Vector3(at.x, ground, at.z), turn);
                mended.Add($"{location.m_prefab.Name} ({at.y - ground:+0.0;-0.0} m, {gone} parts)");
            }
            var trees = DevForest.ClearRoundBuildings(player.transform.position, 1000f, out _);
            Debug.Log($"[Cartheim] Dev: put back on the ground: {string.Join(", ", mended)}; {trees} trees cleared");
            player.Message(MessageHud.MessageType.Center, mended.Count == 0 ? "Every building stands on the ground" : $"Put {mended.Count} buildings back on the ground");
        }

        // The kinds of thing a building's template spawns (as the world's records name them).
        private static HashSet<int> PartsOf(ZoneSystem.ZoneLocation location)
        {
            location.m_prefab.Load();
            return new HashSet<int>(location.m_prefab.Asset.GetComponentsInChildren<ZNetView>(true)
                .Select(v => Utils.GetPrefabName(v.gameObject.name).GetStableHashCode()));
        }
        private static readonly System.Reflection.MethodInfo SpawnLocation = AccessTools.Method(typeof(ZoneSystem), "SpawnLocation");

        // Spawned as the game spawns its own ruins (kept in the world, not marked as cheated).
        public static void Place(string name, Vector3 at, Quaternion turn, int? seed = null)
        {
            var location = Location(name);
            if (location == null || SpawnLocation == null)
                return;
            SpawnLocation.Invoke(ZoneSystem.instance, new object[]
            {
                location, seed ?? Random.Range(0, 99999), at, turn, ZoneSystem.SpawnMode.Full, new List<GameObject>(), false,
            });
            Debug.Log($"[Cartheim] Dev building {name} placed at {at}, turned {turn.eulerAngles.y:0}");
        }

        public static void Update()
        {
            if (_panel && BepInEx.UnityInput.Current.GetKeyDown(KeyCode.Escape))
                Close();
        }

        public static void Close()
        {
            if (!_panel)
                return;
            Object.Destroy(_panel);
            _panel = null;
            _list = null;
            GUIManager.BlockInput(false);
        }

        private static GameObject Button(Transform parent, string text, Vector2 at, float width, System.Action onClick)
        {
            var middle = new Vector2(0.5f, 0.5f);
            var button = GUIManager.Instance.CreateButton(text, parent, middle, middle, at, width, 32f);
            button.GetComponent<Button>().onClick.AddListener(() => onClick());
            return button;
        }
    }
}
