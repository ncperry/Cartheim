using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Cartheim.Plugin
{
    // In the dev world: the game's wood houses (and farms and villages) written out as their templates have them, every part
    // the seed might give them (with its chance) in the building's own space, to design their camps from. Once a session,
    // and from the Dev tab.
    internal static class DevTemplates
    {
        private static readonly string[] Families = { "WoodHouse", "WoodFarm", "WoodVillage" };
        private static bool _done;

        public static string Folder => Path.Combine(BepInEx.Paths.ConfigPath, "cartheim-templates");

        public static void Update()
        {
            if (_done || !DevButtons.InDevWorld || !ZoneSystem.instance || !Player.m_localPlayer || !ZNetScene.instance)
                return;
            _done = true;
            Dump();
        }

        public static void Dump()
        {
            Directory.CreateDirectory(Folder);
            var written = new List<string>();
            foreach (var location in ZoneSystem.instance.m_locations
                         .Where(l => l != null && l.m_prefab.IsValid && Families.Any(f => l.m_prefab.Name.StartsWith(f)))
                         .GroupBy(l => l.m_prefab.Name).Select(g => g.First()))
            {
                location.m_prefab.Load();
                var template = location.m_prefab.Asset;
                if (!template)
                    continue;
                var root = template.transform;
                var lines = new List<string>
                {
                    $"# {location.m_prefab.Name}: biome {location.m_biome}, reaching {location.m_exteriorRadius:0.0} m (interior {location.m_interiorRadius:0.0}), " +
                    $"{location.m_quantity} to a world",
                    "# prefab | x | y | z | yaw | chance % (100: always) | what it is | size",
                };
                foreach (var view in root.GetComponentsInChildren<ZNetView>(true))
                {
                    var local = root.InverseTransformPoint(view.transform.position);
                    var yaw = Mathf.DeltaAngle(root.eulerAngles.y, view.transform.eulerAngles.y);
                    // Its chance: of each optional part on the way down to it.
                    var chance = view.GetComponentsInParent<RandomSpawn>(true).Aggregate(100f, (c, r) => c * r.m_chanceToSpawn / 100f);
                    var kinds = string.Join(",", view.GetComponents<MonoBehaviour>().Select(m => m.GetType().Name)
                        .Where(n => n != "ZNetView" && n != "ZSyncTransform" && n != "WearNTear" && n != "Piece" && n != "MaterialManNotifier" && n != "RandomSpawn"));
                    var size = CartGarden.Measure(GamePieces.Prefab(Utils.GetPrefabName(view.gameObject))).Box.size;
                    lines.Add(string.Join(" | ", Utils.GetPrefabName(view.gameObject), local.x.ToString("0.00"), local.y.ToString("0.00"), local.z.ToString("0.00"),
                        yaw.ToString("0"), chance.ToString("0"), kinds, $"{size.x:0.0}x{size.y:0.0}x{size.z:0.0}"));
                }
                File.WriteAllLines(Path.Combine(Folder, location.m_prefab.Name + ".template"), lines);
                written.Add($"{location.m_prefab.Name} ({lines.Count - 2})");
            }
            Debug.Log($"[Cartheim] Dev: building templates written to {Folder}: {string.Join(", ", written)}");
        }
    }
}
