using System;
using System.Globalization;

namespace Cartheim.Core
{
    // Ground made flat round a building, at its base: a rectangle in the building's own space (its middle, its width along
    // the building's x, its depth along its z), the ground in it at the building's base, blending back to its own height
    // over a margin round it.
    public static class FlatGround
    {
        public const float Margin = 2f;

        public sealed class Area
        {
            public Area(float x, float z, float width, float depth)
            {
                X = x;
                Z = z;
                Width = width;
                Depth = depth;
            }

            public float X { get; }
            public float Z { get; }
            public float Width { get; }
            public float Depth { get; }
        }

        // The ground's height at a point (in the building's space) with the area flattened; none beyond the margin, where
        // it's left as it is.
        public static float? Height(Area area, float x, float z, float natural, float floor)
        {
            var outX = Math.Max(0f, Math.Abs(x - area.X) - area.Width / 2f);
            var outZ = Math.Max(0f, Math.Abs(z - area.Z) - area.Depth / 2f);
            var outside = (float)Math.Sqrt(outX * outX + outZ * outZ);
            if (outside > Margin)
                return null;
            var t = outside / Margin;
            return floor + (natural - floor) * t;
        }

        // How a camp record writes it.
        public static string Line(Area area) =>
            string.Join(" | ", "flatten", "ground", N(area.X), "0.00", N(area.Z), "0", "Flatten", $"width {N(area.Width)}; depth {N(area.Depth)}");

        private static string N(float v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
