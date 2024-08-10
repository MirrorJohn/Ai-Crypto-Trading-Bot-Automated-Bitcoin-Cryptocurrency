using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TraderTools.Simulation;
using TraderTools.Test.Fakes;
using TraderTools.Test.Strategies;

namespace TraderTools.Test
{
    [TestClass]
    public class StrategyRunnerTest
    {
        private FakeBrokersCandlesService _fakeBrokersCandlesService = new FakeBrokersCandlesService();
        private FakeBroker _broker = new FakeBroker();

        [TestMethod]
        public void TestBuyThenCloseTrade()
        {
            var runner = new StrategyRunner(_fakeBrokersCandlesService, _broker, 10000M);
            var strategy = new StrategyBuyThenClose();
            var trades = runner.Run(strategy);

            Assert.AreEqual(1, trades.Count);
            Assert.AreEqual(3030M, trades[0].EntryPrice);
            Assert.AreEqual(5020M, trades[0].ClosePrice);
            Assert.AreEqual(((5020M * 10000M) / 3030M) - 10000M, trades[0].NetProfitLoss);

            Assert.IsFalse(strategy.Trades.AnyOpen);
            Assert.IsFalse(strategy.Trades.AnyOrders);
            Assert.AreEqual(1, strategy.Trades.ClosedTrades.Count());
        }

        [TestMethod]
        public void TestBuyWithIndicator()
        {
            var runner = new StrategyRunner(_fakeBrokersCandlesService, _broker, 10000M);
            var strategy = new StrategyBuyWithIndicator();
            var trades = runner.Run(strategy);

            Assert.AreEqual(1, trades.Count);
            Assert.AreEqual(5030M, trades[0].EntryPrice);

            Assert.IsTrue(strategy.Trades.AnyOpen);
            Assert.IsFalse(strategy.Trades.AnyOrders);
            Assert.AreEqual(0, strategy.Trades.ClosedTrades.Count());
        }

        [TestMethod]
        public void TestBuyThenCloseTradeWithTransactionFee()
        {
            var runner = new StrategyRunner(_fakeBrokersCandlesService, _broker, 10000M);
            var strategy = new StrategyBuyThenClose();
            var trades = runner.Run(strategy);

            Assert.AreEqual(1, trades.Count);
            Assert.AreEqual(3030M, trades[0].EntryPrice);
            Assert.AreEqual(5020M, trades[0].ClosePrice);

            //total = amount + (amount * fee)
            //      = amount * (1 + fee)
            var bought = trades[0].EntryQuantity.Value * trades[0].EntryPrice.Value;
            var buyFee = bought * 0.01m;
            Assert.AreEqual(10000M, bought + buyFee);

            var sold = trades[0].ClosePrice.Value * trades[0].EntryQuantity.Value;
            var sellFee = sold * 0.01m;

            Assert.AreEqual(sold - bought - buyFee - sellFee, trades[0].NetProfitLoss);
            Assert.AreEqual(runner.Balance, 10000M + trades[0].NetProfitLoss);

            Assert.IsFalse(strategy.Trades.AnyOpen);
            Assert.IsFalse(strategy.Trades.AnyOrders);
            Assert.AreEqual(1, strategy.Trades.ClosedTrades.Count());
        }

        [TestMethod]
        public void TestBuyWithStopTrade()
        {
            var runner = new StrategyRunner(_fakeBrokersCandlesService, _broker, 10000M);
            var strategy = new StrategyBuyWithStop();
            var trades = runner.Run(strategy);

            Assert.AreEqual(1, trades.Count);
            Assert.AreEqual(15030M, trades[0].EntryPrice);
            Assert.AreEqual(7000M, trades[0].ClosePrice);
            Assert.AreEqual(10000M, trades[0].CloseDateTime.Value.Ticks);
            Assert.AreEqual((Math.Round((7000M * 10000M) / 15030M) - 10000M), Math.Round(trades[0].NetProfitLoss.Value));

            Assert.IsFalse(strategy.Trades.AnyOpen);
            Assert.IsFalse(strategy.Trades.AnyOrders);
            Assert.AreEqual(1, strategy.Trades.ClosedTrades.Count());
        }

        [TestMethod]
        public void TestBuyWithLimit()
        {
            var runner = new StrategyRunner(_fakeBrokersCandlesService, _broker, 10000M);
            var strategy = new StrategyBuyWithLimit();
            var trades = runner.Run(strategy);

            Assert.AreEqual(1, trades.Count);
            Assert.AreEqual(3030M, trades[0].EntryPrice);
            Assert.AreEqual(7000M, trades[0].ClosePrice);
            Assert.AreEqual(8000M, trades[0].CloseDateTime.Value.Ticks);
            Assert.AreEqual((Math.Round((7000M * 10000M) / 3030M) - 10000M), Math.Round(trades[0].NetProfitLoss.Value));

            Assert.IsFalse(strategy.Trades.AnyOpen);
            Assert.IsFalse(strategy.Trades.AnyOrders);
            Assert.AreEqual(1, strategy.Trades.ClosedTrades.Count());
        }

