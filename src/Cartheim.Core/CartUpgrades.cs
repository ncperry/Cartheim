using System.Collections.Generic;

namespace Cartheim.Core
{
    // The pieces built into one cart, and which can be built next.
    public class CartUpgrades
    {
        private const string Workbench = "piece_workbench";

        private readonly HashSet<string> built = new HashSet<string>();

        // The workbench goes in first; everything else is built onto it.
        public bool CanAdd(string piece) => !built.Contains(piece) && (piece == Workbench || built.Contains(Workbench));

        public void Add(string piece) => built.Add(piece);
    }
}
