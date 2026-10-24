using System.Collections.Generic;
using System.Linq;

namespace Cartheim.Core
{
    // What building a piece into the cart takes: the piece's own recipe plus the nails to fix it on.
    public static class UpgradeCost
    {
        // Materials first found in the swamp or later; a piece made with any of them is held on with iron nails.
        private static readonly HashSet<string> SwampOrLater = new HashSet<string> { "Iron", "Chain" };

        public static IReadOnlyList<ItemCount> For(IEnumerable<ItemCount> recipe)
        {
            var nails = recipe.Any(i => SwampOrLater.Contains(i.Prefab)) ? "IronNails" : "BronzeNails";
            return recipe.Append(new ItemCount(nails, 5)).ToList();
        }
    }
}