        //todo: order expire, trade // with more than balance, invalid trade

        [TestMethod]
        public void TestBuyWithOrder()
        {
            var runner = new StrategyRunner(_fakeBrokersCandlesService, _broker, 10000M);
            var strategy = new StrategyBuyWithOrder();
            var trades = runner.Run(strategy);

            Assert.AreEqual(1, trades.Count);
            Assert.AreEqual(6000M, trades[0].EntryPrice);
            Assert.AreEqual(7000, trades[0].EntryDateTime.Value.Ticks);

            Assert.IsTrue(strategy.Trades.AnyOpen);
            Assert.IsFalse(strategy.Trades.AnyOrders);
            Assert.AreEqual(0, strategy.Trades.ClosedTrades.Count());
            Assert.IsTrue(strategy.Trades.AllTrades.All(x => x.Trade.EntryPrice != null));
            Assert.AreNotEqual(10000M, runner.Balance);
        }

        [TestMethod]
        public void TestExpiredOrder()
        {
            var runner = new StrategyRunner(_fakeBrokersCandlesService, _broker, 10000M);
            var strategy = new StrategyBuyWithExpiredOrder(3000);
            var trades = runner.Run(strategy);

            Assert.AreEqual(1, trades.Count);
            Assert.AreEqual(null, trades[0].EntryPrice);
            Assert.AreEqual(null, trades[0].EntryDateTime);

            Assert.IsFalse(strategy.Trades.AnyOpen);
            Assert.IsFalse(strategy.Trades.AnyOrders);
            Assert.AreEqual(1, strategy.Trades.ClosedTrades.Count());
            Assert.IsTrue(strategy.Trades.AllTrades.All(x => x.Trade.EntryPrice == null));
            Assert.AreEqual(10000M, runner.Balance);
        }

        [TestMethod]
        public void TestBuyCloseTwoMarkets()
        {
            var runner = new StrategyRunner(_fakeBrokersCandlesService, _broker, 10000M);
            var strategy = new StrategyBuyCloseTwoMarkets();
            var trades = runner.Run(strategy);

            Assert.AreEqual(2, trades.Count);
            Assert.AreEqual(8030M, trades[0].EntryPrice);
            Assert.AreEqual(7000M, trades[0].ClosePrice);

            Assert.AreEqual(4030M, trades[1].EntryPrice);
            Assert.AreEqual(6000M, trades[1].ClosePrice);

            Assert.IsFalse(strategy.Trades.AnyOpen);
            Assert.IsFalse(strategy.Trades.AnyOrders);
            Assert.AreEqual(2, strategy.Trades.ClosedTrades.Count());
        }

        [TestMethod]
        public void TestTotalValue()
        {
            var runner = new StrategyRunner(_fakeBrokersCandlesService, _broker, 10000M);
            var s = new StrategyTotalValue();
            var trades = runner.Run(s);

            var trade1Cost = (s.Trade1.EntryPrice.Value * s.Trade1.EntryQuantity) + (s.Trade1.EntryPrice.Value * s.Trade1.EntryQuantity) * 0.001M;
            var trade2Cost = (s.Trade2.EntryPrice.Value * s.Trade2.EntryQuantity) + (s.Trade2.EntryPrice.Value * s.Trade2.EntryQuantity) * 0.001M;
            Func<decimal, decimal> trade1Value = p => (s.Trade1.EntryQuantity.Value * p) - (s.Trade1.EntryQuantity.Value * p) * 0.001M;
            Func<decimal, decimal> trade2Value = p => (s.Trade2.EntryQuantity.Value * p) - (s.Trade2.EntryQuantity.Value * p) * 0.001M;

            Assert.AreEqual(2, trades.Count);

            Assert.AreEqual(10000M, s.TotalValuesBeforeTrades[3500]);
            Assert.AreEqual(10000M - trade1Cost + trade1Value(3020M), s.TotalValuesAfterTrades[3500]);

            var price = 4020M;
            Assert.AreEqual(10000M - trade1Cost + trade1Value(price), s.TotalValuesBeforeTrades[4500]);
            Assert.AreEqual(10000M - trade1Cost + trade1Value(price) - trade2Cost + trade2Value(price), s.TotalValuesAfterTrades[4500]);

            price = 7020M;
            Assert.AreEqual(10000M - trade1Cost + trade1Value(price) - trade2Cost + trade2Value(price), s.TotalValuesBeforeTrades[7500]);
            Assert.AreEqual(10000M - trade1Cost + trade1Value(price) - trade2Cost + trade2Value(price), s.TotalValuesAfterTrades[7500]);
        }
    }
}