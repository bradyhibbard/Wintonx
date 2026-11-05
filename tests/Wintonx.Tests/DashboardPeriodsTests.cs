using Winton.Services;
using Xunit;

namespace Winton.Tests
{
    public class DashboardPeriodsTests
    {
        [Fact]
        public void Mid_month_compares_the_same_days_of_the_prior_periods()
        {
            var periods = DashboardPeriods.For(new DateTime(2026, 11, 5));

            Assert.Equal(new DateTime(2026, 1, 1), periods.YearStart);
            Assert.Equal(new DateTime(2026, 11, 1), periods.MonthStart);
            Assert.Equal(new DateTime(2026, 10, 1), periods.PreviousMonthStart);
            Assert.Equal(new DateTime(2026, 10, 5), periods.PreviousMonthEnd);
            Assert.Equal(new DateTime(2025, 1, 1), periods.PreviousYearStart);
            Assert.Equal(new DateTime(2025, 11, 5), periods.PreviousYearEnd);
        }

        [Fact]
        public void January_compares_to_December_of_the_prior_year()
        {
            var periods = DashboardPeriods.For(new DateTime(2026, 1, 15));

            Assert.Equal(new DateTime(2025, 12, 1), periods.PreviousMonthStart);
            Assert.Equal(new DateTime(2025, 12, 15), periods.PreviousMonthEnd);
        }

        [Fact]
        public void A_longer_month_stops_at_the_end_of_a_shorter_prior_month()
        {
            var periods = DashboardPeriods.For(new DateTime(2026, 3, 31));

            Assert.Equal(new DateTime(2026, 2, 28), periods.PreviousMonthEnd);
        }

        [Fact]
        public void Leap_day_compares_to_February_28_of_the_prior_year()
        {
            var periods = DashboardPeriods.For(new DateTime(2028, 2, 29));

            Assert.Equal(new DateTime(2027, 2, 28), periods.PreviousYearEnd);
            Assert.Equal(new DateTime(2028, 1, 29), periods.PreviousMonthEnd);
        }

        [Fact]
        public void A_time_of_day_is_ignored()
        {
            var periods = DashboardPeriods.For(new DateTime(2026, 11, 5, 14, 30, 0));

            Assert.Equal(new DateTime(2026, 10, 5), periods.PreviousMonthEnd);
        }
    }
}
