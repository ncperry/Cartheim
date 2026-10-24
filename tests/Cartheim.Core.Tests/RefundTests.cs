using System.Collections.Generic;
using Cartheim.Core;
using Xunit;

namespace Cartheim.Core.Tests
{
    public class RefundTests
    {
        private static readonly Dictionary<string, ItemCount[]> Recipes = new Dictionary<string, ItemCount[]>
        {
            ["piece_workbench"] = new[] { new ItemCount("Wood", 10) },
            ["piece_workbench_ext1"] = new[] { new ItemCount("Wood", 10), new ItemCount("Flint", 10) },
        };

        [Fact]
        public void A_bare_cart_refunds_nothing()
        {
            Assert.Empty(new CartUpgrades().Refund(piece => Recipes[piece]));
        }

        [Fact]
        public void A_cart_workbench_refunds_its_wood_and_nails()
        {
            var cart = new CartUpgrades();
            cart.Add("piece_workbench");

            Assert.Equal(new[] { new ItemCount("Wood", 10), new ItemCount("BronzeNails", 5) }, cart.Refund(piece => Recipes[piece]));
        }

        [Fact]
        public void Several_upgrades_refund_each_material_as_one_total()
        {
            var cart = new CartUpgrades();
            cart.Add("piece_workbench");
            cart.Add("piece_workbench_ext1", extends: "piece_workbench");

            Assert.Equal(
                new[] { new ItemCount("Wood", 20), new ItemCount("BronzeNails", 10), new ItemCount("Flint", 10) },
                cart.Refund(piece => Recipes[piece]));
        }
    }
}
