using Cartheim.Core;
using Xunit;

namespace Cartheim.Core.Tests
{
    public class CartStatsTests
    {
        [Fact]
        public void A_bare_cart_keeps_its_own_health()
        {
            Assert.Equal(400f, new CartUpgrades().Health(400f));
        }

        [Fact]
        public void A_cart_workbench_adds_a_hundred_health()
        {
            var cart = new CartUpgrades();
            cart.Add("piece_workbench");

            Assert.Equal(500f, cart.Health(400f));
        }

        [Fact]
        public void Each_workbench_upgrade_adds_another_hundred_health()
        {
            var cart = new CartUpgrades();
            cart.Add("piece_workbench");
            cart.Add("piece_workbench_ext1", extends: "piece_workbench");

            Assert.Equal(600f, cart.Health(400f));
        }
    }
}
