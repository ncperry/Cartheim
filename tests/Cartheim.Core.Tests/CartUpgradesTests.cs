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
    }
}
