using System.Linq;
using Cartheim.Core;
using Xunit;

namespace Cartheim.Core.Tests
{
    public class RuinCampTests
    {
        [Fact]
        public void The_stone_house_ruin_has_a_camp_and_other_places_do_not()
        {
            Assert.NotNull(RuinCamp.For("StoneHouse4(Clone)"));
            Assert.NotNull(RuinCamp.For("StoneHouse4"));
            Assert.Null(RuinCamp.For("Vendor_BlackForest(Clone)"));
            Assert.Null(RuinCamp.For(null));
        }

        [Fact]
        public void In_the_stone_house_the_cart_stands_across_the_break_in_the_back_wall()
        {
            var camp = RuinCamp.For("StoneHouse4");
            Assert.InRange(camp.CartX, -0.64f, 2.29f);
            Assert.InRange(camp.CartZ, 3.2f, 4.5f);
            Assert.Equal(90f, camp.CartYaw);
        }

        [Fact]
        public void Its_door_goes_in_the_arch()
        {
            var camp = RuinCamp.For("StoneHouse4");
            Assert.Equal(0f, camp.DoorX);
            Assert.Equal(-1.8f, camp.DoorZ);
        }

        [Fact]
        public void A_wood_fence_runs_from_the_back_walls_stub_round_the_cart_to_the_side_wall()
        {
            var camp = RuinCamp.For("StoneHouse4");
            var first = camp.Fence.First();
            var last = camp.Fence.Last();
            Assert.Equal((-1.7f, 3.2f), first.from);
            Assert.Equal((3.5f, 4.05f), last.to);
            // Each run starts where the last ended.
            Assert.All(camp.Fence.Zip(camp.Fence.Skip(1), (a, b) => (a, b)), p => Assert.Equal(p.a.to, p.b.from));
        }

        [Fact]
        public void A_garden_is_tried_on_the_left_then_the_right_then_the_front()
        {
            var gardens = RuinCamp.For("StoneHouse4").Gardens;
            Assert.True(gardens[0].x < -4.71f);
            Assert.True(gardens[1].x > 4.71f);
            Assert.True(gardens[2].z < -3.01f);
        }

        [Fact]
        public void No_garden_spot_with_its_torches_stands_in_the_way_out_of_the_door()
        {
            var camp = RuinCamp.For("StoneHouse4");
            // A path two and a half metres wide, from the door five metres out.
            const float pathHalf = 1.25f, pathFrom = -1.8f, pathTo = -6.8f;
            foreach (var (x, z, yaw) in camp.Gardens)
            {
                var turned = System.Math.Abs(yaw - 90f) < 1f || System.Math.Abs(yaw - 270f) < 1f;
                var across = Garden.Width / 2f + Garden.TorchOut + 0.3f;
                var along = Garden.Depth / 2f + Garden.TorchOut + 0.3f;
                var halfX = turned ? along : across;
                var halfZ = turned ? across : along;
                var overlaps = x - halfX < pathHalf && x + halfX > -pathHalf && z - halfZ < pathFrom && z + halfZ > pathTo;
                Assert.False(overlaps, $"the garden at ({x}, {z}) is in the way out of the door");
            }
        }

        [Fact]
        public void A_fence_run_is_covered_end_to_end_with_pieces_turned_along_it()
        {
            var pieces = FenceRun.Pieces((0f, 0f), (0f, 5f), pieceLength: 2f);
            Assert.Equal(3, pieces.Count);
            Assert.All(pieces, p => Assert.Equal(0f, p.x, 3));
            Assert.Equal(1f, pieces.First().z, 3);
            Assert.Equal(4f, pieces.Last().z, 3);
            Assert.All(pieces, p => Assert.Equal(90f, p.yaw, 3));
            Assert.All(FenceRun.Pieces((0f, 0f), (5f, 0f), 2f), p => Assert.Equal(0f, p.yaw, 3));
        }

        [Fact]
        public void A_cart_settles_into_a_ruin_within_twenty_metres()
        {
            Assert.True(RuinCamp.Near(20f));
            Assert.False(RuinCamp.Near(20.5f));
        }
    }
}
