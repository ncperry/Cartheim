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
    }
}
