namespace Cartheim.Core
{
    // A cart's own storage, and every container built into it, holds twice the slots it would elsewhere: as wide, twice
    // as many rows (the game's storage panel scrolls down, not across).
    public static class StorageSize
    {
        public static (int width, int height) Doubled(int width, int height) => (width, height * 2);
    }
}
