using Cartheim.Core;
using Xunit;

namespace Cartheim.Core.Tests
{
    public class CartMassTests
    {
        [Fact]
        public void A_cart_pulls_as_if_it_carried_a_middling_load_whatever_it_holds()
        {
            Assert.Equal(20f + 600f * 0.5f, CartMass.Pulled(baseMass: 20f, itemWeightMassFactor: 0.5f, feltLoad: 600f));
        }

        [Fact]
        public void With_no_felt_load_it_pulls_like_an_empty_cart()
        {
            Assert.Equal(20f, CartMass.Pulled(baseMass: 20f, itemWeightMassFactor: 0.5f, feltLoad: 0f));
        }

        [Fact]
        public void A_felt_load_below_nothing_counts_as_nothing()
        {
            Assert.Equal(20f, CartMass.Pulled(baseMass: 20f, itemWeightMassFactor: 0.5f, feltLoad: -100f));
        }
    }
}
