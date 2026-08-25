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

        // NEW — who bought what, for the expandable row
        public List<MembershipSaleDetail> Details { get; set; } = new();

        public string MonthLabel =>
            new DateTime(Year, Month, 1).ToString("MMMM yyyy");
    }
    public class MembershipSaleDetail
    {
        public string UserName { get; set; } = "";
        public int Months { get; set; }
        public decimal Price { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime ExpiryDate { get; set; }

        public string PlanLabel => Months switch
        {
            1 => "1 Month",
            2 => "2 Months",
            3 => "3 Months",
            _ => $"{Months} Months"
        };
    }
}