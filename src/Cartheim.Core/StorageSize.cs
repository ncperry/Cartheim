namespace Cartheim.Core
{
    // A cart's own storage, and every container built into it, holds twice the slots it would elsewhere: as wide, twice
    // as many rows (the game's storage panel scrolls down, not across).
    public static class StorageSize
    {
        public static (int width, int height) Doubled(int width, int height) => (width, height * 2);

        // Storage with no end (the cart's own): never fewer rows than it has to begin with, and always an empty row below
        // the lowest item, so there's room to put more. lowestUsedRow is -1 when it's empty.
        public static int GrowingRows(int baseRows, int lowestUsedRow) => System.Math.Max(baseRows, lowestUsedRow + 2);
    }
}
