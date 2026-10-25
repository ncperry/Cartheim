namespace Cartheim.Core
{
    // A coal-fired engine built into the cart (in place of the hearth): while it has coal it burns like a hearth, warming
    // and lighting like one, and pushes the cart along so pulling it slows the player less; fully upgraded, not at all.
    public static class SteamEngine
    {
        public const string Engine = "cartheim_steam_engine";
        public const string Boiler = "cartheim_engine_boiler";
        public const string Pistons = "cartheim_engine_pistons";

        public const string Coal = "Coal";
        public const int MaxCoal = 20;

        // Built only once the cart has both of these.
        public static readonly string[] Needs = { "piece_stonecutter", "forge" };

        // What's left after burning for a while, at a coal per secondsPerCoal (as long as a hearth takes over a log).
        public static float Burn(float coal, double seconds, float secondsPerCoal) =>
            System.Math.Max(0f, coal - (float)(seconds / secondsPerCoal));

        // Room for one more coal; as at a hearth, one partly burnt still takes up its place.
        public static bool HasRoomFor(float coal) => System.Math.Ceiling(coal) < MaxCoal;

        // How much of the cart's pull the engine takes off the player: none without coal, half with the engine alone, a
        // quarter more for each upgrade.
        public static float Assist(int engineLevel, float coal) =>
            coal <= 0f || engineLevel <= 0 ? 0f : System.Math.Min(1f, 0.25f + 0.25f * engineLevel);
    }
}
