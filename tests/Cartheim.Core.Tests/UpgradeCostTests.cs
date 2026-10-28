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
        public void The_cart_workbench_needs_no_nails()
        {
            var workbench = new[] { new ItemCount("Wood", 10) };

            Assert.Equal(workbench, UpgradeCost.For("piece_workbench", workbench));
        }

        [Fact]
        public void The_cart_chopping_block_needs_no_nails()
        {
            var choppingBlock = new[] { new ItemCount("Wood", 10), new ItemCount("Flint", 10) };

            Assert.Equal(choppingBlock, UpgradeCost.For("piece_workbench_ext1", choppingBlock));
        }

        [Fact]
        public void The_cart_tanning_rack_needs_no_nails()
        {
            var tanningRack = new[]
            {
                new ItemCount("Wood", 10), new ItemCount("Flint", 15), new ItemCount("LeatherScraps", 20), new ItemCount("DeerHide", 5),
            };

            Assert.Equal(tanningRack, UpgradeCost.For("piece_workbench_ext2", tanningRack));
        }

        [Fact]
        public void The_cart_adze_takes_bronze_nails_as_its_recipe_needs_bronze()
        {
            var adze = new[] { new ItemCount("FineWood", 10), new ItemCount("Bronze", 3) };

            Assert.Equal(new ItemCount("BronzeNails", 5), UpgradeCost.For("piece_workbench_ext3", adze).Last());
        }

        [Fact]
        public void The_cart_tool_shelf_takes_iron_nails_as_its_recipe_needs_iron()
        {
            var toolShelf = new[] { new ItemCount("Iron", 4), new ItemCount("FineWood", 10), new ItemCount("Obsidian", 4) };

            Assert.Equal(new ItemCount("IronNails", 5), UpgradeCost.For("piece_workbench_ext4", toolShelf).Last());
        }

        [Fact]
        public void A_meadows_piece_beyond_the_workbench_still_takes_bronze_nails()
        {
            var bed = new[] { new ItemCount("Wood", 8) };

            Assert.Equal(new ItemCount("BronzeNails", 5), UpgradeCost.For("bed", bed).Last());
        }

        [Fact]
        public void A_chopping_block_takes_bronze_nails()
        {
            var choppingBlock = new[] { new ItemCount("Wood", 10), new ItemCount("Flint", 10) };

            Assert.Equal(new ItemCount("BronzeNails", 5), UpgradeCost.For(choppingBlock).Last());
        }

        [Fact]
        public void A_tanning_rack_takes_bronze_nails()
        {
            var tanningRack = new[]
            {
                new ItemCount("Wood", 10), new ItemCount("Flint", 15), new ItemCount("LeatherScraps", 20), new ItemCount("DeerHide", 5),
            };

            Assert.Equal(new ItemCount("BronzeNails", 5), UpgradeCost.For(tanningRack).Last());
        }

        [Fact]
        public void An_adze_takes_bronze_nails()
        {
            var adze = new[] { new ItemCount("FineWood", 10), new ItemCount("Bronze", 3) };

            Assert.Equal(new ItemCount("BronzeNails", 5), UpgradeCost.For(adze).Last());
        }

        [Fact]
        public void A_forge_takes_bronze_nails()
        {
            var forge = new[] { new ItemCount("Stone", 4), new ItemCount("Coal", 4), new ItemCount("Wood", 10), new ItemCount("Copper", 6) };

            Assert.Equal(new ItemCount("BronzeNails", 5), UpgradeCost.For(forge).Last());
        }

        [Fact]
        public void A_cauldron_takes_bronze_nails()
        {
            var cauldron = new[] { new ItemCount("Tin", 10) };

            Assert.Equal(new ItemCount("BronzeNails", 5), UpgradeCost.For(cauldron).Last());
        }

        [Fact]
        public void A_fermenter_takes_bronze_nails()
        {
            var fermenter = new[] { new ItemCount("FineWood", 30), new ItemCount("Bronze", 5), new ItemCount("Resin", 10) };

            Assert.Equal(new ItemCount("BronzeNails", 5), UpgradeCost.For(fermenter).Last());
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

        [Fact]
        public void A_piece_made_with_a_material_from_a_later_biome_takes_iron_nails()
        {
            var butchersTable = new[]
            {
                new ItemCount("ElderBark", 2), new ItemCount("RoundLog", 4), new ItemCount("FineWood", 4), new ItemCount("Silver", 2),
            };

            Assert.Equal(new ItemCount("IronNails", 5), UpgradeCost.For(butchersTable).Last());
        }

        [Fact]
        public void Fey_lights_are_held_on_with_bronze_nails_as_thistle_grows_in_the_black_forest()
        {
            var cost = UpgradeCost.For(new[] { new ItemCount("FineWood", 2), new ItemCount("Thistle", 1) });

            Assert.Contains(new ItemCount("BronzeNails", 5), cost);
        }

        [Theory]
        [InlineData("Carrot")]
        [InlineData("Dandelion")]
        [InlineData("Feathers")]
        [InlineData("Mushroom")]
        [InlineData("RoundLog")]
        [InlineData("TrophySkeleton")]
        public void Meadows_and_Black_Forest_finds_are_held_on_with_bronze_nails(string material)
        {
            var cost = UpgradeCost.For(new[] { new ItemCount("Wood", 2), new ItemCount(material, 1) });

            Assert.Contains(new ItemCount("BronzeNails", 5), cost);
        }

        // The steam engine's own recipe already has nails in it: the five to fix it on are added to them.
        [Fact]
        public void Nails_already_in_a_recipe_are_added_to_not_listed_twice()
        {
            var engine = new[] { new ItemCount("Iron", 10), new ItemCount("IronNails", 5) };

            Assert.Equal(new[] { new ItemCount("Iron", 10), new ItemCount("IronNails", 10) }, UpgradeCost.For(engine));
        }
    }
}
