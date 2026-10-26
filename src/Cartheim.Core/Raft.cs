namespace Cartheim.Core
{
    // The cart's raft: the game's raft, built onto the cart, that carries it across water (sail taken off: paddled only).
    // It's launched where the water is deep enough to float it, and the cart put ashore where the ground is dry.
    public static class Raft
    {
        public const string Piece = "Raft";

        // How deep the water must be to float it (metres), how far under water the ground may be to put the cart ashore,
        // and how far from each other the cart and its raft may be placed.
        public const float MinDepth = 1f;
        public const float WetGround = 0.3f;
        public const float Reach = 12f;

        public static bool Floats(float ground, float water) => water - ground >= MinDepth;

        public static bool Ashore(float ground, float water) => water - ground <= WetGround;

        public static bool Near(float distance) => distance <= Reach;
    }
}
