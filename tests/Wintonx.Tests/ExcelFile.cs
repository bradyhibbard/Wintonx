using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace Winton.Tests
{
    /// <summary>A formula cell with its already-calculated result.</summary>
    public sealed record Formula(string Text, double Result);

    /// <summary>
    /// Writes small spreadsheets for import tests. The file type follows the extension (.xlsx or .xls).
    /// </summary>
    public static class ExcelFile
    {
        public static readonly string[] SalesHeaders =
            { "Vendor Model", "Description", "Group", "Category", "Cse Sell Price", "Qty Shp", "Tr" };

        public static string Write(string folder, string[] headers, params object[][] rows) =>
            Write(folder, ".xlsx", headers, rows);

        public static string Write(string folder, string extension, string[] headers, params object[][] rows)
        {
            IWorkbook workbook = extension == ".xls" ? new HSSFWorkbook() : new XSSFWorkbook();
            ISheet sheet = workbook.CreateSheet("Sheet1");

            IRow headerRow = sheet.CreateRow(0);
            for (int col = 0; col < headers.Length; col++)
                headerRow.CreateCell(col).SetCellValue(headers[col]);

            for (int r = 0; r < rows.Length; r++)
            {
                IRow row = sheet.CreateRow(r + 1);
                for (int col = 0; col < rows[r].Length; col++)
                {
                    switch (rows[r][col])
                    {
                        case null:
                            break;
                        case string text:
                            row.CreateCell(col).SetCellValue(text);
                            break;
                        case Formula formula:
                            ICell cell = row.CreateCell(col);
                            cell.SetCellFormula(formula.Text);
                            cell.SetCellValue(formula.Result);
                            break;
                        default:
                            row.CreateCell(col).SetCellValue(Convert.ToDouble(rows[r][col]));
                            break;
                    }
                }
            }

            string path = Path.Combine(folder, Guid.NewGuid().ToString("N") + extension);
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
                workbook.Write(stream, false);
            return path;
        }

        /// <summary>One sales report row in SalesHeaders order.</summary>
        public static object[] Sale(object item, object price, object quantity, object code) =>
            new[] { item, "Description", "Group", "Category", price, quantity, code };

        /// <summary>One product list row: the app reads columns 1, 2, 4, 6 and 7.</summary>
        public static object[] Product(string item, string name, string vendor = "Vendor", string category = "Cat", string group = "Grp") =>
            new object[] { item, name, null, vendor, null, category, group };

        public static readonly string[] ProductHeaders =
            { "Item", "Name", "Unused", "Vendor", "Unused", "Category", "Group" };
    }
}
