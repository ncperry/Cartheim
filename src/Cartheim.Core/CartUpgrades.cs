using System.Collections.Generic;
using System.Linq;

namespace Cartheim.Core
{
    // The pieces built into one cart, and which can be built next.
    public class CartUpgrades
    {
        private const string Workbench = "piece_workbench";
        private const string Forge = "forge";
        private const string Hearth = "hearth";

        // A cart holds one chest, whichever kind.
        private static readonly HashSet<string> Chests = new HashSet<string> { "piece_chest_wood", "piece_chest", "piece_chest_blackmetal", "piece_chest_private", "piece_chest_grausten" };

        // A cart has one light to glow with.
        private static readonly HashSet<string> Lights = new HashSet<string>
        {
            "piece_FairylightGarland", "piece_walltorch", "piece_dvergr_lantern", "piece_dvergr_lantern_pole", "piece_Lavalantern",
            "piece_hoodedlantern", "piece_jackoturnip", "piece_snowlantern",
        };

        // Kinds of piece a cart holds only one of.
        private static readonly HashSet<string>[] OneOfEach = { Chests, Lights };

        public const float HealthPerWorkbenchLevel = 100f;
        public const float ArmorPerForgeLevel = 5f;
        // How many upgrades make a cart look as full as one with every slot filled.
        public const int UpgradesThatLookFull = 10;

        // Each built piece, with the station it extends (null for a piece that extends nothing), in build order.
        private readonly List<(string piece, string extends)> built = new List<(string, string)>();

        // The workbench goes in first; everything else is built onto it, and an extension (forge cooler, tanning rack)
        // onto the station it extends.
        public bool CanAdd(string piece, string extends = null) =>
            !Has(piece) &&
            (piece == Workbench || Has(Workbench)) &&
            (extends == null || Has(extends)) &&
            !OneOfEach.Any(kind => kind.Contains(piece) && built.Any(b => kind.Contains(b.piece)));

        public void Add(string piece, string extends = null) => built.Add((piece, extends));

        public UpgradeState StateOf(string piece, string extends, IEnumerable<ItemCount> cost, IReadOnlyDictionary<string, int> carried) =>
            Has(piece) ? UpgradeState.Built :
            !CanAdd(piece, extends) ? UpgradeState.Locked :
            cost.All(c => carried.TryGetValue(c.Prefab, out var have) && have >= c.Count) ? UpgradeState.Ready : UpgradeState.CannotAfford;

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

        public IReadOnlyList<ItemCount> Refund(System.Func<string, IEnumerable<ItemCount>> recipeOf) =>
            built.SelectMany(b => UpgradeCost.For(recipeOf(b.piece)))
                .GroupBy(c => c.Prefab)
                .Select(g => new ItemCount(g.Key, g.Sum(c => c.Count)))
                .ToList();

        public float Health(float ownHealth) => ownHealth + HealthPerWorkbenchLevel * Level(Workbench);

        public const string ThatchRoof = "cartheim_roof_thatch";
        public const string ShingleRoof = "cartheim_roof_shingle";
        public const string GraustenRoof = "cartheim_roof_grausten";

        // Each roof is built over the one before (thatch, then shingle, then grausten), so the roof's level counts them all.
        // Each roof is built over the one before (thatch, then shingle, then grausten), so the roof's level counts them all.
        // A hearth built in is a fire of the cart's own: it lights the cauldron and warms a rest without a campfire nearby.
        public bool HasHearth => Has(Hearth);

        public int RoofLevel => Level(ThatchRoof) + built.Count(b => b.extends == ShingleRoof);

        public float Fullness => System.Math.Min(1f, (float)built.Count / UpgradesThatLookFull);

        public float Armor => ArmorPerForgeLevel * Level(Forge);

        private bool Has(string piece) => built.Any(b => b.piece == piece);

        // A station's level, as the game counts it: 1 for the station, plus 1 per extension built onto it.
        public int Level(string station) =>
            Has(station) ? 1 + built.Count(b => b.extends == station) : 0;
    }
}
