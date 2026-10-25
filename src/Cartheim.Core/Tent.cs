using System.Linq;

namespace Cartheim.Core
{
    // A tent built into the cart: pitched beside it on fairly flat ground, it keeps the rain off whoever (and whatever
    // fire) is under it, and the cart stays put until it's packed up again.
    public static class Tent
    {
        public const string Piece = "cartheim_tent";

        // Ground under the tent, sampled across where it would stand, is flat enough when its highest and lowest points
        // are no more than maxRise apart.
        public static bool FlatEnough(float[] heights, float maxRise) => heights.Max() - heights.Min() <= maxRise;

        // Where the tent's floor goes: the average of the ground under it.
        public static float GroundLevel(float[] heights) => heights.Average();

        // A point in the tent's own space (its floor's middle at the origin, its sides along x, its ends along z): under the
        // tent when it's within its walls and between its floor and roof.
        public static bool Under(float x, float y, float z, float halfWidth, float halfDepth, float height) =>
            System.Math.Abs(x) <= halfWidth && System.Math.Abs(z) <= halfDepth && y >= 0f && y <= height;
    }
}
