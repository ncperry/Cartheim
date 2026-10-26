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
            var forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
            var facing = Quaternion.LookRotation(-forward, Vector3.up);
            for (var i = 0; i < family.Count; i++)
            {
                var at = start + forward * along[i];
                at.y = ZoneSystem.instance.GetGroundHeight(at);
                Place(family[i], at, facing);
            }
            Debug.Log($"[Cartheim] Dev: lined up {family.Count} of {Cartheim.Core.BuildingFamily.Of(name)}, {along.LastOrDefault() + radii.LastOrDefault():0} m long");
            player.Message(MessageHud.MessageType.Center, $"Lined up {family.Count} {Cartheim.Core.BuildingFamily.Of(name)}, {along.LastOrDefault() + radii.LastOrDefault():0} m");
        }
        private static readonly System.Reflection.MethodInfo SpawnLocation = AccessTools.Method(typeof(ZoneSystem), "SpawnLocation");

        // Spawned as the game spawns its own ruins (kept in the world, not marked as cheated).
        public static void Place(string name, Vector3 at, Quaternion turn)
        {
            var location = Location(name);
            if (location == null || SpawnLocation == null)
                return;
            SpawnLocation.Invoke(ZoneSystem.instance, new object[]
            {
                location, Random.Range(0, 99999), at, turn, ZoneSystem.SpawnMode.Full, new List<GameObject>(), false,
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
