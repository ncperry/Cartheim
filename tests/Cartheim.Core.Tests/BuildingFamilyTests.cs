using System.Linq;
using Cartheim.Core;
using Xunit;

namespace Cartheim.Core.Tests
{
    public class BuildingFamilyTests
    {
        [Fact]
        public void A_buildings_family_is_its_name_without_its_number()
        {
            Assert.Equal("WoodHouse", BuildingFamily.Of("WoodHouse12"));
            Assert.Equal("StoneTowerRuins", BuildingFamily.Of("StoneTowerRuins07_sunk"));
            Assert.Equal("Dolmen", BuildingFamily.Of("Dolmen01"));
            Assert.Equal("StartTemple", BuildingFamily.Of("StartTemple"));
        }

        [Fact]
        public void A_family_is_lined_up_in_number_order()
        {
            var names = new[] { "WoodHouse10", "WoodHouse2", "WoodVillage1", "WoodHouse1", "StoneHouse3" };
            Assert.Equal(new[] { "WoodHouse1", "WoodHouse2", "WoodHouse10" }, BuildingFamily.Members("WoodHouse2", names));
        }

        [Fact]
        public void Each_stands_clear_of_the_last_by_both_their_sizes_and_a_gap()
        {
            var along = BuildingFamily.Spacing(new[] { 5f, 10f, 3f }, start: 10f, gap: 4f);
            Assert.Equal(new[] { 15f, 34f, 51f }, along);
        }
    }
}
