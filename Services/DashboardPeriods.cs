namespace Winton.Services
{
    /// <summary>
    /// The date ranges behind the dashboard's comparisons. Each prior period covers the same
    /// stretch of days as the current one, so a partial month isn't compared to a full month.
    /// </summary>
    internal readonly record struct DashboardPeriods(
        DateTime YearStart,
        DateTime MonthStart,
        DateTime PreviousMonthStart,
        DateTime PreviousMonthEnd,
        DateTime PreviousYearStart,
        DateTime PreviousYearEnd)
    {
        public static DashboardPeriods For(DateTime today)
        {
            var yearStart = new DateTime(today.Year, 1, 1);
            var monthStart = new DateTime(today.Year, today.Month, 1);

            return new DashboardPeriods(
                yearStart,
                monthStart,
                monthStart.AddMonths(-1),
                today.Date.AddMonths(-1),
                yearStart.AddYears(-1),
                today.Date.AddYears(-1));
        }
    }
}
