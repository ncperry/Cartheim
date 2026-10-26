using System.Collections.Generic;

namespace Cartheim.Core
{
    // A portal built onto a cart: from the cart, it takes the player to the world's spawn; from the spawn (the sacrificial
    // stones), back to a cart that has one, picked by name when there are several.
    public static class Portal
    {
        public const string Piece = "portal_wood";

        public static string CartName(string builder) => $"{builder}'s Cart";

        // The carts' names as listed, a second (and third...) cart of the same name numbered.
        public static List<string> Labels(IEnumerable<string> names)
        {
            var seen = new Dictionary<string, int>();
            var labels = new List<string>();
            foreach (var name in names)
            {
                seen[name] = seen.TryGetValue(name, out var count) ? count + 1 : 1;
                labels.Add(seen[name] == 1 ? name : $"{name} ({seen[name]})");
            }
            return labels;
        }
    }
}
