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
            ["bed"] = new[] { new ItemCount("Wood", 8) },
        };

        [Fact]
        public void A_bare_cart_refunds_nothing()
        {
            Assert.Empty(new CartUpgrades().Refund(piece => Recipes[piece]));
        }

        [Fact]
        public void A_cart_workbench_refunds_its_wood_and_no_nails_as_it_took_none()
        {
            var cart = new CartUpgrades();
            cart.Add("piece_workbench");

            Assert.Equal(new[] { new ItemCount("Wood", 10) }, cart.Refund(piece => Recipes[piece]));
        }

        [Fact]
        public void Several_upgrades_refund_each_material_as_one_total()
        {
            var cart = new CartUpgrades();
            cart.Add("piece_workbench");
            cart.Add("piece_workbench_ext1", extends: "piece_workbench");
            cart.Add("bed");

            Assert.Equal(
                new[] { new ItemCount("Wood", 28), new ItemCount("Flint", 10), new ItemCount("BronzeNails", 5) },
                cart.Refund(piece => Recipes[piece]));
        }

        [Fact]
        public void Freyrs_blessing_refunds_just_its_own_materials_as_it_took_no_nails()
        {
            var cart = new CartUpgrades();
            cart.Add(FreyrsBlessing.Piece);

            Assert.DoesNotContain(cart.Refund(piece => new[] { new ItemCount("Honey", 20), new ItemCount("BlackMetal", 5) }),
                c => c.Prefab.EndsWith("Nails"));
        }
    }
}
