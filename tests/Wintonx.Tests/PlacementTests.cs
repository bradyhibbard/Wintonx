using Winton.Services;
using Xunit;

namespace Winton.Tests
{
    public class PlacementTests : DatabaseTest
    {
        [Fact]
        public async Task Placing_a_product_adds_it_to_the_section()
        {
            AddSection("S1");
            AddProduct("A");

            await ProductPlacementServices.PlaceProductAsync("A", "S1");

            Assert.Equal("A", Assert.Single(await ProductPlacementServices.GetProductsBySectionAsync("S1")).ItemNumber);
        }

        [Fact]
        public async Task Placing_the_same_product_twice_adds_it_once()
        {
            AddSection("S1");
            AddProduct("A");

            await ProductPlacementServices.PlaceProductAsync("A", "S1");
            await ProductPlacementServices.PlaceProductAsync("A", "S1");

            Assert.Single(await ProductPlacementServices.GetProductsBySectionAsync("S1"));
        }

        [Fact]
        public async Task A_product_can_be_placed_again_after_removal()
        {
            AddSection("S1");
            AddProduct("A");

            await ProductPlacementServices.PlaceProductAsync("A", "S1");
            await ProductPlacementServices.RemoveItemFromSectionAsync("A", "S1", DateTime.Now, "Removed manually");
            await ProductPlacementServices.PlaceProductAsync("A", "S1");

            Assert.Single(await ProductPlacementServices.GetProductsBySectionAsync("S1"));
        }

        [Fact]
        public async Task Removal_affects_only_the_chosen_section()
        {
            AddSection("S1");
            AddSection("S2");
            AddPlacement("A", "S1", Jan(1));
            AddPlacement("A", "S2", Jan(1));

            await ProductPlacementServices.RemoveItemFromSectionAsync("A", "S2", Jan(10), "Removed manually");

            Assert.Empty(await ProductPlacementServices.GetProductsBySectionAsync("S2"));
            Assert.Single(await ProductPlacementServices.GetProductsBySectionAsync("S1"));
        }

        [Fact]
        public async Task Removal_saves_the_notes_and_date_to_the_archive()
        {
            AddSection("S1");
            AddPlacement("A", "S1", Jan(1));

            await ProductPlacementServices.RemoveItemFromSectionAsync("A", "S1", Jan(10), "Removed manually");

            Assert.Equal("Removed manually", Scalar<string>("SELECT RemovalNotes FROM Archive"));
            var archived = Assert.Single(await ProductPlacementServices.GetArchivedProductsBySectionAsync("S1"));
            Assert.Equal(Jan(10), archived.DateRemoved);
        }

        [Fact]
        public async Task Archived_revenue_covers_only_the_time_the_product_was_placed()
        {
            AddSection("S1");
            AddPlacement("A", "S1", Jan(10));
            AddSale("A", Jan(5), 1000m);
            AddSale("A", Jan(15), 100m);
            AddSale("A", Jan(25), 1000m);

            await ProductPlacementServices.RemoveItemFromSectionAsync("A", "S1", Jan(20), "Removed manually");

            Assert.Equal(100m, Assert.Single(await ProductPlacementServices.GetArchivedProductsBySectionAsync("S1")).Revenue);
        }

        [Fact]
        public async Task Placement_lookup_uses_the_chosen_section()
        {
            AddSection("S1");
            AddSection("S2");
            AddPlacement("A", "S1", Jan(1));
            AddPlacement("A", "S2", Jan(15));

            var placement = await ProductPlacementServices.GetPlacementInSectionAsync("A", "S2");

            Assert.Equal(Jan(15), placement.DatePlaced);
            Assert.Null(await ProductPlacementServices.GetPlacementInSectionAsync("A", "S3"));
        }

