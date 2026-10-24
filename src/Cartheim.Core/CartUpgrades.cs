using System.Collections.Generic;
using System.Linq;

namespace Cartheim.Core
{
    // The pieces built into one cart, and which can be built next.
    public class CartUpgrades
    {
        private const string Workbench = "piece_workbench";

        public const float HealthPerWorkbenchLevel = 100f;

        private readonly HashSet<string> built = new HashSet<string>();

        // Each built extension, keyed by the station it extends.
        private readonly List<(string piece, string extends)> extensions = new List<(string, string)>();

        // The workbench goes in first; everything else is built onto it, and an extension (forge cooler, tanning rack)
        // onto the station it extends.
        public bool CanAdd(string piece, string extends = null) =>
            !built.Contains(piece) &&
            (piece == Workbench || built.Contains(Workbench)) &&
            (extends == null || built.Contains(extends));

        public void Add(string piece, string extends = null)
        {
            built.Add(piece);
            if (extends != null) extensions.Add((piece, extends));
        }

        public float Health(float ownHealth) => ownHealth + HealthPerWorkbenchLevel * Level(Workbench);

        public float Armor => 0f;

        // A station's level, as the game counts it: 1 for the station, plus 1 per extension built onto it.
        private int Level(string station) =>
            built.Contains(station) ? 1 + extensions.Count(e => e.extends == station) : 0;
    }
}
