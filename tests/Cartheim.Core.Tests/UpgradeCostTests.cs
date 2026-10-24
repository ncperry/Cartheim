using System.Linq;
using Cartheim.Core;
using Xunit;

namespace Cartheim.Core.Tests
{
    public class UpgradeCostTests
    {
        [Fact]
        public void A_meadows_piece_costs_its_recipe_plus_five_bronze_nails()
        {
            var workbench = new[] { new ItemCount("Wood", 10) };

            Assert.Equal(new[] { new ItemCount("Wood", 10), new ItemCount("BronzeNails", 5) }, UpgradeCost.For(workbench));
        }

        [Fact]
        public void A_piece_made_with_iron_takes_iron_nails()
        {
            var toolShelf = new[] { new ItemCount("Iron", 4), new ItemCount("Wood", 10) };

            Assert.Equal(
                new[] { new ItemCount("Iron", 4), new ItemCount("Wood", 10), new ItemCount("IronNails", 5) },
                UpgradeCost.For(toolShelf));
        }

        [Fact]
        public void A_piece_made_with_chains_takes_iron_nails()
        {
            var forgeBellows = new[] { new ItemCount("Wood", 5), new ItemCount("Chain", 5), new ItemCount("DeerHide", 5) };

            Assert.Equal(new ItemCount("IronNails", 5), UpgradeCost.For(forgeBellows).Last());
        }
    }
}
