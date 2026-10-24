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
    }
}
