using System.Linq;

namespace Cartheim.Core
{
    // What becomes of a cart afloat when its player dies at sea. The game respawns them on land, and with the whole base out
    // on the water, unable to swim back to it, they'd start from nothing; so by default the cart comes ashore beside them,
    // its raft packed away. Not on a hardcore world, where losing it is part of the deal.
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
    }
}
