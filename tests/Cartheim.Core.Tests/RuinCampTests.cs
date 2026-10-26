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
        public void The_cart_faces_into_the_house_so_its_sconce_lights_the_room()
        {
            Assert.Equal(270f, RuinCamp.For("StoneHouse4").CartYaw);
        }

        [Fact]
        public void The_campfire_is_in_a_corner_of_the_room_under_the_tarp_by_a_smoke_vent()
        {
            var camp = RuinCamp.For("StoneHouse4");
            Assert.InRange(camp.FireX, camp.RoomMinX, camp.RoomMaxX);
            Assert.InRange(camp.FireZ, camp.RoomMinZ, camp.RoomMaxZ);
            // Under the tarp, so the rain doesn't put it out...
            Assert.InRange(camp.FireX, camp.TarpMinX, camp.TarpMaxX - 0.3f);
            Assert.InRange(camp.FireZ, camp.TarpMinZ, camp.TarpMaxZ);
            // ...whose high side stops short of the wall, so the smoke rising up it gets out.
            Assert.True(camp.RoomMaxX - camp.TarpMaxX >= 0.4f);
            // Tucked into a corner, against the side wall and the front.
            Assert.True(camp.RoomMaxX - camp.FireX <= 0.9f && camp.FireZ - camp.RoomMinZ <= 0.9f);
        }

        [Fact]
        public void The_rug_covers_much_of_the_room_and_the_bed_lies_on_it()
        {
            var camp = RuinCamp.For("StoneHouse4");
            var half = camp.RugSize / 2f;
            Assert.True(camp.RugSize >= 3f);
            Assert.True(camp.RugX - half >= camp.RoomMinX && camp.RugX + half <= camp.RoomMaxX);
            Assert.True(camp.RugZ - half >= camp.RoomMinZ && camp.RugZ + half <= camp.RoomMaxZ);
            Assert.InRange(camp.BedX, camp.RugX - half, camp.RugX + half);
            Assert.InRange(camp.BedZ, camp.RugZ - half, camp.RugZ + half);
        }

        [Fact]
        public void The_way_in_from_the_door_is_kept_clear_of_the_fire()
        {
            var camp = RuinCamp.For("StoneHouse4");
            Assert.True(System.Math.Abs(camp.FireX - camp.DoorX) >= 1.5f);
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
