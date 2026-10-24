namespace Cartheim.Core
{
    // The pieces built into one cart, and which can be built next.
    public class CartUpgrades
    {
        public bool CanAdd(string piece) => piece == "piece_workbench";
    }
}
