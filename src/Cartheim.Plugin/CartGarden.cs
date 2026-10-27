using System.Collections.Generic;
using System.Linq;
using Cartheim.Core;
using Jotunn.Managers;
using Splatform;
using UnityEngine;
using UnityEngine.UI;

namespace Cartheim.Plugin
{
    // The Wayside Garden (see Garden): set out from the cart's tabs by anyone carrying a cultivator, placed as the raft is
    // (see CartRaft): a square of cultivated ground inside the game's own stone fences, with its wooden fence gate and a
    // standing wood torch at each front corner, all real pieces built by the player; planted, if they like, with a crop.
    // And Freyr's Blessing: a blessed cart makes every crop near it grow faster.
    internal static class CartGarden
    {
        // The game's pieces, by their names in the game's text.
        private const string FenceName = "$piece_stonefence";
        private const string GateName = "$piece_woodfencegate";
        private const string TorchName = "$piece_groundtorchwood";
        private const string CultivatorItem = "Cultivator";
        // How far from the cart a garden's middle may be.
        public const float Reach = 20f;

        // A game piece used in the garden: its prefab, its length, the turn that lays it along x, and how far its pivot
        // is above its lowest point (so it stands on the ground, not half in it).
        internal struct Kind
        {
            public GameObject Prefab;
            public float Length, Turn, Lift;
            // The middle of its meshes, across, from its pivot (a door hangs off its hinge).
            public Vector3 Middle;
            // Its meshes' box, in its own space.
            public Bounds Box;
        }

        // A crop to plant: the plant grown from seeds, and the one grown from the crop itself for more seeds (if any).
        public class Crop
        {
            public string Name;
            public GameObject Plant, SeedPlant;
        }

        private static Kind _fence, _gate, _torch;
        private static GameObject _cultivate;
        private static float _cultivateSpacing;
        private static List<Crop> _crops;
        // The crop being planted with the garden being placed (null: none).
        private static Crop _crop;

        public static bool Carries(Player player) =>
            player && player.GetInventory().GetAllItems().Any(i => i.m_dropPrefab && i.m_dropPrefab.name == CultivatorItem);

        internal static bool Ready()
        {
            if (_fence.Prefab && _gate.Prefab && _torch.Prefab && _cultivate)
                return true;
            var hammer = ObjectDB.instance?.GetItemPrefab("Hammer")?.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_buildPieces;
            var cultivator = ObjectDB.instance?.GetItemPrefab(CultivatorItem)?.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_buildPieces;
            if (!hammer || !cultivator)
                return false;
            GameObject Find(string name) => hammer.m_pieces.FirstOrDefault(p => p && p.GetComponent<Piece>()?.m_name == name);
            _fence = Measure(Find(FenceName));
            _gate = Measure(Find(GateName));
            _torch = Measure(Find(TorchName));
            _cultivate = cultivator.m_pieces.FirstOrDefault(p => p && p.GetComponent<TerrainOp>() is TerrainOp op &&
                                                                op.m_settings.m_paintCleared && op.m_settings.m_paintType == TerrainModifier.PaintType.Cultivate);
            _crops = Crops(cultivator.m_pieces);
            if (!_fence.Prefab || !_gate.Prefab || !_torch.Prefab || !_cultivate)
            {
                Debug.LogWarning($"[Cartheim] Garden pieces not found: fence {(bool)_fence.Prefab}, gate {(bool)_gate.Prefab}, torch {(bool)_torch.Prefab}, cultivate {(bool)_cultivate}");
                return false;
            }
            _cultivateSpacing = Mathf.Max(0.5f, _cultivate.GetComponent<TerrainOp>().m_settings.m_paintRadius * 1.2f);
            Debug.Log($"[Cartheim] Garden: fence {_fence.Prefab.name} {_fence.Length:0.00} m (turned {_fence.Turn}, lifted {_fence.Lift:0.00}), " +
                      $"gate {_gate.Prefab.name} {_gate.Length:0.00} m (turned {_gate.Turn}, lifted {_gate.Lift:0.00}), torch {_torch.Prefab.name} (lifted {_torch.Lift:0.00}), " +
                      $"cultivating every {_cultivateSpacing:0.00} m; crops: {string.Join(", ", _crops.Select(c => $"{c.Name} ({c.Plant.name}{(c.SeedPlant ? " + " + c.SeedPlant.name : "")})"))}");
            return true;
        }

        // The cultivator's plants, paired: "sapling_seedcarrot" (planted from a carrot) goes with "sapling_carrot".
        private static List<Crop> Crops(List<GameObject> pieces)
        {
            var plants = pieces.Where(p => p && p.GetComponent<Plant>()).ToList();
            return plants.Where(p => !p.name.StartsWith("sapling_seed"))
                .Select(p => new Crop
                {
                    Name = PlantName(p),
                    Plant = p,
                    SeedPlant = plants.FirstOrDefault(s => s.name == p.name.Replace("sapling_", "sapling_seed")),
                })
                .OrderBy(c => c.Name).ToList();
        }

