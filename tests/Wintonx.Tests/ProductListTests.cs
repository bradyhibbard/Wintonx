using Winton.Services;
using Xunit;
using static Winton.Tests.ExcelFile;

namespace Winton.Tests
{
    public class ProductListTests : DatabaseTest
    {
        private string ProductFile(params object[][] rows) => Write(TempFolder, ProductHeaders, rows);

        [Fact]
        public async Task Import_adds_products_and_returns_the_count()
        {
            int count = await ImportServices.ImportProductListAsync(ProductFile(Product("A", "Chair"), Product("B", "Sofa")));

            Assert.Equal(2, count);
            Assert.Equal(2, (await ProductService.GetProductsAsync()).Count);
        }

        [Fact]
        public async Task Import_reads_each_field_from_its_column()
        {
            await ImportServices.ImportProductListAsync(ProductFile(Product("A", "Chair", "Acme", "Seating", "Indoor")));

            var product = Assert.Single(await ProductService.GetProductsAsync());
            Assert.Equal("A", product.ItemNumber);
            Assert.Equal("Chair", product.ItemName);
            Assert.Equal("Acme", product.Vendor);
            Assert.Equal("Seating", product.Cat);
            Assert.Equal("Indoor", product.Grp);
        }

        [Fact]
        public async Task Vendor_category_and_group_update_when_the_name_is_unchanged()
        {
            AddProduct("A", "Chair", "Old", "Old", "Old");

            await ImportServices.ImportProductListAsync(ProductFile(Product("A", "Chair", "New", "NewCat", "NewGrp")));

            var product = Assert.Single(await ProductService.GetProductsAsync());
            Assert.Equal("New", product.Vendor);
            Assert.Equal("NewCat", product.Cat);
            Assert.Equal("NewGrp", product.Grp);
        }

        [Fact]
        public async Task Items_not_in_the_file_are_removed()
        {
            AddProduct("OLD");

            await ImportServices.ImportProductListAsync(ProductFile(Product("A", "Chair")));

            Assert.Equal("A", Assert.Single(await ProductService.GetProductsAsync()).ItemNumber);
        }

        [Fact]
        public async Task Repeated_and_blank_items_are_handled()
        {
            int count = await ImportServices.ImportProductListAsync(ProductFile(
                Product("A", "First"), Product("A", "Second"), Product("  ", "Blank"), Product(" B ", "Spaces")));

            Assert.Equal(2, count);
            var products = await ProductService.GetProductsAsync();
            Assert.Equal("First", products.Single(p => p.ItemNumber == "A").ItemName);
            Assert.Contains(products, p => p.ItemNumber == "B");
        }

        [Fact]
        public async Task A_file_with_no_products_fails_and_keeps_the_current_list()
        {
            AddProduct("A");

            await Assert.ThrowsAsync<InvalidOperationException>(() => ImportServices.ImportProductListAsync(ProductFile(Product("", "No item"))));

            Assert.Single(await ProductService.GetProductsAsync());
        }

        [Fact]
        public async Task Product_ids_are_not_reused_after_delete_all()
        {
            AddProduct("A");
            int oldId = Scalar<int>("SELECT ProductID FROM Products WHERE ItemNumber = 'A'");

            Assert.True(await ImportServices.DeleteAllProductsAsync());
            AddProduct("B");

            Assert.True(Scalar<int>("SELECT ProductID FROM Products WHERE ItemNumber = 'B'") > oldId);
        }

        [Fact]
        public async Task Import_repairs_placements_pointing_at_the_wrong_product()
        {
            AddSection("S1");
            AddPlacement("A", "S1", Jan(1), productId: 999);

            await ImportServices.ImportProductListAsync(ProductFile(Product("A", "Chair")));

            int productId = Scalar<int>("SELECT ProductID FROM Products WHERE ItemNumber = 'A'");
            Assert.Equal(productId, Scalar<int>("SELECT ProductID FROM ProductPlacements WHERE ItemNumber = 'A'"));
        }

        [Fact]
        public async Task A_placed_product_can_be_dropped_from_the_list_by_an_import()
        {
            AddSection("S1");
            AddProduct("OLD");
            AddPlacement("OLD", "S1", Jan(1));

            await ImportServices.ImportProductListAsync(ProductFile(Product("A", "Chair")));

            Assert.Equal("A", Assert.Single(await ProductService.GetProductsAsync()).ItemNumber);
            Assert.Equal("OLD", Assert.Single(await ProductPlacementServices.GetProductsBySectionAsync("S1")).ItemNumber);
        }

        [Fact]
        public async Task Placed_products_stay_listed_after_leaving_the_product_list()
        {
            AddSection("S1");
            AddProduct("A");
            AddPlacement("A", "S1", Jan(1));

            await ImportServices.DeleteAllProductsAsync();

            Assert.Equal("A", Assert.Single(await ProductPlacementServices.GetProductsBySectionAsync("S1")).ItemNumber);
        }
    }
}
