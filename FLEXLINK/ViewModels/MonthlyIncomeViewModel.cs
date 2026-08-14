namespace FLEXLINK.ViewModels
{
    public class MonthlyIncomeViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int MembershipCount { get; set; }
        public decimal Total { get; set; }

        public int OneMonthCount { get; set; }
        public int TwoMonthCount { get; set; }
        public int ThreeMonthCount { get; set; }

        public string MonthLabel =>
            new DateTime(Year, Month, 1).ToString("MMMM yyyy");
    }
}