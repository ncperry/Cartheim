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
    }
}
