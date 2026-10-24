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
    }
}
