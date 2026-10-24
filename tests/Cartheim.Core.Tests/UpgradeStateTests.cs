using System.Collections.Generic;
using Cartheim.Core;
using Xunit;

namespace Cartheim.Core.Tests
{
    public class UpgradeStateTests
    {
        private static readonly ItemCount[] WorkbenchCost = { new ItemCount("Wood", 10), new ItemCount("BronzeNails", 5) };

        [Fact]
        public void A_workbench_you_can_afford_is_ready_to_build()
        {
            var carried = new Dictionary<string, int> { ["Wood"] = 10, ["BronzeNails"] = 5 };

            Assert.Equal(UpgradeState.Ready, new CartUpgrades().StateOf("piece_workbench", null, WorkbenchCost, carried));
        }

        [Fact]
        public void A_workbench_short_of_nails_cannot_be_afforded()
        {
            var carried = new Dictionary<string, int> { ["Wood"] = 10, ["BronzeNails"] = 4 };

            Assert.Equal(UpgradeState.CannotAfford, new CartUpgrades().StateOf("piece_workbench", null, WorkbenchCost, carried));
        }
    }
}
