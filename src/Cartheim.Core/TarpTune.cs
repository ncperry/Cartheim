using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Cartheim.Core
{
    // Where a ruin's tarp goes, in the ruin's own space: its edges, its low (-x) and high (+x) sides' heights, and how far
    // it's turned about its middle (degrees).
    public struct TarpBox
    {
        public float MinX, MaxX, MinZ, MaxZ, Low, High, Turn;
    }

    // Tuning a ruin's tarp by hand, in a dev session: moved, dragged about, its edges and heights changed, then its numbers
    // written as the camp takes them, to be put into the camp.
    public static class TarpTune
    {
        // The least a tarp is across or back (metres).
        public const float Smallest = 0.5f;

        public static TarpBox From(RuinCamp camp) => new TarpBox
        {
            MinX = camp.TarpMinX, MaxX = camp.TarpMaxX, MinZ = camp.TarpMinZ, MaxZ = camp.TarpMaxZ,
            Low = camp.TarpLow, High = camp.TarpHigh, Turn = camp.TarpTurn,
        };

        public static TarpBox Moved(TarpBox box, float dx, float dz, float dy)
        {
            box.MinX += dx;
            box.MaxX += dx;
            box.MinZ += dz;
            box.MaxZ += dz;
            box.Low += dy;
            box.High += dy;
            return box;
        }

        public static TarpBox CentredAt(TarpBox box, float x, float z) =>
            Moved(box, x - (box.MinX + box.MaxX) / 2f, z - (box.MinZ + box.MaxZ) / 2f, 0f);

        public static TarpBox Tidy(TarpBox box)
        {
            if (box.MaxX - box.MinX < Smallest)
                box.MaxX = box.MinX + Smallest;
            if (box.MaxZ - box.MinZ < Smallest)
                box.MaxZ = box.MinZ + Smallest;
            if (box.Low > box.High)
                box.High = box.Low;
            return box;
        }

        private static readonly string[] Names = { "TarpMinX", "TarpMaxX", "TarpMinZ", "TarpMaxZ", "TarpLow", "TarpHigh", "TarpTurn" };

        private static float[] Values(TarpBox b) => new[] { b.MinX, b.MaxX, b.MinZ, b.MaxZ, b.Low, b.High, b.Turn };

        public static string Format(TarpBox box) =>
            string.Join(" ", Names.Zip(Values(box), (name, value) => $"{name} = {Math.Round(value, 2).ToString(CultureInfo.InvariantCulture)}f,"));

        public static bool TryParse(string text, out TarpBox box)
        {
            box = default;
            var found = new Dictionary<string, float>();
            foreach (Match match in Regex.Matches(text ?? "", @"(\w+)\s*=\s*(-?[0-9.]+)f?"))
                if (float.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                    found[match.Groups[1].Value] = value;
            if (!Names.All(found.ContainsKey))
                return false;
            box = new TarpBox
            {
                MinX = found["TarpMinX"], MaxX = found["TarpMaxX"], MinZ = found["TarpMinZ"], MaxZ = found["TarpMaxZ"],
                Low = found["TarpLow"], High = found["TarpHigh"], Turn = found["TarpTurn"],
            };
            return true;
        }
    }
}
