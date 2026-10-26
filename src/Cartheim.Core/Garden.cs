using System;
using System.Collections.Generic;
using System.Linq;

namespace Cartheim.Core
{
    // The Wayside Garden: set out by the cart for anyone carrying a cultivator, a square of cultivated ground inside a
    // stone fence with a wooden gate. It stays where it's put, made of real pieces: torn down with the hammer like any
    // fence, which gives back what they cost, so placing one costs exactly its fences and gate. The ground stays
    // cultivated.
    public static class Garden
    {
        // The tab's name, and the side of the square inside the fence (metres).
        public const string Piece = "cartheim_garden";
        public const float Side = 4f;

        // A fence piece or the gate: where its middle is, in the garden's own space (its middle at the origin, its front,
        // with the gate, towards -z), and how it's turned (0 along x, 90 along z).
        public struct Part
        {
            public float X, Z, Yaw;
            public bool Gate;
        }

        public static List<Part> Layout(float fenceLength, float gateWidth)
        {
            var half = Side / 2f;
            var parts = new List<Part>();
            parts.AddRange(Run(-half, half, fenceLength).Select(x => new Part { X = x, Z = half }));
            parts.AddRange(Run(-half, half, fenceLength).Select(z => new Part { X = half, Z = z, Yaw = 90f }));
            parts.AddRange(Run(-half, half, fenceLength).Select(z => new Part { X = -half, Z = z, Yaw = 90f }));
            parts.AddRange(Run(-half, -gateWidth / 2f, fenceLength).Select(x => new Part { X = x, Z = -half }));
            parts.AddRange(Run(gateWidth / 2f, half, fenceLength).Select(x => new Part { X = x, Z = -half }));
            parts.Add(new Part { X = 0f, Z = -half, Gate = true });
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

        public static List<ItemCount> Cost(IEnumerable<ItemCount> fence, int fences, IEnumerable<ItemCount> gate) =>
            fence.Select(c => new ItemCount(c.Prefab, c.Count * fences)).Concat(gate)
                .GroupBy(c => c.Prefab).Select(g => new ItemCount(g.Key, g.Sum(c => c.Count))).ToList();

        // Where the ground is cultivated, each spot cultivating about a spacing around it: across the whole inside.
        public static List<(float x, float z)> CultivatePoints(float spacing)
        {
            var half = Side / 2f;
            var count = Math.Max(1, (int)Math.Ceiling(Side / spacing));
            var step = Side / count;
            var points = new List<(float, float)>();
            for (var i = 0; i < count; i++)
                for (var j = 0; j < count; j++)
                    points.Add((-half + step * (i + 0.5f), -half + step * (j + 0.5f)));
            return points;
        }
    }
}
