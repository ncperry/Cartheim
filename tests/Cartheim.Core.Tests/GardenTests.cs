using System.Linq;
using Cartheim.Core;
using Xunit;

namespace Cartheim.Core.Tests
{
    public class GardenTests
    {
        private const float Half = Garden.Side / 2f;

        [Fact]
        public void A_garden_is_fenced_on_every_side_with_one_gate_in_the_middle_of_its_front()
        {
            var layout = Garden.Layout(fenceLength: 2f, gateWidth: 2f);
            var gate = Assert.Single(layout.Where(p => p.Gate));
            Assert.Equal(0f, gate.X, 3);
            Assert.Equal(-Half, gate.Z, 3);
            Assert.Contains(layout, p => !p.Gate && p.Z == Half);
            Assert.Contains(layout, p => !p.Gate && p.X == Half);
            Assert.Contains(layout, p => !p.Gate && p.X == -Half);
            Assert.Contains(layout, p => !p.Gate && p.Z == -Half);
        }

        [Fact]
        public void The_fence_leaves_no_gap_along_a_side()
        {
            const float fence = 1.5f;
            var back = Garden.Layout(fence, gateWidth: 2f).Where(p => p.Z == Half).Select(p => p.X).OrderBy(x => x).ToList();
            Assert.True(back.First() - fence / 2f <= -Half + 0.001f);
            Assert.True(back.Last() + fence / 2f >= Half - 0.001f);
            Assert.All(back.Zip(back.Skip(1), (a, b) => b - a), step => Assert.True(step <= fence + 0.001f));
        }

        [Fact]
        public void Fences_along_the_sides_are_turned_across()
        {
            var layout = Garden.Layout(fenceLength: 2f, gateWidth: 2f);
            Assert.All(layout.Where(p => p.X == Half || p.X == -Half), p => Assert.Equal(90f, p.Yaw));
            Assert.All(layout.Where(p => p.Z == Half || p.Z == -Half), p => Assert.Equal(0f, p.Yaw));
        }

        [Fact]
        public void A_garden_costs_what_its_fences_and_gate_cost()
        {
            var cost = Garden.Cost(new[] { new ItemCount("Stone", 4) }, fences: 7, new[] { new ItemCount("Wood", 4), new ItemCount("Stone", 1) });
            Assert.Equal(29, cost.Single(c => c.Prefab == "Stone").Count);
            Assert.Equal(4, cost.Single(c => c.Prefab == "Wood").Count);
        }

        [Fact]
        public void Its_whole_inside_is_cultivated()
        {
            var points = Garden.CultivatePoints(spacing: 1f);
            foreach (var x in new[] { -Half, 0f, Half })
                foreach (var z in new[] { -Half, 0f, Half })
                    Assert.Contains(points, p => System.Math.Abs(p.x - x) <= 1f && System.Math.Abs(p.z - z) <= 1f);
            Assert.All(points, p => Assert.True(System.Math.Abs(p.x) <= Half && System.Math.Abs(p.z) <= Half));
        }
    }
}
