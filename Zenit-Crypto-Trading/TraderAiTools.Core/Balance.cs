namespace TraderTools.Core
{
    public class Balance
    {
        public string Asset { get; set; }

        public decimal Amount { get; set; }
        public decimal Value { get; set; }
        public decimal PortfolioPercent { get; set; }
    }
}