        private static string PlantName(GameObject plant)
        {
            var piece = plant.GetComponent<Piece>();
            return piece ? Localization.instance.Localize(piece.m_name) : plant.name;
        }

        internal static Kind Measure(GameObject prefab)
        {
            if (!prefab)
                return default;
            var bounds = new Bounds();
            var first = true;
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.sharedMesh)
                    continue;
                foreach (var corner in Corners(filter.sharedMesh.bounds))
                {
                    var p = prefab.transform.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    if (first)
                        bounds = new Bounds(p, Vector3.zero);
                    else
                        bounds.Encapsulate(p);
                    first = false;
                }
            }
            return new Kind
            {
                Prefab = prefab,
                Length = Mathf.Max(bounds.size.x, bounds.size.z),
                Turn = bounds.size.x >= bounds.size.z ? 0f : 90f,
                Lift = Mathf.Max(0f, -bounds.min.y),
                Middle = new Vector3(bounds.center.x, 0f, bounds.center.z),
                Box = bounds,
            };
        }

        private static IEnumerable<Vector3> Corners(Bounds b)
        {
            for (var i = 0; i < 8; i++)
                yield return new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
        }

        // Everything the garden is made of and planted with, in its own space: what, where, how turned and lifted.
        private struct Placed
        {
            public GameObject Prefab;
            public float X, Z, Yaw, Lift;
        }

        private static List<Placed> Everything()
        {
            var placed = Garden.Layout(_fence.Length, _gate.Length).Select(p =>
            {
                var kind = p.Gate ? _gate : _fence;
                return new Placed { Prefab = kind.Prefab, X = p.X, Z = p.Z, Yaw = p.Yaw + kind.Turn, Lift = kind.Lift };
            }).ToList();
            placed.AddRange(Garden.TorchSpots().Select(t => new Placed { Prefab = _torch.Prefab, X = t.x, Z = t.z, Lift = _torch.Lift }));
            if (_crop != null)
            {
                var (crops, seedPlants) = Garden.Planting(_crop.SeedPlant);
                var spots = Garden.PlantSpots(crops + seedPlants, PlantSpacing(_crop));
                for (var i = 0; i < spots.Count; i++)
                    placed.Add(new Placed { Prefab = i < crops ? _crop.Plant : _crop.SeedPlant, X = spots[i].x, Z = spots[i].z });
            }
            return placed;
        }

        // As far apart as the roomier of the crop's plants needs to grow.
        private static float PlantSpacing(Crop crop) =>
            Mathf.Max(1f, 2f * new[] { crop.Plant, crop.SeedPlant }.Where(p => p).Max(p => p.GetComponent<Plant>().m_growRadius) + 0.2f);

        public static List<ItemCount> Cost()
        {
            var parts = new List<(IEnumerable<ItemCount>, int)>
            {
                (GamePieces.Recipe(_fence.Prefab.name), Garden.Layout(_fence.Length, _gate.Length).Count(p => !p.Gate)),
                (GamePieces.Recipe(_gate.Prefab.name), 1),
                (GamePieces.Recipe(_torch.Prefab.name), Garden.TorchSpots().Count),
            };
            if (_crop != null)
                parts.AddRange(Planting(_crop));
            return Garden.Cost(parts.ToArray());
        }

        // The seeds (and crops planted for seed) a crop's planting takes.
        private static List<(IEnumerable<ItemCount>, int)> Planting(Crop crop)
        {
            var (crops, seedPlants) = Garden.Planting(crop.SeedPlant);
            var parts = new List<(IEnumerable<ItemCount>, int)> { (GamePieces.Recipe(crop.Plant.name), crops) };
            if (crop.SeedPlant)
                parts.Add((GamePieces.Recipe(crop.SeedPlant.name), seedPlants));
            return parts;
        }

        // Whether the player has (with the cart's storage) the seeds to plant it.
        private static bool HasSeedsFor(Player player, Crop crop)
        {
            var cost = Garden.Cost(Planting(crop).ToArray());
            var carried = GamePieces.Carried(player, cost);
            return player.NoCostCheat() || cost.All(c => carried.TryGetValue(c.Prefab, out var have) && have >= c.Count);
        }

        // From the garden tab: which crop to plant (or none), then placing.
        private static GameObject _panel;
        private const float Width = 420f;
        private const float RowHeight = 40f;

        public static void Choose(CartBase cart)
        {
            Close();
            if (!Ready() || !cart)
                return;
            var options = new List<(string label, Crop crop)> { ("Just the garden", null) };
            // Only crops the player has the seeds for.
            foreach (var crop in _crops.Where(c => HasSeedsFor(Player.m_localPlayer, c)))
            {
                var (crops, seedPlants) = Garden.Planting(crop.SeedPlant);
                options.Add((crop.SeedPlant ? $"{crop.Name}: {crops}, and {seedPlants} {PlantName(crop.SeedPlant)}" : $"{crop.Name}: {crops}", crop));
            }
            var height = 110f + options.Count * RowHeight;
            var middle = new Vector2(0.5f, 0.5f);
            _panel = GUIManager.Instance.CreateWoodpanel(GUIManager.CustomGUIFront.transform, middle, middle, Vector2.zero, Width, height, draggable: false);
            var title = GUIManager.Instance.CreateText("Plant the garden with?", _panel.transform, middle, middle, new Vector2(0f, height / 2f - 30f),
                GUIManager.Instance.AveriaSerifBold, 22, GUIManager.Instance.ValheimOrange, true, Color.black, Width - 40f, 30f, false);
            title.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            for (var i = 0; i < options.Count; i++)
            {
                var (label, crop) = options[i];
                var button = GUIManager.Instance.CreateButton(label, _panel.transform, middle, middle,
                    new Vector2(0f, height / 2f - 70f - i * RowHeight), Width - 60f, RowHeight - 6f);
                button.GetComponent<Button>().onClick.AddListener(() =>
                {
                    Close();
                    _crop = crop;
                    CartRaft.BeginGarden(cart);
                });
            }
            var cancel = GUIManager.Instance.CreateButton("Cancel", _panel.transform, middle, middle, new Vector2(0f, -height / 2f + 30f), 140f, 36f);
            cancel.GetComponent<Button>().onClick.AddListener(Close);
            GUIManager.BlockInput(true);
        }

        public static void Close()
        {
            if (!_panel)
                return;
            Object.Destroy(_panel);
            _panel = null;
            GUIManager.BlockInput(false);
        }

        public static void Update()
        {
            if (_panel && BepInEx.UnityInput.Current.GetKeyDown(KeyCode.Escape))
                Close();
        }

        // A ghost of the whole garden, kept on the ground by Pose.
        public static GameObject Ghost()
        {
            if (!Ready())
                return null;
            var root = new GameObject("Cartheim placing the garden");
            foreach (var p in Everything())
            {
                var piece = CartRaft.Ghost(p.Prefab.name);
                if (!piece)
                    continue;
                piece.transform.SetParent(root.transform, false);
                piece.transform.localPosition = new Vector3(p.X, p.Lift, p.Z);
                piece.transform.localRotation = Quaternion.Euler(0f, p.Yaw, 0f);
                piece.AddComponent<GhostLift>().Lift = p.Lift;
            }
            return root;
        }

        private class GhostLift : MonoBehaviour
        {
            public float Lift;
        }

        public static void Pose(GameObject ghost, Vector3 at, Quaternion facing)
        {
            ghost.transform.SetPositionAndRotation(at, facing);
            foreach (Transform piece in ghost.transform)
            {
                var p = piece.position;
                piece.position = new Vector3(p.x, Ground(p) + (piece.GetComponent<GhostLift>()?.Lift ?? 0f), p.z);
            }
        }

        internal static float Ground(Vector3 p) => ZoneSystem.instance ? ZoneSystem.instance.GetGroundHeight(p) : p.y;

        private static string ItemName(string prefab) =>
            GamePieces.Item(prefab) is ItemDrop item ? Localization.instance.Localize(item.m_itemData.m_shared.m_name) : prefab;

        // Why the garden can't go there, if it can't: rough ground, water, too far from the cart, a crop that won't grow
        // there, or too dear.
        public static string Problem(Vector3 at, Quaternion facing, CartBase cart, Player player)
        {
            var heights = new List<float>();
            for (var x = -1; x <= 1; x++)
                for (var z = -1; z <= 1; z++)
                    heights.Add(Ground(at + facing * new Vector3(x * Garden.Width / 2f, 0f, z * Garden.Depth / 2f)));
            var water = ZoneSystem.instance ? ZoneSystem.instance.m_waterLevel : 30f;
            if (!Raft.Ashore(heights.Min(), water))
                return "The garden needs dry ground";
            // As flat as the tent needs.
            if (!Tent.FlatEnough(heights.ToArray(), Plugin.TentMaxRise.Value))
                return "The ground is too uneven for a garden";
            if (Vector3.Distance(at, cart.transform.position) > Reach)
                return "Too far from the cart";
            if (_crop != null && (Heightmap.FindBiome(at) & _crop.Plant.GetComponent<Plant>().m_biome) == 0)
                return $"{_crop.Name} won't grow here";
            if (!player.NoCostCheat())
            {
                var cost = Cost();
                var carried = GamePieces.Carried(player, cost);
                if (cost.Any(c => !carried.TryGetValue(c.Prefab, out var have) || have < c.Count))
                    return "This garden takes " + string.Join(", ", cost.Select(c => $"{c.Count} {ItemName(c.Prefab)}"));
            }
            return null;
        }

        // Paid for; the ground inside cultivated; the fence, gate and torches built and the crop planted, as the player's own.
        public static void Place(Vector3 at, Quaternion facing, Player player)
        {
            if (!Ready() || !player)
                return;
            var cost = Cost();
            if (!player.NoCostCheat())
                GamePieces.Take(player, cost);
            Build(at, facing, player);
            Debug.Log($"[Cartheim] Wayside Garden set out at {at}{(_crop != null ? ", planted with " + _crop.Name : "")}, for {string.Join(", ", cost)}");
            player.Message(MessageHud.MessageType.Center, _crop != null ? $"Wayside Garden set out and planted with {_crop.Name}" : "Wayside Garden set out");
        }

        private static void Build(Vector3 at, Quaternion facing, Player player)
        {
            foreach (var (x, z) in Garden.CultivatePoints(_cultivateSpacing))
            {
                var p = at + facing * new Vector3(x, 0f, z);
                Apply(_cultivate, new Vector3(p.x, Ground(p), p.z));
            }
            foreach (var part in Everything())
            {
                var p = at + facing * new Vector3(part.X, 0f, part.Z);
                var built = Object.Instantiate(part.Prefab, new Vector3(p.x, Ground(p) + part.Lift, p.z), facing * Quaternion.Euler(0f, part.Yaw, 0f));
                built.GetComponent<Piece>()?.SetCreator(player.GetPlayerID(), PlatformManager.DistributionPlatform.LocalUser.PlatformUserID);
            }
            _fence.Prefab.GetComponent<Piece>()?.m_placeEffect.Create(at, facing);
        }

        // Ground cultivated at each of these spots that isn't already: how many spots it took.
        internal static int Cultivate(IEnumerable<Vector3> spots)
        {
            if (!Ready())
                return 0;
            var done = 0;
            foreach (var spot in spots)
            {
                var at = new Vector3(spot.x, Ground(spot), spot.z);
                var ground = Heightmap.FindHeightmap(at);
                if (ground && ground.IsCultivated(at))
                    continue;
                Apply(_cultivate, at);
                done++;
            }
            return done;
        }

        // The cultivator's own work at one spot, without its sound (a whole garden at once would be a racket).
        private static void Apply(GameObject terrainOp, Vector3 at)
        {
            var wasActive = terrainOp.activeSelf;
            terrainOp.SetActive(false);
            GameObject op;
            ZNetView.m_forceDisableInit = true;
            try
            {
                op = Object.Instantiate(terrainOp, at, Quaternion.identity);
            }
            finally
            {
                ZNetView.m_forceDisableInit = false;
                terrainOp.SetActive(wasActive);
            }
            op.GetComponent<TerrainOp>().m_onPlacedEffect = new EffectList();
            op.SetActive(true);
        }

        // A hammer piece by its name in the game's text, measured.
        internal static Kind HammerPiece(string name)
        {
            var hammer = ObjectDB.instance?.GetItemPrefab("Hammer")?.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_buildPieces;
            return Measure(hammer ? hammer.m_pieces.FirstOrDefault(p => p && p.GetComponent<Piece>()?.m_name == name) : null);
        }

        // Freyr's Blessing, every second from a blessed cart: every crop near it this game looks after is moved on by half
        // the time since the cart last blessed it. Only crops it blessed the time before (so not across a gap while the
        // cart was away, but across a night slept through); and when two blessed carts are near, only one moves it on.
        private const string BlessedAtKey = "cartheim_freyr_blessed";

        public static long Bless(CartBase cart, long lastBlessed)
        {
            if (!ZNet.instance)
                return lastBlessed;
            var now = ZNet.instance.GetTime().Ticks;
            foreach (var plant in SlowUpdate.GetAllInstaces().OfType<Plant>())
            {
                var view = plant ? plant.GetComponent<ZNetView>() : null;
                if (!view || !view.IsValid() || !view.IsOwner() ||
                    !FreyrsBlessing.Near(Vector3.Distance(plant.transform.position, cart.transform.position)))
                    continue;
                var zdo = view.GetZDO();
                var last = zdo.GetLong(BlessedAtKey, 0L);
                zdo.Set(BlessedAtKey, now);
                if (lastBlessed == 0 || last != lastBlessed || now <= last)
                    continue;
                var planted = zdo.GetLong(ZDOVars.s_plantTime, 0L);
                if (planted != 0)
                    zdo.Set(ZDOVars.s_plantTime, planted - (long)FreyrsBlessing.Bonus(now - last));
            }
            return now;
        }
    }
}
