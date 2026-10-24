using Cartheim.Core;
using Xunit;

namespace Cartheim.Core.Tests
{
    // Comfort the cart adds on top of what the game counts from the pieces around the player.
    public class ComfortTests
    {
        private const int HearthComfort = 2;

        [Fact]
        public void A_bare_cart_adds_no_comfort()
        {
            Assert.Equal(0, new CartUpgrades().ExtraComfort(HearthComfort, nearbyFireComfort: 0));
        }

        [Fact]
        public void Each_roof_level_adds_one_comfort()
        {
            var cart = new CartUpgrades();
            cart.Add("piece_workbench");
            cart.Add(CartUpgrades.ThatchRoof);
            cart.Add(CartUpgrades.ShingleRoof, extends: CartUpgrades.ThatchRoof);

            Assert.Equal(2, cart.ExtraComfort(HearthComfort, nearbyFireComfort: 0));
        }

        [Fact]
        public void A_hearth_with_no_other_fire_near_gives_its_comfort()
        {
            var cart = new CartUpgrades();
            cart.Add("piece_workbench");
            cart.Add("hearth");

            Assert.Equal(HearthComfort, cart.ExtraComfort(HearthComfort, nearbyFireComfort: 0));
        }

        // The game counts only the best fire nearby; the campfire's 1 is already counted, so the hearth adds the difference.
        [Fact]
        public void A_hearth_beside_a_campfire_adds_only_what_it_beats_it_by()
        {
            var cart = new CartUpgrades();
            cart.Add("piece_workbench");
            cart.Add("hearth");

            Assert.Equal(1, cart.ExtraComfort(HearthComfort, nearbyFireComfort: 1));
        }
    }
}
