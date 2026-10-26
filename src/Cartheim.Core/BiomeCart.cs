using System.Collections.Generic;
using System.Linq;

namespace Cartheim.Core
{
    // The lands in the order they're reached, as far as the dev world's carts go; Later is anything past them.
    public enum Biome { Meadows, BlackForest, Swamp, Mountains, Plains, Later }

    // A cart built out for a biome (in the dev world): every upgrade buildable with what that biome and the ones before it
    // give (a swamp cart has all that takes iron; a mountain cart all that takes silver or dragon tears), the best first, so
    // that where a cart takes only one of a kind (a chest, a bed, a rug) it's the latest.
    public static class BiomeCart
    {
        private static readonly Dictionary<string, Biome> Tiers = new Dictionary<string, Biome>();

        private static void Tier(Biome biome, params string[] materials)
        {
            foreach (var material in materials)
                Tiers[material] = biome;
        }

        static BiomeCart()
        {
            Tier(Biome.Meadows, "Wood", "Stone", "Flint", "Resin", "LeatherScraps", "DeerHide", "Feathers", "Raspberry", "Mushroom",
                "Dandelion", "HardAntler", "Honey", "QueenBee", "NeckTail", "Coal");
            Tier(Biome.BlackForest, "Copper", "CopperOre", "Tin", "TinOre", "Bronze", "BronzeNails", "FineWood", "RoundLog", "TrollHide",
                "SurtlingCore", "GreydwarfEye", "AncientSeed", "BoneFragments", "Blueberries", "Thistle", "Carrot", "CarrotSeeds",
                "CryptKey", "Coins", "BjornHide", "BjornPaw", "Amber", "AmberPearl", "Ruby");
            Tier(Biome.Swamp, "Iron", "IronScrap", "IronNails", "Chain", "ElderBark", "Guck", "WitheredBone", "Ooze", "Root",
                "Bloodbag", "Entrails", "Turnip", "TurnipSeeds", "Thunderstone");
            Tier(Biome.Mountains, "Silver", "SilverOre", "DragonTear", "Obsidian", "WolfPelt", "WolfFang", "WolfClaw", "Crystal",
                "FreezeGland", "Onion", "OnionSeeds", "JuteRed", "DragonEgg");
            Tier(Biome.Plains, "BlackMetal", "BlackMetalScrap", "LinenThread", "Flax", "Barley", "Needle", "LoxPelt", "Tar",
                "GoblinTotem", "Cloudberry");
        }

        public static Biome TierOf(string material) =>
            material != null && Tiers.TryGetValue(material, out var biome) ? biome : Biome.Later;

        public static Biome TierOf(IEnumerable<string> materials) =>
            materials.Select(TierOf).DefaultIfEmpty(Biome.Meadows).Max();

        // The pieces buildable by the end of a biome, the latest-tiered first (in the given order among equals).
        public static IEnumerable<string> For(Biome biome, IEnumerable<(string piece, string[] materials)> pieces) =>
            pieces.Select((p, i) => (p.piece, tier: TierOf(p.materials), i))
                .Where(p => p.tier <= biome)
                .OrderByDescending(p => p.tier).ThenBy(p => p.i)
                .Select(p => p.piece);
    }
}
