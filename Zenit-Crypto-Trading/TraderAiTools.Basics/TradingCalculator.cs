using System.Collections.Generic;
using System.Linq;

namespace TraderTools.Basics
{
    public static class TradingCalculator
    {
        public static decimal CalculateMaxDrawdownPercent(decimal startBalance, List<Trade> trades, bool includeIgnored = false)
        {
            var balance = startBalance;
            var high = 0M;
            var maxDrawdownPercent = 0M;

            foreach (var t in trades.OrderBy(x => x.OrderDateTime ?? x.EntryDateTime))
            {
                balance += t.Profit ?? 0M;
                if (balance > high) high = balance;

                if (balance < high)
                {
                    var drawdownPercent = (1 - (balance / high)) * 100M;
                    if (drawdownPercent > maxDrawdownPercent)
                    {
                        maxDrawdownPercent = drawdownPercent;
                    }
                }
            }

            return maxDrawdownPercent;
        }

        public static decimal CalculateExpectancy(List<Trade> ts, bool includeIgnored = false)
        {
            // Trades.Count(x => x.RMultiple != null) > 0 ? Trades.Where(t => t.RMultiple != null).Sum(t => t.RMultiple.Value) / Trades.Count(x => x.RMultiple != null) : 0;
            var trades = ts;

            if (!includeIgnored)
            {
                trades = ts.Where(x => x.Ignore == false).ToList();
            }

            if (trades.Count == 0) return 0M;

            var winRate = (decimal)trades.Count(t => t.RMultiple > 0) / (decimal)trades.Count;

            var winTrades = trades.Where(t => t.RMultiple > 0).ToList();
            var averageWin = winTrades.Count > 0 ? winTrades.Average(t => t.RMultiple.Value) : 0;
            var loseTrades = trades.Where(t => t.RMultiple <= 0).ToList();
            var averageLose = loseTrades.Count > 0 ? trades.Where(t => t.RMultiple <= 0).Average(t => -t.RMultiple.Value) : 0;

            return (winRate * averageWin) - ((1 - winRate) * averageLose);
        }
    }
}