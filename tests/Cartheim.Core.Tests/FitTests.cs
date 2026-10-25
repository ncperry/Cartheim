using Cartheim.Core;
using Xunit;

namespace Cartheim.Core.Tests
{
    public class FitTests
    {
        [Fact]
        public void A_piece_wider_than_its_spot_shrinks_to_fit()
        {
            var fit = Fit.Into(spotWidth: 0.5f, spotLength: 1f, maxHeight: 2f, width: 2f, length: 1f, height: 1f);

            Assert.Equal(0.25f, fit.Scale, 3);
        }

        [Fact]
        public void A_tall_piece_shrinks_to_the_height_allowed()
        {
            var fit = Fit.Into(spotWidth: 1f, spotLength: 1f, maxHeight: 0.5f, width: 1f, length: 1f, height: 2f);

            Assert.Equal(0.25f, fit.Scale, 3);
        }
    }
}
