using Cartheim.Core;
using Xunit;

namespace Cartheim.Core.Tests
{
    public class DevWorldTests
    {
        [Fact]
        public void The_dev_world_is_known_by_its_name_whatever_its_capitals_and_spacing()
        {
            Assert.True(DevWorld.Is("CartHeim Dev"));
            Assert.True(DevWorld.Is(" cartheim  dev "));
            Assert.False(DevWorld.Is("Cartheim"));
            Assert.False(DevWorld.Is(null));
        }

        [Fact]
        public void An_item_stand_aimed_at_the_floor_stands_up_and_at_a_wall_hangs_on_it()
        {
            Assert.True(DevWorld.OnFloor(normalUp: 0.9f));
            Assert.False(DevWorld.OnFloor(normalUp: 0.1f));
        }
    }
}
