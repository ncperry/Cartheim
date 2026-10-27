using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Cartheim.Plugin
{
    // In the dev world: a camp set up by hand round one of the game's buildings, read and written down (a record to make it
    // a camp a cart can put up there); and a fresh copy of a building set beside it, to try a cart's camp on.
    internal static class DevCampRecord
    {
        // The building the player stands at (its area holds them), else the nearest within 30 m.
        private static Location BuildingHere(Player player)
        {
            var here = player.transform.position;
            return Object.FindObjectsByType<Location>(FindObjectsSortMode.None)
                .Where(l => l && Flat(l.transform.position - here).magnitude <= Mathf.Max(30f, l.m_exteriorRadius))
                .OrderBy(l => Flat(l.transform.position - here).magnitude).FirstOrDefault();
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        public static string Folder => Path.Combine(BepInEx.Paths.ConfigPath, "cartheim-camps");

        private const float CultivatedStep = 0.5f;

        // Everything loaded within the building's area and a margin: what it is, whose, where and turned how in the
        // building's own space, what it does, and what's on it. Written to a file named after the building, and the log.
        public static void Record()
        {
            InventoryGui.instance?.Hide();
            var player = Player.m_localPlayer;
            var building = player ? BuildingHere(player) : null;
            if (!building)
            {
                player?.Message(MessageHud.MessageType.Center, "Stand at a building to record its camp");
                return;
            }
            var name = building.name.Replace("(Clone)", "").Trim();
            var origin = building.transform;
            var reach = building.m_exteriorRadius + 10f;
            var lines = new List<string>
            {
                $"# Camp at {name}: recorded {System.DateTime.Now:yyyy-MM-dd HH:mm}, the building at {origin.position}, turned {origin.eulerAngles.y:0.0}, reaching {building.m_exteriorRadius:0.0} m",
                "# whose | prefab | x | y | z | yaw | what it is | what's on it",
            };
            var things = Object.FindObjectsByType<ZNetView>(FindObjectsSortMode.None)
                .Where(v => v && v.IsValid() && Flat(v.transform.position - origin.position).magnitude <= reach && !v.GetComponent<Player>())
                .OrderBy(v => v.GetComponent<Piece>() is Piece p && p.GetCreator() != 0L ? 0 : 1).ThenBy(v => v.name);
            foreach (var view in things)
            {
                var piece = view.GetComponent<Piece>();
                var whose = view.GetComponent<Vagon>() ? "cart" : piece && piece.GetCreator() != 0L ? "player" : "building";
                var local = origin.InverseTransformPoint(view.transform.position);
                var yaw = Mathf.DeltaAngle(origin.eulerAngles.y, view.transform.eulerAngles.y);
                var kinds = string.Join(",", view.GetComponents<MonoBehaviour>().Select(m => m.GetType().Name)
                    .Where(n => n != "ZNetView" && n != "ZSyncTransform" && n != "WearNTear" && n != "Piece" && n != "LODGroup"));
                lines.Add(string.Join(" | ", whose, Utils.GetPrefabName(view.gameObject), local.x.ToString("0.00"), local.y.ToString("0.00"),
                    local.z.ToString("0.00"), yaw.ToString("0"), kinds, whose == "player" && !view.GetComponent<CartRider>() ? Join($"ground {StandsOn(view, origin):0.00}", OnIt(view)) : OnIt(view)));
            }
            // The ground flattened round it (see DevGround.Flatten), to flatten again where the camp's set up.
            var flattened = building.GetComponentInParent<LocationProxy>()?.GetComponent<ZNetView>()?.GetZDO()?.GetString(DevGround.FlattenedKey, "");
            if (!string.IsNullOrEmpty(flattened))
                lines.Add(flattened);
            // The cultivated ground, every half metre of it.
            var cultivated = 0;
            for (var x = -reach; x <= reach; x += CultivatedStep)
                for (var z = -reach; z <= reach; z += CultivatedStep)
                {
                    var at = origin.position + Quaternion.Euler(0f, origin.eulerAngles.y, 0f) * new Vector3(x, 0f, z);
                    var ground = Heightmap.FindHeightmap(at);
                    if (!ground || !ground.IsCultivated(at))
                        continue;
                    lines.Add(string.Join(" | ", "ground", "cultivated", x.ToString("0.00"), "0.00", z.ToString("0.00"), "0", "Cultivated", ""));
                    cultivated++;
                }
            Debug.Log($"[Cartheim] Dev: {cultivated} spots of cultivated ground at {name}");
            Directory.CreateDirectory(Folder);
            var file = Path.Combine(Folder, name + ".camp");
            File.WriteAllLines(file, lines);
            Debug.Log($"[Cartheim] Dev: camp at {name} recorded to {file}:\n{string.Join("\n", lines)}");
            player.Message(MessageHud.MessageType.Center, $"Recorded the camp at {name}: {lines.Count - 2} things");
        }

        // The height (in the building's space) of what a piece stands on, found as a camp finds it when putting the piece up
        // (see CartRuin.Placement): the highest ground or floor of the building's own under any corner of it, not counting
        // what the player built.
        private const float FloorsWithin = 1.5f;

        private static float StandsOn(ZNetView view, Transform origin)
        {
            var box = CartGarden.Measure(GamePieces.Prefab(Utils.GetPrefabName(view.gameObject))).Box;
            var turn = Quaternion.Euler(0f, view.transform.eulerAngles.y, 0f);
            var solid = LayerMask.GetMask("terrain", "piece", "static_solid");
            var corners = new[] { new Vector3(box.min.x, 0f, box.min.z), new Vector3(box.min.x, 0f, box.max.z), new Vector3(box.max.x, 0f, box.min.z), new Vector3(box.max.x, 0f, box.max.z), Vector3.zero };
            var highest = corners.Select(c =>
            {
                var spot = view.transform.position + turn * c;
                var floors = Physics.RaycastAll(new Vector3(spot.x, origin.position.y + FloorsWithin, spot.z), Vector3.down, 2f * FloorsWithin, solid, QueryTriggerInteraction.Ignore)
                    .Where(h => !h.collider.GetComponentInParent<Character>() && !(h.collider.GetComponentInParent<Piece>() is Piece p && p.GetCreator() != 0L))
                    .Select(h => h.point.y);
                return floors.Append(CartGarden.Ground(spot)).Max();
            }).Max();
            return highest - origin.position.y;
        }

private static string Join(string first, string rest) => rest == "" ? first : first + "; " + rest;
        // What's on it, as the game keeps it: an item stand's item, a fire's fuel, a container's contents, a hive's honey.
        private static string OnIt(ZNetView view)
        {
            var zdo = view.GetZDO();
            var notes = new List<string>();
            if (view.GetComponent<ItemStand>() is ItemStand stand && stand.HaveAttachment())
                notes.Add("holds " + (ObjectDB.instance.GetItemPrefab(stand.GetAttachedItem())?.name ?? stand.GetAttachedItem().ToString()));
            if (view.GetComponent<ArmorStand>() is ArmorStand armour)
            {
                var worn = Enumerable.Range(0, armour.m_slots.Count)
                    .Select(i => (i, item: zdo.GetInt((i + "_item").GetStableHashCode())))
                    .Where(s => s.item != 0)
                    .Select(s => $"{s.i}:{ObjectDB.instance.GetItemPrefab(s.item)?.name ?? s.item.ToString()}").ToList();
                if (worn.Count > 0)
                    notes.Add("wears " + string.Join(", ", worn));
            }
            if (view.GetComponent<Fireplace>())
                notes.Add($"fuel {zdo.GetFloat(ZDOVars.s_fuel):0.0}");
            if (view.GetComponent<Container>() is Container chest && chest.GetInventory() != null)
                notes.Add("holds " + string.Join(", ", chest.GetInventory().GetAllItems().Select(i => $"{i.m_stack} {i.m_dropPrefab?.name ?? i.m_shared.m_name}")));
            if (view.GetComponent<Beehive>())
                notes.Add($"honey {zdo.GetInt(ZDOVars.s_level)}");
            if (view.GetComponent<Piece>() is Piece piece && piece.m_resources.Length > 0)
                notes.Add("made of " + string.Join(", ", piece.m_resources.Where(r => r.m_resItem).Select(r => $"{r.m_amount} {r.m_resItem.name}")));
            return string.Join("; ", notes);
        }

        // A fresh copy of the building the player stands at, set out to its side, past both their reaches and a gap: the same
        // building, turned the same way, without what's been built round the first.
        public static void CopyToTheSide()
        {
            InventoryGui.instance?.Hide();
            var player = Player.m_localPlayer;
            var building = player ? BuildingHere(player) : null;
            if (!building)
            {
                player?.Message(MessageHud.MessageType.Center, "Stand at a building to copy it");
                return;
            }
            var name = building.name.Replace("(Clone)", "").Trim();
            var origin = building.transform;
            var side = Flat(origin.right).normalized;
            var at = origin.position + side * (2f * building.m_exteriorRadius + 15f);
            at.y = WorldGenerator.instance.GetHeight(at.x, at.z);
            if (at.y < ZoneSystem.instance.m_waterLevel + 0.2f)
            {
                at = origin.position - side * (2f * building.m_exteriorRadius + 15f);
                at.y = WorldGenerator.instance.GetHeight(at.x, at.z);
            }
            CartRaft.ClearForestAt(at, building.m_exteriorRadius + CartRaft.ClearMargin);
            // The same seed, so the same of its optional parts (huts, a beehive) as the first.
            var seed = building.GetComponentInParent<LocationProxy>()?.GetComponent<ZNetView>()?.GetZDO()?.GetInt(ZDOVars.s_seed);
            DevBuildings.Place(name, at, Quaternion.Euler(0f, origin.eulerAngles.y, 0f), seed);
            DevForest.ClearRoundBuildings(at, 5f, out _);
            player.Message(MessageHud.MessageType.Center, $"A copy of {name} set {Vector3.Distance(at, origin.position):0} m to the side");
        }
    }
}
