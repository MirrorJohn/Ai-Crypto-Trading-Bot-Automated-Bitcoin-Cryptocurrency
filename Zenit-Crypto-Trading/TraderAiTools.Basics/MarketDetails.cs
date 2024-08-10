namespace TraderTools.Basics
{
    public class MarketDetails
    {
        public MarketDetails()
        {
        }

        public MarketDetails(
            string broker, string name, string currency, decimal? pointSize, int? digits,
            int? minLotSize, decimal? contractMultiplier)
        {
            Broker = broker;
            Name = name;
            Currency = currency;
            PointSize = pointSize;
            Digits = digits;
            MinLotSize = minLotSize;
            ContractMultiplier = contractMultiplier;
        }

        public int? Digits { get; set; }
        public decimal? PointSize { get; set; }
        public int? MinLotSize { get; set; }
        public decimal? ContractMultiplier { get; set; }
        public string Currency { get; set; }
        public string Name { get; set; }
        public string Broker { get; set; }

        public static string GetKey(string broker, string market)
        {
            return $"{broker}-{market}";
        }
    }
}