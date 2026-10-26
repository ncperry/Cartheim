using System;
using System.Collections.Generic;
using System.Linq;

namespace Cartheim.Core
{
    // The Wayside Garden: set out by the cart for anyone carrying a cultivator, a plot of cultivated ground inside a
    // stone fence with a wooden gate and a standing torch at each front corner, planted with a crop if they like. It
    // stays where it's put, made of real pieces: torn down with the hammer like any fence, which gives back what they
    // cost, so placing one costs exactly its pieces, plus what's planted. The ground stays cultivated.
    public static class Garden
    {
        // The tab's name, and the plot inside the fence (metres): its width across the front, with the gate, and its depth.
        public const string Piece = "cartheim_garden";
        public const float Width = 5f;
        public const float Depth = 7f;

        // A fence piece or the gate: where its middle is, in the garden's own space (its middle at the origin, its front,
        // with the gate, towards -z), and how it's turned (0 along x, 90 along z).
        public struct Part
        {
            public float X, Z, Yaw;
            public bool Gate;
        }

        public static List<Part> Layout(float fenceLength, float gateWidth)
        {
            var w = Width / 2f;
            var d = Depth / 2f;
            var parts = new List<Part>();
            parts.AddRange(Run(-w, w, fenceLength).Select(x => new Part { X = x, Z = d }));
            parts.AddRange(Run(-d, d, fenceLength).Select(z => new Part { X = w, Z = z, Yaw = 90f }));
            parts.AddRange(Run(-d, d, fenceLength).Select(z => new Part { X = -w, Z = z, Yaw = 90f }));
            parts.AddRange(Run(-w, -gateWidth / 2f, fenceLength).Select(x => new Part { X = x, Z = -d }));
            parts.AddRange(Run(gateWidth / 2f, w, fenceLength).Select(x => new Part { X = x, Z = -d }));
            parts.Add(new Part { X = 0f, Z = -d, Gate = true });
            return parts;
        }

        // The middles of fence pieces covering from a to b end to end (overlapping a little where they don't fit exactly).
        private static IEnumerable<float> Run(float a, float b, float fenceLength)
        {
            var length = b - a;
            if (length <= 0.01f)
                yield break;
            var count = Math.Max(1, (int)Math.Ceiling(length / fenceLength - 0.001f));
            if (count == 1)
            {
                yield return (a + b) / 2f;
                yield break;
            }
            var step = (length - fenceLength) / (count - 1);
            for (var i = 0; i < count; i++)
                yield return a + fenceLength / 2f + i * step;
        }

        // Each recipe times how many are built or planted with it, added up.
        public static List<ItemCount> Cost(params (IEnumerable<ItemCount> recipe, int times)[] parts) =>
            parts.SelectMany(p => p.recipe.Select(c => new ItemCount(c.Prefab, c.Count * p.times)))
                .Where(c => c.Count > 0)
                .GroupBy(c => c.Prefab).Select(g => new ItemCount(g.Key, g.Sum(c => c.Count))).ToList();

        // A standing wood torch just outside each front corner, either side of the gate.
        private const float TorchOut = 0.6f;

        public static List<(float x, float z)> TorchSpots()
        {
            var x = Width / 2f + TorchOut;
            var z = -(Depth / 2f + TorchOut);
            return new List<(float, float)> { (-x, z), (x, z) };
        }

        // Planted with the garden: a crop that has a seed plant (carrots: carrot seeds grow carrots, and a carrot planted
        // grows seed carrots, for more seeds) eighteen of the crop and six for seed; one without, twenty-four.
        public const int Plants = 24;
        public const int SeedPlants = 6;

        public static (int crops, int seedPlants) Planting(bool hasSeedPlant) =>
            hasSeedPlant ? (Plants - SeedPlants, SeedPlants) : (Plants, 0);

        // Where they're planted: rows of four across, as far apart as each needs to grow, but no nearer the fence than this.
        private const int PerRow = 4;
        private const float FromFence = 0.5f;

        public static List<(float x, float z)> PlantSpots(int count, float spacing)
        {
            var rows = (count + PerRow - 1) / PerRow;
            var across = Math.Min(spacing, (Width - 2 * FromFence) / Math.Max(1, PerRow - 1));
            var along = Math.Min(spacing, (Depth - 2 * FromFence) / Math.Max(1, rows - 1));
            var spots = new List<(float, float)>();
            for (var i = 0; i < count; i++)
            {
                var row = i / PerRow;
                var inRow = Math.Min(PerRow, count - row * PerRow);
                var column = i % PerRow;
                spots.Add(((column - (inRow - 1) / 2f) * across, (row - (rows - 1) / 2f) * along));
            }
            return spots;
        }

        // Where the ground is cultivated, each spot cultivating about a spacing around it: across the whole inside.
        public static List<(float x, float z)> CultivatePoints(float spacing)
        {
            var across = Math.Max(1, (int)Math.Ceiling(Width / spacing));
            var along = Math.Max(1, (int)Math.Ceiling(Depth / spacing));
            var points = new List<(float, float)>();
            for (var i = 0; i < across; i++)
                for (var j = 0; j < along; j++)
                    points.Add((-Width / 2f + Width / across * (i + 0.5f), -Depth / 2f + Depth / along * (j + 0.5f)));
            return points;
        }
    }
}
