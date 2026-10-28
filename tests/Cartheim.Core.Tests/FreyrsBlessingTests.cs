using System.Linq;
using Cartheim.Core;
using Xunit;

namespace Cartheim.Core.Tests
{
    public class FreyrsBlessingTests
    {
        [Fact]
        public void It_needs_a_beehive_and_a_fermenter_on_the_cart()
        {
            var cart = new CartUpgrades();
            cart.Add("piece_workbench");
            Assert.False(cart.CanAdd(FreyrsBlessing.Piece));
            cart.Add(CartUpgrades.Beehive);
            Assert.False(cart.CanAdd(FreyrsBlessing.Piece));
            cart.Add(FreyrsBlessing.Fermenter);
            Assert.True(cart.CanAdd(FreyrsBlessing.Piece));
            cart.Add(FreyrsBlessing.Piece);
            Assert.False(cart.CanAdd(FreyrsBlessing.Piece));
        }

        [Fact]
        public void It_takes_no_nails_it_is_no_piece_nailed_on()
        {
            var recipe = new[] { new ItemCount("Honey", 20), new ItemCount("Barley", 10) };
            Assert.Equal(recipe, UpgradeCost.For(FreyrsBlessing.Piece, recipe));
            Assert.Contains(UpgradeCost.For("bed", new[] { new ItemCount("Wood", 8) }), c => c.Prefab == "BronzeNails");
        }

        [Fact]
        public void Crops_near_it_take_two_thirds_of_their_time()
        {
            const double grownIn = 3000;
            var blessedFor = grownIn * 2 / 3;
            Assert.Equal(grownIn, blessedFor + FreyrsBlessing.Bonus(blessedFor), 6);
        }

        [Fact]
        public void It_blesses_crops_within_twenty_metres()
        {
            Assert.True(FreyrsBlessing.Near(20f));
            Assert.False(FreyrsBlessing.Near(20.5f));
        }
    }
}
