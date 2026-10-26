using System.Collections.Generic;
using System.Linq;
using Cartheim.Core;
using Xunit;

namespace Cartheim.Core.Tests
{
    public class CratesTests
    {
        [Theory]
        [InlineData("Wood", "Building")]
        [InlineData("Stone", "Building")]
        [InlineData("Ore", "Metals")]
        [InlineData("Metal", "Metals")]
        [InlineData("Food", "Food")]
        [InlineData("Fish", "Food")]
        [InlineData("Ingredients", "Ingredients")]
        [InlineData("Seeds", "Ingredients")]
        [InlineData("Textiles", "Hides & drops")]
        [InlineData("MonsterDrops", "Hides & drops")]
        [InlineData("Trophies", "Trophies")]
        [InlineData("Meads", "Meads")]
        [InlineData("Gear", "Gear")]
        [InlineData("Ammo", "Gear")]
        [InlineData("Valuables", "Valuables")]
        [InlineData("Misc", "Misc")]
        public void Each_type_of_item_has_its_crate(string category, string crate)
        {
            Assert.Equal(crate, Crates.For(category).Name);
        }

        [Fact]
        public void A_type_no_crate_names_goes_in_the_misc_crate()
        {
            Assert.Equal(Crates.Misc, Crates.For("SomethingNew"));
        }

        [Fact]
        public void There_are_ten_crates_and_the_cart_s_own_storage_is_the_misc_one_shown_last()
        {
            Assert.Equal(10, Crates.All.Count);
            Assert.Equal(Crates.Misc, Crates.All.Last());
        }

        [Fact]
        public void Every_crate_has_its_own_slot_in_the_cart_but_misc_which_is_the_cart_s_storage()
        {
            var slots = Crates.All.Where(c => c != Crates.Misc).Select(c => c.Slot).ToList();
            Assert.Equal(9, slots.Distinct().Count());
            Assert.All(slots, s => Assert.True(Crates.IsSlot(s)));
            Assert.Null(Crates.Misc.Slot);
            Assert.False(Crates.IsSlot("piece_chest_wood"));
        }

        [Fact]
        public void A_crate_takes_only_its_own_types()
        {
            var food = Crates.For("Food");
            Assert.True(food.Takes("Fish"));
            Assert.False(food.Takes("Wood"));
            Assert.True(Crates.Misc.Takes("SomethingNew"));
            Assert.False(Crates.Misc.Takes("Food"));
        }

        [Fact]
        public void A_crate_is_named_for_messages()
        {
            Assert.Equal("the food crate in the cart", Crates.For("Food").InTheCart);
            Assert.Equal("the hides & drops crate in the cart", Crates.For("Textiles").InTheCart);
        }

        [Fact]
        public void Coins_stand_for_valuables_and_ectoplasm_for_the_rest()
        {
            Assert.Equal("Coins", Crates.For("Valuables").Icon);
            Assert.Equal("Ectoplasm", Crates.Misc.Icon);
        }

        [Fact]
        public void Carrot_soup_stands_for_food()
        {
            Assert.Equal("CarrotSoup", Crates.For("Food").Icon);
        }

        [Fact]
        public void Items_in_the_wrong_crate_are_listed_with_where_they_belong()
        {
            var misplaced = Crates.Misplaced(Crates.Misc, new[] { "Blueberries", "Ectoplasm", "Wood" },
                prefab => prefab == "Blueberries" ? "Food" : prefab == "Wood" ? "Wood" : "Misc");
            Assert.Equal(new[] { (0, "Food"), (2, "Building") }, misplaced.Select(m => (m.index, m.crate.Name)));
        }

        [Fact]
        public void Totals_that_match_say_so()
        {
            var before = new[] { new ItemCount("Wood", 50), new ItemCount("Wood", 20), new ItemCount("Coins", 3) };
            var after = new[] { new ItemCount("Coins", 3), new ItemCount("Wood", 70) };
            Assert.Empty(Crates.Differences(before, after));
        }

        [Fact]
        public void Totals_that_differ_name_each_difference()
        {
            var before = new[] { new ItemCount("Wood", 50), new ItemCount("Coins", 3) };
            var after = new[] { new ItemCount("Wood", 40), new ItemCount("Resin", 1) };
            Assert.Equal(new[] { "Coins: 3 before, 0 after", "Resin: 0 before, 1 after", "Wood: 50 before, 40 after" },
                Crates.Differences(before, after));
        }
    }
}
