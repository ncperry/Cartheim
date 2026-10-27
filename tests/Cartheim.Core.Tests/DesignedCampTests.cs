using System.Linq;
using Cartheim.Core;
using Xunit;

namespace Cartheim.Core.Tests
{
    public class DesignedCampTests
    {
        private static readonly int[] Houses = Enumerable.Range(1, 13).ToArray();

        [Fact]
        public void Every_wood_house_has_a_camp_with_a_fire_to_cook_over_and_a_place_for_the_cart()
        {
            foreach (var n in Houses)
            {
                var camp = RuinCamp.For("WoodHouse" + n);
                Assert.True(camp != null, $"WoodHouse{n}");
                Assert.Contains(camp.Recorded, p => p.Prefab == "fire_pit");
                Assert.Equal(2, camp.Recorded.Count(p => p.Prefab == "piece_cookingstation"));
                Assert.True(camp.CartX != 0f || camp.CartZ != 0f, $"WoodHouse{n} has no place for the cart");
            }
        }

        [Fact]
        public void A_camp_without_a_bed_of_its_own_pitches_the_carts_tent()
        {
            foreach (var n in Houses)
            {
                var camp = RuinCamp.For("WoodHouse" + n);
                Assert.True(camp.BedSpot != null || camp.PitchesTent, $"WoodHouse{n}");
            }
        }

        [Fact]
        public void A_feast_is_set_out_to_look_at_but_the_seeds_on_the_table_are_to_take()
        {
            foreach (var house in new[] { "WoodHouse9", "WoodHouse11" })
            {
                var camp = RuinCamp.For(house);
                Assert.Contains("FeastMeadows", camp.Untouchable);
                Assert.Contains(camp.Recorded, p => p.Prefab == "CarrotSeeds");
                Assert.DoesNotContain("CarrotSeeds", camp.Untouchable);
            }
            Assert.Contains(RuinCamp.For("WoodHouse10").Recorded, p => p.Prefab == "TurnipSeeds");
            Assert.Contains(RuinCamp.For("WoodHouse13").Recorded, p => p.Prefab == "Blueberries");
        }

        [Fact]
        public void The_watchtower_is_ringed_with_stakes_but_for_a_way_in()
        {
            var camp = RuinCamp.For("WoodHouse6");
            var stakes = camp.Recorded.Where(p => p.Prefab == "piece_sharpstakes").ToList();
            Assert.Equal(13, stakes.Count);
            Assert.All(stakes, s => Assert.InRange(System.Math.Sqrt(s.X * s.X + s.Z * s.Z), 6.4, 6.6));
            // The way in faces the cart.
            Assert.DoesNotContain(stakes, s => s.Z < -6f);
            Assert.True(camp.CartZ < -6f);
        }
    }
}
