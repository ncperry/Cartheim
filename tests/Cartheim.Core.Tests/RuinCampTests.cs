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
        public void A_sconce_hangs_on_the_outside_of_the_front_wall_either_side_of_the_door()
        {
            var camp = RuinCamp.For("StoneHouse4");
            Assert.Equal(2, camp.Sconces.Count);
            Assert.Contains(camp.Sconces, x => x < camp.DoorX - 0.65f);
            Assert.Contains(camp.Sconces, x => x > camp.DoorX + 0.65f);
            // On the wall's outer face, pointing out, at about head height.
            Assert.True(camp.SconceZ < camp.DoorZ);
            Assert.InRange(camp.SconceHeight, 1.5f, 2.2f);
        }

        [Fact]
        public void A_bronze_shield_hangs_on_the_wall_above_the_bed()
        {
            var camp = RuinCamp.For("StoneHouse4");
            Assert.Equal(camp.RoomMinX, camp.ShieldX);
            Assert.True(System.Math.Abs(camp.ShieldX - camp.BedX) <= 1.2f);
            Assert.Equal(camp.BedZ, camp.ShieldZ);
            Assert.InRange(camp.ShieldHeight, 1.3f, 2.2f);
            // Held off the rough stone a little, so none of it sinks into the wall.
            Assert.InRange(RuinCamp.WallOff, 0.15f, 0.4f);
            Assert.Equal("ShieldBronzeBuckler", RuinCamp.Shield);
        }

        [Fact]
        public void Black_forest_trophies_hang_round_the_room_clear_of_the_door()
        {
            var camp = RuinCamp.For("StoneHouse4");
            Assert.Contains(camp.Hangings, h => h.Item == "TrophyForestTroll");
            Assert.True(camp.Hangings.Count >= 2);
            foreach (var h in camp.Hangings)
            {
                // On the inside of a wall that stands (the back wall is broken: the cart's there), looking into the room.
                var onLeft = h.X == camp.RoomMinX && h.FaceX == 1f;
                var onRight = h.X == camp.RoomMaxX && h.FaceX == -1f;
                var onFront = h.Z == camp.RoomMinZ && h.FaceZ == 1f;
                Assert.True(onLeft || onRight || onFront, h.Item);
                Assert.InRange(h.X, camp.RoomMinX, camp.RoomMaxX);
                Assert.InRange(h.Z, camp.RoomMinZ, camp.RoomMaxZ);
                Assert.InRange(h.Height, 1.4f, 2.3f);
                if (onFront)
                    Assert.True(System.Math.Abs(h.X - camp.DoorX) >= 1.2f, h.Item);
                // Not where the shield goes.
                Assert.False(h.X == camp.ShieldX && System.Math.Abs(h.Z - camp.ShieldZ) < 1f, h.Item);
            }
        }

        [Fact]
        public void A_suit_of_bronze_armour_stands_by_the_right_wall_looking_into_the_room()
        {
            var camp = RuinCamp.For("StoneHouse4");
            Assert.Equal(new[] { "HelmetBronze", "ArmorBronzeChest", "ArmorBronzeLegs" }, RuinCamp.Armour);
            Assert.InRange(camp.ArmourX, camp.RoomMaxX - 0.9f, camp.RoomMaxX - 0.3f);
            Assert.InRange(camp.ArmourZ, camp.RoomMinZ, camp.RoomMaxZ);
            Assert.Equal(270f, camp.ArmourYaw);
            // Clear of the fire, and off the rug.
            Assert.True(Distance(camp.ArmourX, camp.ArmourZ, camp.FireX, camp.FireZ) >= 1.5f);
            Assert.False(OnRug(camp, camp.ArmourX, camp.ArmourZ));
        }

        [Fact]
        public void Thistles_grow_in_the_room_but_not_through_the_rug_nor_in_the_way()
        {
            var camp = RuinCamp.For("StoneHouse4");
            Assert.Equal("Pickable_Thistle", RuinCamp.Thistle);
            Assert.True(camp.Thistles.Count >= 3);
            foreach (var (x, z) in camp.Thistles)
            {
                Assert.InRange(x, camp.RoomMinX + 0.2f, camp.RoomMaxX - 0.2f);
                Assert.InRange(z, camp.RoomMinZ + 0.2f, camp.RoomMaxZ - 0.2f);
                Assert.False(OnRug(camp, x, z), $"{x},{z}");
                Assert.True(Distance(x, z, camp.FireX, camp.FireZ) >= 1f, $"{x},{z}");
                Assert.True(Distance(x, z, camp.ArmourX, camp.ArmourZ) >= 0.7f, $"{x},{z}");
                // Not in the way in from the door.
                Assert.True(System.Math.Abs(x - camp.DoorX) >= 0.8f, $"{x},{z}");
            }
        }

        private static bool OnRug(RuinCamp camp, float x, float z) =>
            System.Math.Abs(x - camp.RugX) <= camp.RugSize / 2f + 0.1f && System.Math.Abs(z - camp.RugZ) <= camp.RugSize / 2f + 0.1f;

        private static float Distance(float x1, float z1, float x2, float z2) =>
            (float)System.Math.Sqrt((x1 - x2) * (x1 - x2) + (z1 - z2) * (z1 - z2));

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
        public void The_camp_isnt_torn_down_while_anything_is_on_its_cooking_station()
        {
            Assert.True(RuinCamp.CanTearDown(itemsCooking: 0, fuel: 0f));
            Assert.False(RuinCamp.CanTearDown(itemsCooking: 1, fuel: 0f));
            Assert.False(RuinCamp.CanTearDown(itemsCooking: 0, fuel: 1f));
        }

        [Fact]
        public void A_cart_settles_into_a_ruin_within_twenty_metres()
        {
            Assert.True(RuinCamp.Near(20f));
            Assert.False(RuinCamp.Near(20.5f));
        }
    }
}
