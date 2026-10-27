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

[Fact]        public void A_building_more_than_a_metre_and_a_half_off_the_ground_is_put_back_on_it()        {            Assert.True(DevWorld.OffTheGround(placedAt: 30f, ground: 35.45f));            Assert.True(DevWorld.OffTheGround(placedAt: 40f, ground: 35f));            Assert.False(DevWorld.OffTheGround(placedAt: 35f, ground: 35.9f));        }
        [Fact]
        public void An_item_stand_aimed_at_the_floor_stands_up_and_at_a_wall_hangs_on_it()
        {
            Assert.True(DevWorld.OnFloor(normalUp: 0.9f));
            Assert.False(DevWorld.OnFloor(normalUp: 0.1f));
        }
    }
}
