using Cartheim.Core;
using Xunit;

namespace Cartheim.Core.Tests
{
    public class PortalTests
    {
        [Fact]
        public void A_portal_is_built_onto_the_workbench_once()
        {
            var cart = new CartUpgrades();
            Assert.False(cart.CanAdd(Portal.Piece));
            cart.Add("piece_workbench");
            Assert.True(cart.CanAdd(Portal.Piece));
            cart.Add(Portal.Piece);
            Assert.False(cart.CanAdd(Portal.Piece));
        }

        [Fact]
        public void A_cart_is_named_for_who_built_its_portal()
        {
            Assert.Equal("Carmen's Cart", Portal.CartName("Carmen"));
        }

        [Fact]
        public void Carts_with_the_same_name_are_told_apart_by_number()
        {
            Assert.Equal(new[] { "Carmen's Cart", "Bjorn's Cart", "Carmen's Cart (2)" },
                Portal.Labels(new[] { "Carmen's Cart", "Bjorn's Cart", "Carmen's Cart" }));
        }
    }
}