        [Fact]
        public async Task Editing_a_placed_date_affects_only_that_section_and_moves_its_sales()
        {
            AddSection("S1");
            AddSection("S2");
            AddPlacement("A", "S1", Jan(1));
            AddPlacement("A", "S2", Jan(20));
            AddSale("A", Jan(10), 100m);

            await ProductPlacementServices.UpdateDatePlacedAsync("A", "S2", Jan(5));

            Assert.Equal(Jan(1), (await ProductPlacementServices.GetPlacementInSectionAsync("A", "S1")).DatePlaced);
            Assert.Equal(Jan(5), (await ProductPlacementServices.GetPlacementInSectionAsync("A", "S2")).DatePlaced);
            Assert.Equal(50m, await SalesDataServices.GetRevenueBySectionAsync("S2", Jan(1), Jan(31)));
        }

        [Fact]
        public async Task Deleting_a_section_archives_its_current_products()
        {
            AddSection("S1");
            AddPlacement("A", "S1", Jan(1));

            await ProductPlacementServices.ArchiveAndDeleteSectionAsync("S1");

            Assert.Equal(0, Scalar<int>("SELECT COUNT(*) FROM Sections"));
            Assert.Equal(0, Scalar<int>("SELECT COUNT(*) FROM ProductPlacements"));
            Assert.Equal("Section Deleted", Scalar<string>("SELECT RemovalNotes FROM Archive"));
        }

        [Fact]
        public async Task Deleting_a_section_works_after_products_were_removed_from_it()
        {
            // This used to fail while still reporting success.
            AddSection("S1");
            AddPlacement("A", "S1", Jan(1));
            AddPlacement("B", "S1", Jan(1));
            await ProductPlacementServices.RemoveItemFromSectionAsync("A", "S1", Jan(10), "Removed manually");

            await ProductPlacementServices.ArchiveAndDeleteSectionAsync("S1");

            Assert.Equal(0, Scalar<int>("SELECT COUNT(*) FROM Sections"));
            Assert.Equal(1, Scalar<int>("SELECT COUNT(*) FROM Archive WHERE ItemNumber = 'A'"));
            Assert.Equal(1, Scalar<int>("SELECT COUNT(*) FROM Archive WHERE ItemNumber = 'B'"));
        }

        [Fact]
        public async Task A_section_with_earlier_archive_history_can_be_deleted_and_keeps_that_history()
        {
            AddSection("S1");
            AddPlacement("A", "S1", Jan(1));
            await ProductPlacementServices.RemoveItemFromSectionAsync("A", "S1", Jan(10), "Removed manually");

            await ProductPlacementServices.ArchiveAndDeleteSectionAsync("S1");

            Assert.Equal(0, Scalar<int>("SELECT COUNT(*) FROM Sections"));
            Assert.Single(await ProductPlacementServices.GetArchivedProductsBySectionAsync("S1"));
        }

        [Fact]
        public async Task Correcting_archived_dates_moves_the_sales_with_them()
        {
            AddSection("S1");
            AddPlacement("A", "S1", Jan(10));
            AddSale("A", Jan(5), 100m);
            await ProductPlacementServices.RemoveItemFromSectionAsync("A", "S1", Jan(20), "Removed manually");
            int archiveId = Scalar<int>("SELECT PlacementID FROM Archive");

            await ProductPlacementServices.UpdateArchiveDatesAsync(archiveId, Jan(1), Jan(20));

            Assert.Equal(100m, await SalesDataServices.GetRevenueBySectionAsync("S1", Jan(1), Jan(31)));
        }

        [Fact]
        public async Task Filters_find_sections_by_current_placements_only()
        {
            AddSection("S1");
            AddSection("S2");
            AddPlacement("A", "S1", Jan(1));
            AddPlacement("A", "S2", Jan(1), removed: Jan(5));

            var ids = await ProductPlacementServices.GetSectionIdsForItemNumbersAsync(new[] { "A" });

            Assert.Equal("S1", Assert.Single(ids));
        }
    }
}
