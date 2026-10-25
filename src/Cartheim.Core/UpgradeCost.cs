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
            "Wood", "FineWood", "Stone", "Flint", "Resin", "Coal", "LeatherScraps", "DeerHide", "Copper", "Tin", "Bronze", "QueenBee",
        };

        public static IReadOnlyList<ItemCount> For(IEnumerable<ItemCount> recipe)
        {
            var nails = recipe.All(i => BlackForestOrEarlier.Contains(i.Prefab)) ? "BronzeNails" : "IronNails";
            return recipe.Append(new ItemCount(nails, 5)).ToList();
        }
    }
}
