using System.Collections.Generic;
using System.Linq;

namespace Cartheim.Core
{
    // What building a piece into the cart takes: the piece's own recipe plus the nails to fix it on.
    public static class UpgradeCost
    {
        // Materials found no later than the Black Forest. Anything else, including materials a game update adds,
        // counts as swamp or later, so the piece is held on with iron nails.
        private static readonly HashSet<string> BlackForestOrEarlier = new HashSet<string>
        {
            "Wood", "FineWood", "Stone", "Flint", "Resin", "Coal", "LeatherScraps", "DeerHide", "Copper", "Tin", "Bronze", "QueenBee", "Thistle",
            "Carrot", "Dandelion", "Feathers", "Mushroom", "RoundLog", "TrophySkeleton",
        };

        private const int Nails = 5;

        public static IReadOnlyList<ItemCount> For(IEnumerable<ItemCount> recipe)
        {
            var items = recipe.ToList();
            var nails = items.All(i => BlackForestOrEarlier.Contains(i.Prefab) || i.Prefab == "BronzeNails") ? "BronzeNails" : "IronNails";
            // Nails already in the recipe (the steam engine's) get the extra ones added to them.
            if (items.Any(i => i.Prefab == nails))
                return items.Select(i => i.Prefab == nails ? new ItemCount(nails, i.Count + Nails) : i).ToList();
            return items.Append(new ItemCount(nails, Nails)).ToList();
        }
    }
}
