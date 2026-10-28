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

        // Freyr's Blessing isn't nailed on: it costs just its own materials. Nor are the workbench and its extensions
        // while they're meadows work (no bronze or later in their recipe), so a cart can start out before the forge.
        public static IReadOnlyList<ItemCount> For(string piece, IEnumerable<ItemCount> recipe)
        {
            var items = recipe.ToList();
            return piece == FreyrsBlessing.Piece || IsWorkbench(piece) && items.All(i => IsMeadows(i.Prefab)) ? items : For(items);
        }

        private static bool IsWorkbench(string piece) => piece == "piece_workbench" || piece.StartsWith("piece_workbench_ext");

        private static bool IsMeadows(string material) =>
            BlackForestOrEarlier.Contains(material) && material != "Bronze" && material != "BronzeNails";

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
