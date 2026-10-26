using Cartheim.Core;
using Xunit;

namespace Cartheim.Core.Tests
{
    public class TarpTuneTests
    {
        private static readonly TarpBox Box = new TarpBox { MinX = -4f, MaxX = 2f, MinZ = -2f, MaxZ = 4f, Low = 2.5f, High = 3.5f, Turn = 0f };

        [Fact]
        public void A_ruins_tarp_starts_from_its_camp()
        {
            var camp = RuinCamp.For("StoneHouse4");
            var box = TarpTune.From(camp);
            Assert.Equal(camp.TarpMinX, box.MinX);
            Assert.Equal(camp.TarpMaxZ, box.MaxZ);
            Assert.Equal(camp.TarpHigh, box.High);
            Assert.Equal(camp.TarpTurn, box.Turn);
        }

        [Fact]
        public void Dragged_the_whole_tarp_moves_keeping_its_size_and_slope()
        {
            var moved = TarpTune.Moved(Box, dx: 1f, dz: -0.5f, dy: 0.25f);
            Assert.Equal((-3f, 3f, -2.5f, 3.5f, 2.75f, 3.75f), (moved.MinX, moved.MaxX, moved.MinZ, moved.MaxZ, moved.Low, moved.High));
        }

        [Fact]
        public void Grabbed_its_middle_goes_where_its_put()
        {
            var put = TarpTune.CentredAt(Box, 0f, 0f);
            Assert.Equal((-3f, 3f, -3f, 3f), (put.MinX, put.MaxX, put.MinZ, put.MaxZ));
            Assert.Equal(Box.Low, put.Low);
        }

        [Fact]
        public void Its_edges_never_cross_nor_its_low_side_rise_above_its_high()
        {
            var tidy = TarpTune.Tidy(new TarpBox { MinX = 2f, MaxX = 1f, MinZ = 0f, MaxZ = 0.2f, Low = 4f, High = 3f });
            Assert.True(tidy.MaxX - tidy.MinX >= TarpTune.Smallest);
            Assert.True(tidy.MaxZ - tidy.MinZ >= TarpTune.Smallest);
            Assert.True(tidy.Low <= tidy.High);
        }

        [Fact]
        public void Its_numbers_are_written_as_the_camp_takes_them_and_read_back()
        {
            var text = TarpTune.Format(new TarpBox { MinX = -4.7f, MaxX = 2.5f, MinZ = -2.3f, MaxZ = 4.4f, Low = 2.55f, High = 3.55f, Turn = 5f });
            Assert.Equal("TarpMinX = -4.7f, TarpMaxX = 2.5f, TarpMinZ = -2.3f, TarpMaxZ = 4.4f, TarpLow = 2.55f, TarpHigh = 3.55f, TarpTurn = 5f,", text);
            Assert.True(TarpTune.TryParse(text, out var back));
            Assert.Equal(-2.3f, back.MinZ);
            Assert.Equal(5f, back.Turn);
            Assert.False(TarpTune.TryParse("", out _));
        }
    }
}
