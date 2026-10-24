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
        public void A_bare_cart_has_no_roof()
        {
            Assert.Equal(0, new CartUpgrades().RoofLevel);
        }

        [Fact]
        public void A_shingle_roof_over_the_thatch_makes_roof_level_two()
        {
            var cart = new CartUpgrades();
            cart.Add("piece_workbench");
            cart.Add(CartUpgrades.ThatchRoof);
            cart.Add(CartUpgrades.ShingleRoof, extends: CartUpgrades.ThatchRoof);

            Assert.Equal(2, cart.RoofLevel);
        }

        [Fact]
        public void A_grausten_roof_over_the_shingle_makes_roof_level_three()
        {
            var cart = new CartUpgrades();
            cart.Add("piece_workbench");
            cart.Add(CartUpgrades.ThatchRoof);
            cart.Add(CartUpgrades.ShingleRoof, extends: CartUpgrades.ThatchRoof);
            cart.Add(CartUpgrades.GraustenRoof, extends: CartUpgrades.ShingleRoof);

            Assert.Equal(3, cart.RoofLevel);
        }

        [Fact]
        public void A_bare_cart_looks_empty()
        {
            Assert.Equal(0f, new CartUpgrades().Fullness);
        }

        [Fact]
        public void Each_upgrade_makes_the_cart_look_a_tenth_fuller()
        {
            var cart = new CartUpgrades();
            cart.Add("piece_workbench");
            cart.Add("piece_workbench_ext1", extends: "piece_workbench");

            Assert.Equal(0.2f, cart.Fullness, 3);
        }

        [Fact]
        public void A_cart_never_looks_more_than_full()
        {
            var cart = new CartUpgrades();
            for (var i = 0; i < 12; i++)
                cart.Add("piece" + i);

            Assert.Equal(1f, cart.Fullness);
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
