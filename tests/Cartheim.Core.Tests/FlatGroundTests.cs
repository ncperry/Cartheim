using System.Linq;
using Cartheim.Core;
using Xunit;

namespace Cartheim.Core.Tests
{
    public class FlatGroundTests
    {
        // 12 m along the building's x, 8 m along its z, centred 2 m out along x.
        private static readonly FlatGround.Area Area = new FlatGround.Area(2f, 0f, 12f, 8f);

        [Fact]
        public void Inside_the_area_the_ground_is_at_the_buildings_base()
        {
            Assert.Equal(10f, FlatGround.Height(Area, 2f, 0f, natural: 13f, floor: 10f));
            Assert.Equal(10f, FlatGround.Height(Area, 8f, 4f, natural: 7f, floor: 10f));
        }

        [Fact]
        public void Round_it_the_ground_blends_back_to_its_own_over_the_margin()
        {
            // 1 m out from the area's edge along x, of a 2 m margin: halfway.
            Assert.Equal(11.5f, FlatGround.Height(Area, 9f, 0f, natural: 13f, floor: 10f).Value, 3);
            Assert.Equal(13f, FlatGround.Height(Area, 10f, 0f, natural: 13f, floor: 10f).Value, 3);
            Assert.Null(FlatGround.Height(Area, 10.5f, 0f, natural: 13f, floor: 10f));
        }

        [Fact]
        public void A_camp_record_says_what_ground_it_flattens()
        {
            var camp = CampRecords.Parse("WoodHouse4",
                "flatten | ground | -4.00 | 0.00 | -1.00 | 0 | Flatten | width 14.00; depth 10.00\n" +
                "player | fire_pit | 0.04 | 0.00 | -3.03 | 0 | Fireplace | fuel 0.7\n");
            var area = camp.Flatten.Single();
            Assert.Equal((-4f, -1f, 14f, 10f), (area.X, area.Z, area.Width, area.Depth));
            Assert.Single(camp.Recorded);
        }

        [Fact]
        public void The_line_written_for_an_area_reads_back_as_it()
        {
            var camp = CampRecords.Parse("WoodHouse4", FlatGround.Line(new FlatGround.Area(-4f, -1f, 14f, 10f)));
            Assert.Equal((-4f, -1f, 14f, 10f), (camp.Flatten[0].X, camp.Flatten[0].Z, camp.Flatten[0].Width, camp.Flatten[0].Depth));
        }
    }
}
