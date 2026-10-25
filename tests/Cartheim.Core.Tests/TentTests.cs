using Cartheim.Core;
using Xunit;

namespace Cartheim.Core.Tests
{
    public class TentTests
    {
        [Fact]
        public void A_tent_is_built_onto_the_workbench_once()
        {
            var cart = new CartUpgrades();
            Assert.False(cart.CanAdd(Tent.Piece));
            cart.Add("piece_workbench");
            Assert.True(cart.CanAdd(Tent.Piece));
            cart.Add(Tent.Piece);
            Assert.False(cart.CanAdd(Tent.Piece));
        }

        // The rug lies under the tent, so it comes after it.
        [Theory]
        [InlineData("rug_deer")]
        [InlineData("jute_carpet")]
        public void A_rug_needs_the_tent_built_first(string rug)
        {
            var cart = new CartUpgrades();
            cart.Add("piece_workbench");
            Assert.False(cart.CanAdd(rug));

            cart.Add(Tent.Piece);

            Assert.True(cart.CanAdd(rug));
        }

        [Fact]
        public void Ground_that_rises_less_than_the_most_allowed_is_flat_enough()
        {
            Assert.True(Tent.FlatEnough(new[] { 10f, 10.2f, 10.4f, 9.9f }, maxRise: 0.6f));
        }

        [Fact]
        public void Ground_that_rises_more_is_not()
        {
            Assert.False(Tent.FlatEnough(new[] { 10f, 10.2f, 11f, 9.9f }, maxRise: 0.6f));
        }

        [Fact]
        public void The_tent_stands_at_the_ground_s_average_height()
        {
            Assert.Equal(10.1f, Tent.GroundLevel(new[] { 10f, 10.2f, 10.4f, 9.8f }), 3);
        }

        [Theory]
        [InlineData(0f, 1f, 0f, true)]      // the middle
        [InlineData(2.3f, 0.5f, 1.9f, true)] // a corner, inside
        [InlineData(2.5f, 1f, 0f, false)]   // past a side
        [InlineData(0f, 1f, 2.2f, false)]   // past an end
        [InlineData(0f, 3.5f, 0f, false)]   // above the roof
        [InlineData(0f, -0.5f, 0f, false)]  // below the ground
        public void Under_the_tent_is_within_its_walls_and_below_its_roof(float x, float y, float z, bool under)
        {
            Assert.Equal(under, Tent.Under(x, y, z, halfWidth: 2.4f, halfDepth: 2f, height: 2.9f));
        }
    }
}
