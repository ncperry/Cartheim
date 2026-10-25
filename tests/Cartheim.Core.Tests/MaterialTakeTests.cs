using Cartheim.Core;
using Xunit;

namespace Cartheim.Core.Tests
{
    public class MaterialTakeTests
    {
        [Fact]
        public void What_the_player_carries_is_used_first()
        {
            Assert.Equal(new[] { 10, 0, 0 }, MaterialTake.Split(need: 10, have: new[] { 25, 40, 5 }));
        }

        [Fact]
        public void The_rest_comes_from_the_cart_storage_in_order()
        {
            Assert.Equal(new[] { 3, 40, 2 }, MaterialTake.Split(need: 45, have: new[] { 3, 40, 5 }));
        }

        [Fact]
        public void Never_more_than_there_is()
        {
            Assert.Equal(new[] { 3, 4 }, MaterialTake.Split(need: 45, have: new[] { 3, 4 }));
        }
    }
}
