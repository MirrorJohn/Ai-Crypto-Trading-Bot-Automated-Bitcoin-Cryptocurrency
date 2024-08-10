using System;
using System.Collections.Generic;
using System.Linq;
using TraderTools.Basics;

namespace TraderTools.Simulation
{
    public class LinkedItem<T>
    {
        public T Next { get; set; }
        public T Prev { get; set; }
    }

    public class TradeWithIndexingCollection
    {
        private TradeWithIndexing _firstOrder;
        private TradeWithIndexing _firstOpen;
        private TradeWithIndexing _firstClosed;

        public bool AnyOrders => _firstOrder != null;

        public bool AnyOpen => _firstOpen != null;

        public bool AnyOpenWithStopOrLimit
        {
            get
            {
                if (!AnyOpen) return false;

                foreach (var t in OpenTrades)
                {
                    if (t.Trade.StopPrice != null || t.Trade.LimitPrice != null)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public void AddOpenTrade(Trade t)
        {
            var tradeWithIndexing = new TradeWithIndexing
            {
                Trade = t
            };

            AddToListStart(tradeWithIndexing, ref _firstOpen);
        }

        public int CachedTradesCount { get; set; } = 0;

        public void AddClosedTrade(Trade t)
        {
            var tradeWithIndexing = new TradeWithIndexing
            {
                Trade = t
            };

            AddToListStart(tradeWithIndexing, ref _firstClosed);
        }

        public void AddOrderTrade(Trade t)
        {
            var tradeWithIndexing = new TradeWithIndexing
            {
                Trade = t
            };

            if (_firstOrder == null)
            {
                _firstOrder = tradeWithIndexing;
                return;
            }

            if (t.OrderDateTime.Value.Ticks <= _firstOrder.Trade.OrderDateTime.Value.Ticks)
            {
                AddToListStart(tradeWithIndexing, ref _firstOrder);
                return;
            }

            // Find position
            var positionNext = _firstOrder;
            while (positionNext.Next != null && positionNext.Next.Trade.OrderDateTime.Value.Ticks < t.OrderDateTime.Value.Ticks)
            {
                positionNext = positionNext.Next;
            }

            tradeWithIndexing.Next = positionNext.Next;
            tradeWithIndexing.Prev = positionNext;
            positionNext.Next = tradeWithIndexing;
        }

        public void MoveOrderToOpen(TradeWithIndexing t)
        {
            RemoveFromList(t, ref _firstOrder);
            AddToListStart(t, ref _firstOpen);
        }

        public void MoveOrderToClosed(TradeWithIndexing t)
        {
            RemoveFromList(t, ref _firstOrder);
            AddToListStart(t, ref _firstClosed);
        }

        public void MoveOpenToClose(TradeWithIndexing t)
        {
            RemoveFromList(t, ref _firstOpen);
            AddToListStart(t, ref _firstClosed);
        }

        private void AddToListStart(TradeWithIndexing t, ref TradeWithIndexing first)
        {
            t.Prev = null;
            t.Next = first;
            if (first != null) first.Prev = t;
            first = t;
        }

        private void RemoveFromList(TradeWithIndexing t, ref TradeWithIndexing first)
        {
            if (first == t)
            {
                first = t.Next;
                if (first != null) first.Prev = null;
            }
            else
            {
                t.Prev.Next = t.Next;
                if (t.Next != null) t.Next.Prev = t.Prev;
            }
        }

        public IEnumerable<TradeWithIndexing> OpenTrades
        {
            get
            {
                if (_firstOpen == null) yield break;

                var v = _firstOpen;
                while (v != null)
                {
                    var n = v.Next;
                    yield return v;
                    v = n;
                }
            }
        }

        public IEnumerable<TradeWithIndexing> OrderTradesAsc
        {
            get
            {
                if (_firstOrder == null) yield break;

                var v = _firstOrder;
                while (v != null)
                {
                    var n = v.Next;
                    yield return v;
                    v = n;
                }
            }
        }

        public IEnumerable<TradeWithIndexing> AllTrades => OrderTradesAsc.Concat(OpenTrades).Concat(ClosedTrades);

        public IEnumerable<TradeWithIndexing> ClosedTrades
        {
            get
            {
                if (_firstClosed == null) yield break;

                var v = _firstClosed;
                while (v != null)
                {
                    var n = v.Next;
                    yield return v;
                    v = n;
                }
            }
        }

        public void AddTrade(Trade newTrade)
        {
            if (newTrade.CloseDateTime != null)
            {
                AddClosedTrade(newTrade);
            }
            else if (newTrade.EntryPrice == null && newTrade.OrderPrice != null)
            {
                AddOrderTrade(newTrade);
            }
            else if (newTrade.EntryPrice != null)
            {
                AddOpenTrade(newTrade);
            }
        }

        public void MoveTrades()
        {
            foreach (var t in OpenTrades.ToList())
            {
                if (t.Trade.CloseDateTime != null)
                {
                    MoveOpenToClose(t);
                }
            }

            foreach (var t in OrderTradesAsc.ToList())
            {
                if (t.Trade.EntryDateTime != null && t.Trade.CloseDateTime == null)
                {
                    MoveOrderToOpen(t);
                }
                else if (t.Trade.EntryDateTime != null && t.Trade.CloseDateTime != null)
                {
                    MoveOrderToClosed(t);
                }
            }
        }
    }
}