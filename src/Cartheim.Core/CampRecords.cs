using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Cartheim.Core
{
    // A piece of a recorded camp: what it is, where it stands and how it's turned, in the building's own space.
    public class RecordedPiece
    {
        public string Prefab;
        public float X, Y, Z, Yaw;
    }

    // Camps set up by hand round a building in the dev world and recorded there (Record camp), shipped with the mod: what the
    // player built there (not what rides in the cart, nor the building's own), and where the cart stood.
    public static class CampRecords
    {
        // Food set out on a camp's table to look at, not to eat (each camp's own; what's not listed is there to take).
        private static readonly Dictionary<string, string[]> SetTable = new Dictionary<string, string[]>
        {
            ["WoodHouse3"] = new[] { "FeastMeadows", "MeadStaminaLingering" },
        };

        // Camps that pitch the cart's tent beside the cart.
        private static readonly HashSet<string> Tented = new HashSet<string> { "WoodHouse3" };

        public static RuinCamp Parse(string name, string text)
        {
            var camp = new RuinCamp { Name = name, Recorded = new List<RecordedPiece>(), Area = 10f };
            camp.Untouchable = new HashSet<string>(SetTable.TryGetValue(name, out var table) ? table : new string[0]);
            camp.PitchesTent = Tented.Contains(name);
            foreach (var line in text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0))
            {
                if (line.StartsWith("#"))
                {
                    var reach = Regex.Match(line, @"reaching ([0-9.]+) m");
                    if (reach.Success)
                        camp.Area = Number(reach.Groups[1].Value);
                    continue;
                }
                var parts = line.Split('|').Select(p => p.Trim()).ToArray();
                if (parts.Length < 7)
                    continue;
                var piece = new RecordedPiece
                {
                    Prefab = parts[1], X = Number(parts[2]), Y = Number(parts[3]), Z = Number(parts[4]), Yaw = Number(parts[5]),
                };
                if (parts[0] == "cart")
                {
                    camp.CartX = piece.X;
                    camp.CartZ = piece.Z;
                    camp.CartYaw = piece.Yaw;
                }
                // What the player built there, less what rides in the cart (its own upgrades, wherever it goes).
                else if (parts[0] == "player" && !parts[6].Split(',').Contains("CartRider"))
                    camp.Recorded.Add(piece);
            }
            return camp;
        }

        private static float Number(string text) => float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);

        // The camps shipped with the mod, by building.
        public static Dictionary<string, RuinCamp> Shipped()
        {
            var camps = new Dictionary<string, RuinCamp>();
            var assembly = typeof(CampRecords).Assembly;
            foreach (var resource in assembly.GetManifestResourceNames().Where(r => r.EndsWith(".camp")))
            {
                // Named Cartheim.Core.Camps.<building>.camp.
                var name = resource.Substring(0, resource.Length - ".camp".Length);
                name = name.Substring(name.LastIndexOf('.') + 1);
                using (var reader = new StreamReader(assembly.GetManifestResourceStream(resource)))
                    camps[name] = Parse(name, reader.ReadToEnd());
            }
            return camps;
        }
    }
}
