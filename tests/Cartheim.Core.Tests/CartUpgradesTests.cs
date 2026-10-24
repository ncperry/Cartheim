using Cartheim.Core;
using Xunit;

namespace Cartheim.Core.Tests
{
    public class CartUpgradesTests
    {
        [Fact]
        public void A_bare_cart_can_be_made_a_workbench()
        {
            Assert.True(new CartUpgrades().CanAdd("piece_workbench"));
        }

        [Fact]
        public void A_bare_cart_cannot_take_a_forge()
        {
            Assert.False(new CartUpgrades().CanAdd("forge"));
        }

        [Fact]
        public void A_cart_workbench_can_take_a_forge()
        {
            var cart = new CartUpgrades();
            cart.Add("piece_workbench");

            Assert.True(cart.CanAdd("forge"));
        }

        [Fact]
        public void A_piece_cannot_be_built_in_twice()
        {
            var cart = new CartUpgrades();
            cart.Add("piece_workbench");

            Assert.False(cart.CanAdd("piece_workbench"));
        }

        [Fact]
        public void An_extension_needs_its_station_built_in_first()
        {
            var cart = new CartUpgrades();
            cart.Add("piece_workbench");

            Assert.False(cart.CanAdd("forge_ext1", extends: "forge"));
        }

        [Fact]
        public void An_extension_can_go_onto_its_station()
        {
            var cart = new CartUpgrades();
            cart.Add("piece_workbench");
            cart.Add("forge");

            Assert.True(cart.CanAdd("forge_ext1", extends: "forge"));
        }

        [Fact]
        public void A_bare_cart_saves_as_nothing()
        {
            Assert.Equal("", new CartUpgrades().Save());
        }

        [Fact]
        public void A_saved_cart_loads_back_with_the_same_pieces()
        {
            var cart = new CartUpgrades();
            cart.Add("piece_workbench");

            Assert.False(CartUpgrades.Load(cart.Save()).CanAdd("piece_workbench"));
        }
    }
}
