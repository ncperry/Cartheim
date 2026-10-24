using System.Collections.Generic;

namespace Cartheim.Core
{
    // The pieces built into one cart, and which can be built next.
    public class CartUpgrades
    {
        private const string Workbench = "piece_workbench";

        private readonly HashSet<string> built = new HashSet<string>();

        // The workbench goes in first; everything else is built onto it, and an extension (forge cooler, tanning rack)
        // onto the station it extends.
        public bool CanAdd(string piece, string extends = null) =>
            !built.Contains(piece) &&
            (piece == Workbench || built.Contains(Workbench)) &&
            (extends == null || built.Contains(extends));

        public void Add(string piece) => built.Add(piece);

        public const float HealthPerWorkbenchLevel = 100f;

        public float Health(float ownHealth) => ownHealth + (built.Contains(Workbench) ? HealthPerWorkbenchLevel : 0f);
    }
}
