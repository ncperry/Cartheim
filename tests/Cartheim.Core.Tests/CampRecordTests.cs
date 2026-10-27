using System.Linq;
using Cartheim.Core;
using Xunit;

namespace Cartheim.Core.Tests
{
    public class CampRecordTests
    {
        private const string Record =
            "# Camp at WoodHouse3: recorded 2026-10-26 19:41, the building at (-26.48, 34.80, -6.48), turned 76.1, reaching 10.0 m\n" +
            "# whose | prefab | x | y | z | yaw | what it is | what's on it\n" +
            "player | bed | 1.89 | 0.48 | 3.73 | -61 | Bed,CartRider,CartRider | made of 8 Wood\n" +
            "player | fire_pit | 0.00 | 0.00 | 0.12 | 14 | Fireplace,CinderSpawner | fuel 0.7; made of 5 Stone, 2 Wood\n" +
            "player | Raspberry | -2.08 | 0.87 | 4.99 | 171 | ItemDrop | made of 1 Raspberry(Clone)\n" +
            "building | Beehive | -3.11 | 1.16 | 1.47 | 121 | SpawnOnDamaged,DropOnDestroyed,Aoe | \n" +
            "cart | Cart | 1.87 | -0.01 | 3.82 | -61 | Vagon,CartBase | made of 20 Wood\n";

        [Fact]
        public void A_recorded_camp_has_what_the_player_built_there_but_not_what_rides_in_the_cart_nor_the_buildings_own()
        {
            var camp = CampRecords.Parse("WoodHouse3", Record);
            Assert.Equal(new[] { "fire_pit", "Raspberry" }, camp.Recorded.Select(p => p.Prefab));
            var fire = camp.Recorded.First();
            Assert.Equal((0f, 0f, 0.12f, 14f), (fire.X, fire.Y, fire.Z, fire.Yaw));
        }

        [Fact]
        public void The_cart_stands_where_it_was_recorded_and_the_camp_reaches_as_far_as_the_building()
        {
            var camp = CampRecords.Parse("WoodHouse3", Record);
            Assert.Equal((1.87f, 3.82f, -61f), (camp.CartX, camp.CartZ, camp.CartYaw));
            Assert.Equal(10f, camp.Area);
            Assert.Equal("WoodHouse3", camp.Name);
        }

        [Fact]
        public void A_recorded_camp_has_no_tarp_nor_room_of_its_own()
        {
            var camp = CampRecords.Parse("WoodHouse3", Record);
            Assert.False(camp.HasTarp);
            Assert.False(camp.HasRoom);
            Assert.True(RuinCamp.For("StoneHouse4").HasTarp);
        }

        [Fact]
        public void The_wood_house_camp_pitches_the_carts_tent_and_its_outer_chair_faces_the_table()
        {
            var camp = RuinCamp.For("WoodHouse3");
            Assert.True(camp.PitchesTent);
            Assert.False(CampRecords.Parse("Other", "").PitchesTent);
            var outer = camp.Recorded.Where(p => p.Prefab == "piece_chair").OrderByDescending(p => p.X * p.X + p.Z * p.Z).First();
            Assert.Equal(149f, outer.Yaw);
        }

        [Fact]
        public void A_rug_laid_in_a_recorded_camp_marks_where_the_carts_rug_goes_and_the_buildings_first_bed_where_the_carts_bed_goes()
        {
            var record = "# Camp at X: reaching 8.0 m\n" +
                         "player | rug_deer | -0.06 | 0.10 | -0.79 | 180 | MaterialManNotifier | made of 4 DeerHide\n" +
                         "player | rug_deer | -6.42 | 0.56 | 3.40 | 29 | CartRider,CartRider | made of 4 DeerHide\n" +
                         "building | goblin_bed | -1.67 | 0.00 | -2.19 | -1 | RandomSpawn | \n" +
                         "building | goblin_bed | 1.58 | 0.00 | -2.19 | 3 | RandomSpawn | \n";
            var camp = CampRecords.Parse("X", record);
            Assert.Empty(camp.Recorded);
            Assert.Equal((-0.06f, 0.10f, -0.79f, 180f), (camp.RugSpot.X, camp.RugSpot.Y, camp.RugSpot.Z, camp.RugSpot.Yaw));
            Assert.Equal("rug_deer", camp.RugSpot.Prefab);
            Assert.Equal("goblin_bed", camp.BedSpot.Prefab);
            Assert.Equal(-1.67f, camp.BedSpot.X);
            // The second: a spare, made a real bed for good.
            Assert.Equal(1.58f, camp.SpareBedSpot.X);
            Assert.True(camp.HasRoom);
            Assert.False(CampRecords.Parse("Y", "").HasRoom);
        }

        [Fact]
        public void The_second_wood_house_camp_ships_with_its_shelter_and_its_room()
        {
            var camp = RuinCamp.For("WoodHouse2");
            Assert.Contains(camp.Recorded, p => p.Prefab == "wood_wall_roof_top_45");
            Assert.DoesNotContain(camp.Recorded, p => p.Prefab.StartsWith("rug_"));
            Assert.NotNull(camp.RugSpot);
            Assert.NotNull(camp.BedSpot);
        }

        [Fact]
        public void The_first_wood_house_camp_is_the_seconds_with_its_shelter_a_metre_lower_to_meet_its_lower_back_wall()
        {
            var first = RuinCamp.For("WoodHouse1");
            var second = RuinCamp.For("WoodHouse2");
            float Top(RuinCamp camp, string prefab) => camp.Recorded.First(p => p.Prefab == prefab).Y;
            Assert.Equal(Top(second, "wood_roof_top_45") - 1f, Top(first, "wood_roof_top_45"), 3);
            Assert.Equal(Top(second, "wood_beam") - 1f, Top(first, "wood_beam"), 3);
            Assert.DoesNotContain(first.Recorded, p => p.Prefab == "wood_pole");
            Assert.Equal(second.Recorded.Count(p => p.Prefab == "piece_table"), first.Recorded.Count(p => p.Prefab == "piece_table"));
            Assert.NotNull(first.BedSpot);
            Assert.NotNull(first.RugSpot);
            // Its roof comes down further in: the bench, and the turnip lantern on it, stand in from the wall.
            Assert.True(first.Recorded.First(p => p.Prefab == "piece_bench01").X >= -1.7f);
            Assert.True(first.Recorded.First(p => p.Prefab == "piece_jackoturnip").X >= -1.5f);
        }

        [Fact]
        public void The_wood_house_camp_ships_with_the_mod_with_its_table_set_but_not_to_be_eaten_from_but_the_berries()
        {
            var camp = RuinCamp.For("WoodHouse3(Clone)");
            Assert.NotNull(camp);
            Assert.Contains(camp.Recorded, p => p.Prefab == "piece_sharpstakes");
            Assert.Contains("FeastMeadows", camp.Untouchable);
            Assert.Contains("MeadStaminaLingering", camp.Untouchable);
            Assert.DoesNotContain("Raspberry", camp.Untouchable);
        }
    }
}
