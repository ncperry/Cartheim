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

        [Fact]
        public void Workbench_upgrades_still_count_after_the_cart_is_saved_and_loaded()
        {
            var cart = new CartUpgrades();
            cart.Add("piece_workbench");
            cart.Add("piece_workbench_ext1", extends: "piece_workbench");

            Assert.Equal(600f, CartUpgrades.Load(cart.Save()).Health(400f));
        }

        [Fact]
        public void A_bare_cart_has_no_armor()
        {
            Assert.Equal(0f, new CartUpgrades().Armor);
        }

        [Fact]
        public void Each_forge_level_adds_five_armor()
        {
            var cart = new CartUpgrades();
            cart.Add("piece_workbench");
            cart.Add("forge");
            cart.Add("forge_ext1", extends: "forge");

            Assert.Equal(10f, cart.Armor);
        }
    }
}
