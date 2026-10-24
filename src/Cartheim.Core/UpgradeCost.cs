using System.Collections.Generic;
using System.Linq;

namespace Cartheim.Core
{
    // What building a piece into the cart takes: the piece's own recipe plus the nails to fix it on.
    public static class UpgradeCost
    {
        public static IReadOnlyList<ItemCount> For(IEnumerable<ItemCount> recipe)
        {
            var nails = recipe.Any(i => i.Prefab == "Iron") ? "IronNails" : "BronzeNails";
            return recipe.Append(new ItemCount(nails, 5)).ToList();
        }
    }
}
