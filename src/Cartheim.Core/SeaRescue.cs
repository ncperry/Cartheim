using System.Linq;

namespace Cartheim.Core
{
    // What becomes of a cart afloat when its player dies at sea. The game respawns them on land, and with the whole base out
    // on the water, unable to swim back to it, they'd start from nothing; so by default the cart washes ashore beside its
    // builder, its raft packed away, unless someone is still alive near it. Not on a hardcore world, where losing it is
    // part of the deal.
    public enum SeaDeath
    {
        ComesExceptHardcore,
        AlwaysComes,
        StaysAtSea,
    }

    public static class SeaRescue
    {
        // How near where the player died a cart afloat must be to come.
        public const float Reach = 100f;

        public static bool Comes(SeaDeath setting, bool hardcore) =>
            setting == SeaDeath.AlwaysComes || setting == SeaDeath.ComesExceptHardcore && !hardcore;

        // The world's preset as the game keeps it: a preset's name ("hardcore"), or its modifiers when set one by one
        // ("deathpenalty_hardcore:combat_hard").
        public static bool Hardcore(string preset) =>
            !string.IsNullOrEmpty(preset) && preset.Split(':').Any(p => p == "hardcore" || p == "deathpenalty_hardcore");

        public static bool Near(float distance) => distance <= Reach;

        // Adrift only when nobody else is still alive near it: otherwise it stays with them, for them to bring ashore.
        public static bool Adrift(int othersAliveNear) => othersAliveNear == 0;

        // It washes ashore by the player who built the cart, or by whoever died when nobody did.
        public static long ComesTo(long builder, long died) => builder != 0 ? builder : died;

        // The game that brings it over (it knows where the cart is): that of whoever died, or once they've left, the server's.
        public static bool Brings(bool died, bool diedOnline, bool server) => died || !diedOnline && server;

        // While another player is near the cart (the game hands it to them), it's left alone.
        public static bool WithSomeoneElse(long owner, long me) => owner != 0 && owner != me;
    }
}
