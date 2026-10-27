using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Cartheim.Core
{
    // The game's buildings come in numbered families (WoodHouse1 to WoodHouse13): placed together in a line in the dev world,
    // in number order, each clear of the one before.
    public static class BuildingFamily
    {
        private static readonly Regex Numbered = new Regex(@"^(\D+?)(\d+)(.*)$");

        public static string Of(string name)
        {
            var match = Numbered.Match(name ?? "");
            return match.Success ? match.Groups[1].Value : name ?? "";
        }

        private static int Number(string name)
        {
            var match = Numbered.Match(name);
            return match.Success ? int.Parse(match.Groups[2].Value) : 0;
        }

        public static List<string> Members(string name, IEnumerable<string> names) =>
            names.Where(n => Of(n) == Of(name)).Distinct().OrderBy(Number).ThenBy(n => n).ToList();

        // How many of a line fit: up to the first that wouldn't stand on dry land.
        public static int Fits(IEnumerable<bool> dry) => dry.TakeWhile(d => d).Count();

        // How far along the line each one's middle is, given how far each reaches from its middle.
        public static List<float> Spacing(IEnumerable<float> radii, float start, float gap)
        {
            var along = new List<float>();
            var edge = start;
            foreach (var radius in radii)
            {
                along.Add(edge + radius);
                edge += 2 * radius + gap;
            }
            return along;
        }
    }
}
