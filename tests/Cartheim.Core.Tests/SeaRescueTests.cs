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

        [Fact]
        public void A_cart_with_someone_still_alive_near_it_stays_with_them()
        {
            Assert.True(SeaRescue.Adrift(othersAliveNear: 0));
            Assert.False(SeaRescue.Adrift(othersAliveNear: 1));
        }

        [Fact]
        public void It_washes_ashore_by_its_builder_or_by_whoever_died_when_it_has_none()
        {
            Assert.Equal(7L, SeaRescue.ComesTo(builder: 7L, died: 9L));
            Assert.Equal(9L, SeaRescue.ComesTo(builder: 0L, died: 9L));
        }

        [Fact]
        public void The_game_of_whoever_died_brings_it_or_the_server_once_they_have_left()
        {
            Assert.True(SeaRescue.Brings(died: true, diedOnline: true, server: false));
            Assert.False(SeaRescue.Brings(died: false, diedOnline: true, server: true));
            Assert.True(SeaRescue.Brings(died: false, diedOnline: false, server: true));
            Assert.False(SeaRescue.Brings(died: false, diedOnline: false, server: false));
        }

        [Fact]
        public void It_waits_while_another_player_is_with_it()
        {
            const long me = 1L, other = 2L;
            Assert.False(SeaRescue.WithSomeoneElse(owner: 0L, me));
            Assert.False(SeaRescue.WithSomeoneElse(owner: me, me));
            Assert.True(SeaRescue.WithSomeoneElse(owner: other, me));
        }
    }
}
