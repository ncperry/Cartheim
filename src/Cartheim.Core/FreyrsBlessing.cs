namespace Cartheim.Core
{
    // Freyr's Blessing: the cart's bees, blessed, see that every crop within reach of the cart grows half as fast again
    // (two-thirds of its time), wherever it's planted; moved away, the cart takes the blessing with it. A Plains-age
    // upgrade: it needs the cart's beehive and fermenter, and Plains materials.
    public static class FreyrsBlessing
    {
        public const string Piece = "cartheim_freyr";
        public const string Fermenter = "fermenter";

        public const float Reach = 20f;
        public const double Speed = 1.5;

        public static bool Near(float distance) => distance <= Reach;

        // How much further on a crop is for having been blessed so long.
        public static double Bonus(double blessedFor) => blessedFor * (Speed - 1);
    }
}
