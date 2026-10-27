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
        // Its height in the building's own space, as recorded (Y is above the ground under it, where that was recorded).
        public float InBuilding;
        // What an armour stand wears, slot by slot.
        public List<(int slot, string item)> Wears = new List<(int, string)>();
    }

    // Camps set up by hand round a building in the dev world and recorded there (Record camp), shipped with the mod: what the
    // player built there (not what rides in the cart, nor the building's own), and where the cart stood.
    public static class CampRecords
    {
        // Food set out on a camp's table to look at, not to eat (each camp's own; what's not listed is there to take).
        private static readonly Dictionary<string, string[]> SetTable = new Dictionary<string, string[]>
        {
            ["WoodHouse3"] = new[] { "FeastMeadows", "MeadStaminaLingering" },
            ["WoodHouse7"] = new[] { "QueensJam" },
            ["WoodHouse9"] = new[] { "FeastMeadows", "CookedMeat", "MeadStaminaMinor" },
            ["WoodHouse10"] = new[] { "NeckTailGrilled", "MeadHealthMinor" },
            ["WoodHouse11"] = new[] { "FeastMeadows", "CookedMeat" },
        };

        // Camps that pitch the cart's tent beside the cart.
        private static readonly HashSet<string> Tented = new HashSet<string>
        {
            "WoodHouse3", "WoodHouse5", "WoodHouse6", "WoodHouse8", "WoodHouse9", "WoodHouse10", "WoodHouse12", "WoodHouse13",
        };

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
                piece.InBuilding = piece.Y;
                // Its height above what it stood on, where that was recorded (else above the building's own ground).
                var under = parts.Length > 7 ? Regex.Match(parts[7], @"(?:^|; )ground (-?[0-9.]+)") : Match.Empty;
                if (under.Success)
                    piece.Y -= Number(under.Groups[1].Value);
                if (parts.Length > 7)
                    foreach (Match worn in Regex.Matches(Regex.Match(parts[7], @"wears ([^;]*)").Groups[1].Value, @"(\d+):(\w+)"))
                        piece.Wears.Add((int.Parse(worn.Groups[1].Value, CultureInfo.InvariantCulture), worn.Groups[2].Value));
                if (parts[0] == "ground")
                {
                    if (piece.Prefab == Cultivated)
                        camp.Cultivated.Add((piece.X, piece.Z));
                }
                else if (parts[0] == "cart")
                {
                    camp.CartX = piece.X;
                    camp.CartZ = piece.Z;
                    camp.CartYaw = piece.Yaw;
                }
                // What the player built there, less what rides in the cart (its own upgrades, wherever it goes); a rug laid
                // there marks where the cart's own rug lies.
                else if (parts[0] == "player" && !parts[6].Split(',').Contains("CartRider"))
                {
                    if (piece.Prefab.StartsWith(Rug))
                        camp.RugSpot = camp.RugSpot ?? piece;
                    else
                        camp.Recorded.Add(piece);
                }
                // The building's own first bed: where the cart's bed goes, in its place; its second, a spare.
                else if (parts[0] == "building" && piece.Prefab.EndsWith(Bed))
                {
                    if (camp.BedSpot == null)
                        camp.BedSpot = piece;
                    else
                        camp.SpareBedSpot = camp.SpareBedSpot ?? piece;
                }
            }
            return camp;
        }

        private const string Rug = "rug_";
        private const string Cultivated = "cultivated";
        private const string Bed = "_bed";

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
