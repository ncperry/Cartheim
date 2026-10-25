using Cartheim.Core;
using Xunit;

namespace Cartheim.Core.Tests
{
    public class StorageSizeTests
    {
        [Fact]
        public void Cart_storage_holds_twice_the_slots_in_twice_the_rows()
        {
            var (width, height) = StorageSize.Doubled(6, 3);

            Assert.Equal((6, 6), (width, height));
        }
    }
}
