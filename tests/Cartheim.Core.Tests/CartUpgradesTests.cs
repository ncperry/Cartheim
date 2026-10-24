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
        public void A_cart_takes_only_one_chest()
        {
            var cart = new CartUpgrades();
            cart.Add("piece_workbench");
            cart.Add("piece_chest_wood");

            Assert.False(cart.CanAdd("piece_chest"));
        }

        [Theory]
        [InlineData("piece_chest_blackmetal")]
        [InlineData("piece_chest_private")]
        [InlineData("piece_chest_grausten")]
        public void Every_kind_of_chest_counts_as_the_one_chest(string chest)
        {
            var cart = new CartUpgrades();
            cart.Add("piece_workbench");
            cart.Add(chest);

            Assert.False(cart.CanAdd("piece_chest_wood"));
        }

        [Theory]
        [InlineData("piece_chest_barrel")]
        [InlineData("piece_chest_warderobe")]
        public void A_barrel_or_wardrobe_goes_in_alongside_the_chest(string container)
        {
            var cart = new CartUpgrades();
            cart.Add("piece_workbench");
            cart.Add("piece_chest_wood");

            Assert.True(cart.CanAdd(container));
        }

        [Fact]
        public void A_cart_takes_only_one_light()
        {
            var cart = new CartUpgrades();
            cart.Add("piece_workbench");
            cart.Add("piece_FairylightGarland");

            Assert.False(cart.CanAdd("piece_walltorch"));
        }

        [Theory]
        [InlineData("piece_dvergr_lantern")]
        [InlineData("piece_dvergr_lantern_pole")]
        [InlineData("piece_Lavalantern")]
        [InlineData("piece_hoodedlantern")]
        [InlineData("piece_jackoturnip")]
        [InlineData("piece_snowlantern")]
        public void Every_lantern_counts_as_the_one_light(string lantern)
        {
            var cart = new CartUpgrades();
            cart.Add("piece_workbench");
            cart.Add(lantern);

            Assert.False(cart.CanAdd("piece_FairylightGarland"));
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
