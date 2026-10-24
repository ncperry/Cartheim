using System.Collections.Generic;
using System.Linq;

namespace Cartheim.Core
{
    // The pieces built into one cart, and which can be built next.
    public class CartUpgrades
    {
        private const string Workbench = "piece_workbench";
        private const string Forge = "forge";

        public const float HealthPerWorkbenchLevel = 100f;
        public const float ArmorPerForgeLevel = 5f;

        // Each built piece, with the station it extends (null for a piece that extends nothing), in build order.
        private readonly List<(string piece, string extends)> built = new List<(string, string)>();

        // The workbench goes in first; everything else is built onto it, and an extension (forge cooler, tanning rack)
        // onto the station it extends.
        public bool CanAdd(string piece, string extends = null) =>
            !Has(piece) &&
            (piece == Workbench || Has(Workbench)) &&
            (extends == null || Has(extends));

        public void Add(string piece, string extends = null) => built.Add((piece, extends));

        public UpgradeState StateOf(string piece, string extends, IEnumerable<ItemCount> cost, IReadOnlyDictionary<string, int> carried) =>
            UpgradeState.Ready;

        // One piece per line, as saved on the cart; an extension as "piece>station".
        public string Save() =>
            string.Join("\n", built.Select(b => b.extends == null ? b.piece : b.piece + ">" + b.extends));

        public static CartUpgrades Load(string saved)
        {
            var cart = new CartUpgrades();
            foreach (var line in saved.Split(new[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split('>');
                cart.Add(parts[0], parts.Length > 1 ? parts[1] : null);
            }
            return cart;
        }

        public float Health(float ownHealth) => ownHealth + HealthPerWorkbenchLevel * Level(Workbench);

        public float Armor => ArmorPerForgeLevel * Level(Forge);

        private bool Has(string piece) => built.Any(b => b.piece == piece);

        // A station's level, as the game counts it: 1 for the station, plus 1 per extension built onto it.
        private int Level(string station) =>
            Has(station) ? 1 + built.Count(b => b.extends == station) : 0;
    }
}
