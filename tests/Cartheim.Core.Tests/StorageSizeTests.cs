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

        [Fact]
        public void Growing_storage_keeps_its_own_rows_while_empty()
        {
            Assert.Equal(6, StorageSize.GrowingRows(baseRows: 6, lowestUsedRow: -1));
        }

        [Fact]
        public void Growing_storage_keeps_an_empty_row_below_its_lowest_item()
        {
            Assert.Equal(7, StorageSize.GrowingRows(baseRows: 6, lowestUsedRow: 5));
            Assert.Equal(42, StorageSize.GrowingRows(baseRows: 6, lowestUsedRow: 40));
        }
    }
}
