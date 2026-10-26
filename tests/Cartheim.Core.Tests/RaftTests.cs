using Cartheim.Core;
using Xunit;

namespace Cartheim.Core.Tests
{
    public class RaftTests
    {
        [Fact]
        public void A_raft_is_built_onto_the_workbench_once()
        {
            var cart = new CartUpgrades();
            Assert.False(cart.CanAdd(Raft.Piece));
            cart.Add("piece_workbench");
            Assert.True(cart.CanAdd(Raft.Piece));
            cart.Add(Raft.Piece);
            Assert.False(cart.CanAdd(Raft.Piece));
        }

        [Theory]
        [InlineData(28f, 30f, true)]    // two metres deep
        [InlineData(29.5f, 30f, false)] // knee deep
        [InlineData(31f, 30f, false)]   // dry land
        public void A_raft_floats_only_where_the_water_is_deep_enough(float ground, float water, bool floats)
        {
            Assert.Equal(floats, Raft.Floats(ground, water));
        }

        [Theory]
        [InlineData(31f, 30f, true)]    // dry land
        [InlineData(29.9f, 30f, true)]  // the water's edge
        [InlineData(29f, 30f, false)]   // a metre under
        public void The_cart_is_put_ashore_only_on_ground_at_or_above_the_water(float ground, float water, bool ashore)
        {
            Assert.Equal(ashore, Raft.Ashore(ground, water));
        }

        [Theory]
        [InlineData(8f, true)]
        [InlineData(20f, false)]
        public void The_raft_and_the_cart_go_only_so_far_from_each_other(float distance, bool near)
        {
            Assert.Equal(near, Raft.Near(distance));
        }
    }
}
