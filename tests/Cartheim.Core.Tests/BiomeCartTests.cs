using System.Linq;
using Cartheim.Core;
using Xunit;

namespace Cartheim.Core.Tests
{
    public class BiomeCartTests
    {
        [Fact]
        public void Each_material_belongs_to_the_biome_it_first_comes_from()
        {
            Assert.Equal(Biome.Meadows, BiomeCart.TierOf("Wood"));
            Assert.Equal(Biome.BlackForest, BiomeCart.TierOf("Bronze"));
            Assert.Equal(Biome.Swamp, BiomeCart.TierOf("Iron"));
            Assert.Equal(Biome.Mountains, BiomeCart.TierOf("Silver"));
            Assert.Equal(Biome.Mountains, BiomeCart.TierOf("DragonTear"));
            Assert.Equal(Biome.Plains, BiomeCart.TierOf("BlackMetal"));
            // Later lands, and anything not known, are past every cart.
            Assert.Equal(Biome.Later, BiomeCart.TierOf("Eitr"));
            Assert.Equal(Biome.Later, BiomeCart.TierOf("SomethingNew"));
        }

        [Fact]
        public void A_piece_belongs_to_the_latest_biome_any_of_its_materials_comes_from()
        {
            Assert.Equal(Biome.Swamp, BiomeCart.TierOf(new[] { "Wood", "Iron", "Stone" }));
            Assert.Equal(Biome.Meadows, BiomeCart.TierOf(new string[0]));
        }

        [Fact]
        public void A_biomes_cart_has_everything_buildable_by_the_end_of_that_biome_best_first()
        {
            var pieces = new[]
            {
                ("piece_workbench", new[] { "Wood" }),
                ("forge", new[] { "Stone", "Copper" }),
                ("piece_stonecutter", new[] { "Wood", "Iron" }),
                ("piece_artisanstation", new[] { "Wood", "DragonTear" }),
                ("blackforge", new[] { "BlackMarble", "YggdrasilWood" }),
                ("piece_chest_wood", new[] { "Wood" }),
                ("piece_chest", new[] { "FineWood", "Iron" }),
            };
            var swamp = BiomeCart.For(Biome.Swamp, pieces).ToList();
            Assert.Equal(new[] { "piece_stonecutter", "piece_chest", "forge", "piece_workbench", "piece_chest_wood" }, swamp);
            Assert.Contains("piece_artisanstation", BiomeCart.For(Biome.Mountains, pieces));
            Assert.DoesNotContain("blackforge", BiomeCart.For(Biome.Plains, pieces));
        }
    }
}
