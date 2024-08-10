namespace TraderTools.Basics.Helpers
{
    public static class PipsHelper
    {
        public static decimal GetPriceInPips(decimal price, MarketDetails market)
        {
            return price / market.PointSize.Value;
        }
    }
}