using System.Linq;
using Cartheim.Core;
using Xunit;

namespace Cartheim.Core.Tests
{
    public class GardenTests
    {
        private const float HalfW = Garden.Width / 2f, HalfD = Garden.Depth / 2f;

        [Fact]
        public void A_garden_is_fenced_on_every_side_with_one_gate_in_the_middle_of_its_front()
        {
            var layout = Garden.Layout(fenceLength: 2f, gateWidth: 2f);
            var gate = Assert.Single(layout.Where(p => p.Gate));
            Assert.Equal(0f, gate.X, 3);
            Assert.Equal(-HalfD, gate.Z, 3);
            Assert.Contains(layout, p => !p.Gate && p.Z == HalfD);
            Assert.Contains(layout, p => !p.Gate && p.X == HalfW);
            Assert.Contains(layout, p => !p.Gate && p.X == -HalfW);
            Assert.Contains(layout, p => !p.Gate && p.Z == -HalfD);
        }

        [Fact]
        public void The_fence_leaves_no_gap_along_a_side()
        {
            const float fence = 1.5f;
            var back = Garden.Layout(fence, gateWidth: 2f).Where(p => p.Z == HalfD).Select(p => p.X).OrderBy(x => x).ToList();
            Assert.True(back.First() - fence / 2f <= -HalfW + 0.001f);
            Assert.True(back.Last() + fence / 2f >= HalfW - 0.001f);
            Assert.All(back.Zip(back.Skip(1), (a, b) => b - a), step => Assert.True(step <= fence + 0.001f));
        }

        [Fact]
        public void Fences_along_the_sides_are_turned_across()
        {
            var layout = Garden.Layout(fenceLength: 2f, gateWidth: 2f);
            Assert.All(layout.Where(p => p.X == HalfW || p.X == -HalfW), p => Assert.Equal(90f, p.Yaw));
            Assert.All(layout.Where(p => p.Z == HalfD || p.Z == -HalfD), p => Assert.Equal(0f, p.Yaw));
        }

        [Fact]
        public void A_garden_is_five_metres_wide_and_seven_deep_room_for_four_by_six_crops_a_metre_apart()
        {
            Assert.Equal(5f, Garden.Width);
            Assert.Equal(7f, Garden.Depth);
            // A crop grows only with no fence within its growing radius (half a metre), so about 0.6 m from the fence.
            Assert.True(Garden.Width - 1.2f >= 3f && Garden.Depth - 1.2f >= 5f);
        }

        [Fact]
        public void A_garden_costs_what_its_pieces_and_its_planting_cost()
        {
            var cost = Garden.Cost(
                (new[] { new ItemCount("Stone", 4) }, 7),
                (new[] { new ItemCount("Wood", 4), new ItemCount("Stone", 1) }, 1),
                (new[] { new ItemCount("Wood", 2), new ItemCount("Resin", 2) }, 2),
                (new[] { new ItemCount("CarrotSeeds", 1) }, 15),
                (new[] { new ItemCount("Carrot", 1) }, 5));
            Assert.Equal(29, cost.Single(c => c.Prefab == "Stone").Count);
            Assert.Equal(8, cost.Single(c => c.Prefab == "Wood").Count);
            Assert.Equal(4, cost.Single(c => c.Prefab == "Resin").Count);
            Assert.Equal(15, cost.Single(c => c.Prefab == "CarrotSeeds").Count);
            Assert.Equal(5, cost.Single(c => c.Prefab == "Carrot").Count);
        }

        [Fact]
        public void A_standing_torch_stands_just_outside_each_front_corner()
        {
            var torches = Garden.TorchSpots();
            Assert.Equal(2, torches.Count);
            Assert.All(torches, t => Assert.True(t.z < -HalfD && System.Math.Abs(t.x) > HalfW));
            Assert.Contains(torches, t => t.x < 0);
            Assert.Contains(torches, t => t.x > 0);
        }

        [Fact]
        public void A_crop_with_a_seed_plant_is_planted_eighteen_and_six()
        {
            Assert.Equal((18, 6), Garden.Planting(hasSeedPlant: true));
            Assert.Equal((24, 0), Garden.Planting(hasSeedPlant: false));
        }

        [Fact]
        public void Twenty_four_plants_go_in_six_rows_of_four_each_with_room_to_grow_clear_of_the_fence()
        {
            const float spacing = 1f;
            var spots = Garden.PlantSpots(24, spacing);
            Assert.Equal(24, spots.Count);
            Assert.Equal(4, spots.Select(s => s.x).Distinct().Count());
            Assert.Equal(6, spots.Select(s => s.z).Distinct().Count());
            AllInside(spots, margin: 0.6f);
            foreach (var a in spots)
                foreach (var b in spots.Where(b => b != a))
                    Assert.True(System.Math.Sqrt((a.x - b.x) * (a.x - b.x) + (a.z - b.z) * (a.z - b.z)) >= spacing - 0.001f);
        }

        [Fact]
        public void Plants_wanting_more_room_than_the_garden_has_are_squeezed_in_rather_than_planted_in_the_fence()
        {
            AllInside(Garden.PlantSpots(24, spacing: 2f), margin: 0.4f);
        }

        private static void AllInside(System.Collections.Generic.List<(float x, float z)> spots, float margin) =>
            Assert.All(spots, s => Assert.True(System.Math.Abs(s.x) <= HalfW - margin + 0.001f && System.Math.Abs(s.z) <= HalfD - margin + 0.001f));

        [Fact]
        public void Its_whole_inside_is_cultivated()
        {
            var points = Garden.CultivatePoints(spacing: 1f);
            foreach (var x in new[] { -HalfW, 0f, HalfW })
                foreach (var z in new[] { -HalfD, 0f, HalfD })
                    Assert.Contains(points, p => System.Math.Abs(p.x - x) <= 1f && System.Math.Abs(p.z - z) <= 1f);
            Assert.All(points, p => Assert.True(System.Math.Abs(p.x) <= HalfW && System.Math.Abs(p.z) <= HalfD));
        }
    }
}
