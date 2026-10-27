namespace Cartheim.Core
{
    // The world kept for building and testing the mod ("CartHeim Dev", as Valheim Inventory knows it too): no monsters
    // spawn there, and its dev tools are on there, and only there. Known by its name, whatever its capitals and spaces.
    public static class DevWorld
    {
        public const string Name = "CartHeim Dev";

        public static bool Is(string worldName) =>
            worldName != null && Squeezed(worldName) == Squeezed(Name);

        private static string Squeezed(string name) => name.Replace(" ", "").ToLowerInvariant();

        // An item stand placed on a surface this upright (its normal's up part) stands on it; on anything steeper it hangs.
        public static bool OnFloor(float normalUp) => normalUp > 0.7f;

        // A building placed this far above or below the ground the world was made with (placed where the ground wasn't
        // known) is put back on it.
        public static bool OffTheGround(float placedAt, float ground) => System.Math.Abs(placedAt - ground) > 1.5f;
    }
}
