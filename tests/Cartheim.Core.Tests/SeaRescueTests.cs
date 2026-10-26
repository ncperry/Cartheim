using Cartheim.Core;
using Xunit;

namespace Cartheim.Core.Tests
{
    public class SeaRescueTests
    {
        [Fact]
        public void By_default_the_cart_comes_ashore_to_the_player_except_on_hardcore()
        {
            Assert.True(SeaRescue.Comes(SeaDeath.ComesExceptHardcore, hardcore: false));
            Assert.False(SeaRescue.Comes(SeaDeath.ComesExceptHardcore, hardcore: true));
        }

        [Fact]
        public void It_can_be_set_to_always_come_or_never_come()
        {
            Assert.True(SeaRescue.Comes(SeaDeath.AlwaysComes, hardcore: true));
            Assert.False(SeaRescue.Comes(SeaDeath.StaysAtSea, hardcore: false));
        }

        [Theory]
        [InlineData("hardcore", true)]
        [InlineData("deathpenalty_hardcore:combat_hard", true)]
        [InlineData("combat_hard:deathpenalty_hardcore", true)]
        [InlineData("normal", false)]
        [InlineData("hard", false)]
        [InlineData("deathpenalty_hard:resources_less", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void A_world_is_hardcore_by_its_preset_or_its_death_penalty(string preset, bool hardcore)
        {
            Assert.Equal(hardcore, SeaRescue.Hardcore(preset));
        }

        [Fact]
        public void Only_carts_afloat_near_where_the_player_died_come()
        {
            Assert.True(SeaRescue.Near(SeaRescue.Reach));
            Assert.False(SeaRescue.Near(SeaRescue.Reach + 1f));
        }
    }
}
