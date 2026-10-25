using Cartheim.Core;
using Xunit;

namespace Cartheim.Core.Tests
{
    public class SteamEngineTests
    {
        private const string Stonecutter = "piece_stonecutter";
        private const string Forge = "forge";

        private static CartUpgrades CartWith(params string[] pieces)
        {
            var cart = new CartUpgrades();
            cart.Add("piece_workbench");
            foreach (var piece in pieces)
                cart.Add(piece);
            return cart;
        }

        [Fact]
        public void An_engine_needs_the_stonecutter_and_the_forge_built_first()
        {
            Assert.False(CartWith().CanAdd(SteamEngine.Engine));
            Assert.False(CartWith(Stonecutter).CanAdd(SteamEngine.Engine));
            Assert.False(CartWith(Forge).CanAdd(SteamEngine.Engine));
            Assert.True(CartWith(Stonecutter, Forge).CanAdd(SteamEngine.Engine));
        }

        [Fact]
        public void Its_two_upgrades_go_onto_the_engine_once_each()
        {
            var cart = CartWith(Stonecutter, Forge);
            Assert.False(cart.CanAdd(SteamEngine.Boiler, extends: SteamEngine.Engine));

            cart.Add(SteamEngine.Engine);
            cart.Add(SteamEngine.Boiler, SteamEngine.Engine);

            Assert.False(cart.CanAdd(SteamEngine.Boiler, extends: SteamEngine.Engine));
            Assert.True(cart.CanAdd(SteamEngine.Pistons, extends: SteamEngine.Engine));
            cart.Add(SteamEngine.Pistons, SteamEngine.Engine);
            Assert.Equal(3, cart.Level(SteamEngine.Engine));
        }

        [Fact]
        public void It_burns_a_coal_in_the_time_a_hearth_burns_a_log()
        {
            Assert.Equal(9f, SteamEngine.Burn(coal: 10f, seconds: 50.0, secondsPerCoal: 50f), 3);
            Assert.Equal(9.5f, SteamEngine.Burn(coal: 10f, seconds: 25.0, secondsPerCoal: 50f), 3);
        }

        [Fact]
        public void It_burns_down_to_nothing_and_no_further()
        {
            Assert.Equal(0f, SteamEngine.Burn(coal: 1f, seconds: 500.0, secondsPerCoal: 50f));
        }

        [Fact]
        public void It_holds_up_to_twenty_coal()
        {
            Assert.True(SteamEngine.HasRoomFor(coal: 19f));
            Assert.False(SteamEngine.HasRoomFor(coal: 20f));
            // Like a hearth, a coal half burnt still takes up its place.
            Assert.False(SteamEngine.HasRoomFor(coal: 19.4f));
        }

        [Fact]
        public void Without_coal_it_does_not_help_pull()
        {
            Assert.Equal(0f, SteamEngine.Assist(engineLevel: 3, coal: 0f));
        }

        [Fact]
        public void Loaded_it_takes_half_the_pull_and_more_with_each_upgrade_until_all_of_it()
        {
            Assert.Equal(0f, SteamEngine.Assist(engineLevel: 0, coal: 5f));
            Assert.Equal(0.5f, SteamEngine.Assist(engineLevel: 1, coal: 5f));
            Assert.Equal(0.75f, SteamEngine.Assist(engineLevel: 2, coal: 5f));
            Assert.Equal(1f, SteamEngine.Assist(engineLevel: 3, coal: 5f));
        }

        [Fact]
        public void A_burning_engine_warms_like_a_hearth()
        {
            var cart = CartWith(Stonecutter, Forge, SteamEngine.Engine);

            Assert.Equal(2, cart.ExtraComfort(hearthComfort: 2, nearbyFireComfort: 0, engineBurning: true));
            Assert.Equal(0, cart.ExtraComfort(hearthComfort: 2, nearbyFireComfort: 0, engineBurning: false));
        }
    }
}
