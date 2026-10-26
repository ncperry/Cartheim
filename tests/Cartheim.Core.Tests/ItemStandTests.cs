using Cartheim.Core;
using Xunit;

namespace Cartheim.Core.Tests
{
    public class ItemStandTests
    {
        // It hangs on the front cross of the roof.
        [Fact]
        public void An_item_stand_needs_a_roof()
        {
            var cart = new CartUpgrades();
            cart.Add("piece_workbench");
            Assert.False(cart.CanAdd(CartUpgrades.ItemStand));

            cart.Add(CartUpgrades.ThatchRoof);

            Assert.True(cart.CanAdd(CartUpgrades.ItemStand));
        }

        [Fact]
        public void A_cart_has_one_item_stand()
        {
            var cart = new CartUpgrades();
            cart.Add("piece_workbench");
            cart.Add(CartUpgrades.ThatchRoof);
            cart.Add(CartUpgrades.ItemStand);

            Assert.False(cart.CanAdd(CartUpgrades.ItemStand));
        }
    }
}
