using Cartheim.Core;
using Xunit;

namespace Cartheim.Core.Tests
{
    public class BeehiveTests
    {
        private static CartUpgrades Workbench()
        {
            var cart = new CartUpgrades();
            cart.Add("piece_workbench");
            return cart;
        }

        [Fact]
        public void A_cart_with_one_beehive_can_take_a_second()
        {
            var cart = Workbench();
            cart.Add("piece_beehive");

            Assert.True(cart.CanAdd("piece_beehive"));
        }

        [Fact]
        public void A_cart_holds_at_most_two_beehives()
        {
            var cart = Workbench();
            cart.Add("piece_beehive");
            cart.Add("piece_beehive");

            Assert.False(cart.CanAdd("piece_beehive"));
        }

        [Fact]
        public void With_one_beehive_built_the_second_is_still_to_build()
        {
            var cart = Workbench();
            cart.Add("piece_beehive");
            var cost = new[] { new ItemCount("Wood", 10) };
            var carried = new System.Collections.Generic.Dictionary<string, int> { ["Wood"] = 10 };

            Assert.Equal(UpgradeState.Ready, cart.StateOf("piece_beehive", null, cost, carried));
        }
    }
}
