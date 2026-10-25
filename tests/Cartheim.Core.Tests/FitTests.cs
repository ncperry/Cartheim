using Cartheim.Core;
using Xunit;

namespace Cartheim.Core.Tests
{
    public class FitTests
    {
        [Fact]
        public void A_piece_wider_than_its_spot_shrinks_to_fit()
        {
            var fit = Fit.Into(spotWidth: 0.5f, spotLength: 0.5f, maxHeight: 2f, width: 2f, length: 1f, height: 1f);

            Assert.Equal(0.25f, fit.Scale, 3);
        }

        [Fact]
        public void A_tall_piece_shrinks_to_the_height_allowed()
        {
            var fit = Fit.Into(spotWidth: 1f, spotLength: 1f, maxHeight: 0.5f, width: 1f, length: 1f, height: 2f);

            Assert.Equal(0.25f, fit.Scale, 3);
        }

        [Fact]
        public void A_piece_lying_across_a_long_spot_turns_to_lie_along_it()
        {
            var fit = Fit.Into(spotWidth: 0.5f, spotLength: 1f, maxHeight: 2f, width: 1f, length: 0.5f, height: 0.5f);

            Assert.True(fit.Turned);
            Assert.Equal(1f, fit.Scale, 3);
        }

        [Fact]
        public void A_small_piece_is_not_enlarged()
        {
            var fit = Fit.Into(spotWidth: 2f, spotLength: 2f, maxHeight: 2f, width: 1f, length: 1f, height: 1f);

            Assert.Equal(1f, fit.Scale, 3);
        }
    }
}
