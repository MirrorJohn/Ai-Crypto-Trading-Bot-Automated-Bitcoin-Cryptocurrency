using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Text;
using Newtonsoft.Json;

namespace TraderTools.Basics
{
    public enum TradeCloseReason
    {
        HitStop,
        HitLimit,
        HitExpiry,
        ManualClose,
        OrderClosed
    }

    public enum OrderType
    {
        /// <summary>
        /// Buy below current market price or sell above current market price.
        /// </summary>
        LimitEntry,

        /// <summary>
        /// Buy above current price or sell below current market price.
        /// </summary>
        StopEntry
    }

    public enum TradeUpdated
    {
        Order,
        Stop,
        Limit
    }

    public enum TradeUpdateMode
    {
        Default,

        /// <summary>
        /// An unchanging trade has the order price, stop, limit and expiry time set upfront then doesn't change.
        /// This type is useful as trades can be cached to speed up simulation.
        /// </summary>
        Unchanging
    }

    public class Trade : INotifyPropertyChanged
    {
        #region Fields
        private decimal? _entryPrice;
        private decimal? _closePrice;
        private TradeDirection? _tradeDirection;
        private decimal? _orderPrice;
        private DateTime? _orderDateTime;
        private DateTime? _orderExpireTime;
        private DateTime? _entryDateTime;
        private decimal? _limitPrice;
        private decimal? _stopPrice;
        private OrderType? _orderType;

        private decimal? _pricePerPip;
        private decimal? _netProfitLoss;
        private Timeframe? _timeframe;
        private DateTime? _closeDateTime;
        private TradeCloseReason? _closeReason;
        private string _comments;
        private string _strategies;
        private decimal? _stopInPips;
        private decimal? _limitInPips;
        private decimal? _initialStopInPips;
        private decimal? _initialLimitInPips;
        private decimal? _rMultiple;
        private decimal? _initialStop;
        private decimal? _initialLimit;
        private decimal? _grossProfitLoss;
        private List<DatePrice> _limitPrices = new List<DatePrice>();
        private List<DatePrice> _stopPrices = new List<DatePrice>();
        private List<DatePrice> _orderPrices = new List<DatePrice>();
        private string _commissionValueCurrency;
        private decimal? _commissionValue;
        private decimal? _entryValue;
        private string _entryValueCurrency;
        private decimal? _riskAmount;
        private bool _isAnalysed;

        [JsonIgnore]
        private Subject<(Trade Trade, TradeUpdated Updated)> _updatedSubject = new Subject<(Trade Trade, TradeUpdated Updated)>();

        #endregion

        public Trade()
        {
        }

        [JsonIgnore]
        public IObservable<(Trade Trade, TradeUpdated Updated)> UpdatedObservable => _updatedSubject.AsObservable();

        public string Comments
        {
            get => _comments;
            set
            {
                _comments = value;
                OnPropertyChanged();
            }
        }

        public TradeUpdateMode UpdateMode { get; set; } = TradeUpdateMode.Default;

        public string Strategies
        {
            get => _strategies ?? (_strategies = string.Empty);
            set
            {
                _strategies = value;
                OnPropertyChanged();
            }
        }

        public Trade SetStrategies(string value)
        {
            Strategies = value;
            return this;
        }

        public DateTime? EntryDateTime
        {
            get => _entryDateTime;
            set
            {
                _entryDateTime = value;
                OnPropertyChanged();
                OnPropertyChanged("EntryDateTimeLocal");
            }
        }

        public string Id { get; set; }

        public string Broker { get; set; }

        public decimal? Commission { get; set; }

        public string CommissionAsset { get; set; }

        public string OrderId { get; set; }

        public bool Ignore { get; set; }

        public decimal? EntryPrice
        {
            get => _entryPrice;
            set
            {
                _entryPrice = value;

                if (!CalculateOptions.HasFlag(CalculateOptions.ExcludePipsCalculations))
                {
                    TradeCalculator.UpdateStopPips(this);
                    TradeCalculator.UpdateInitialStopPips(this);
                    TradeCalculator.UpdateLimitPips(this);
                    TradeCalculator.UpdateInitialLimitPips(this);
                }

                TradeCalculator.UpdateRMultiple(this);
                OnPropertyChanged();
                OnPropertyChanged("Status");
            }
        }

        public decimal? ClosePrice
        {
            get => _closePrice;
            set
            {
                _closePrice = value;
                TradeCalculator.UpdateRMultiple(this);
                OnPropertyChanged();
                OnPropertyChanged("Status");
            }
        }

        public decimal? EntryQuantity { get; set; }

        public decimal? GrossProfitLoss
        {
            get => _grossProfitLoss;
            set
            {
                _grossProfitLoss = value;
                TradeCalculator.UpdateRMultiple(this);
                OnPropertyChanged();
                OnPropertyChanged("Profit");
            }
        }

        public decimal? CommissionValue
        {
            get => _commissionValue;
            set
            {
                _commissionValue = value;
                OnPropertyChanged();
            }
        }

        public string CommissionValueCurrency
        {
            get => _commissionValueCurrency;
            set
            {
                _commissionValueCurrency = value;
                OnPropertyChanged();
            }
        }

        public decimal? EntryValue
        {
            get => _entryValue;
            set
            {
                _entryValue = value;
                OnPropertyChanged();
            }
        }

        public string EntryValueCurrency
        {
            get => _entryValueCurrency;
            set
            {
                _entryValueCurrency = value;
                OnPropertyChanged();
            }
        }

        public decimal? NetProfitLoss
        {
            get => _netProfitLoss;
            set
            {
                _netProfitLoss = value;
                TradeCalculator.UpdateRMultiple(this);
                OnPropertyChanged();
                OnPropertyChanged("Profit");
            }
        }

        public decimal? Profit
        {
            get { return NetProfitLoss ?? GrossProfitLoss; }
        }

        public decimal? Rollover { get; set; }

        public decimal? PricePerPip
        {
            get => _pricePerPip;
            set
            {
                _pricePerPip = value;
                OnPropertyChanged();
            }
        }

        public string Market { get; set; }

        public string BaseAsset { get; set; }

        public bool Alert { get; set; }

        public string CustomText1 { get; set; }

        public Timeframe? Timeframe
        {
            get => _timeframe;
            set
            {
                _timeframe = value;
                OnPropertyChanged();
            }
        }

        public Trade SetChartTimeframe(Timeframe timeframe)
        {
            Timeframe = timeframe;
            return this;
        }

        public TradeDirection? TradeDirection
        {
            get => _tradeDirection;
            set
            {
                _tradeDirection = value;
                OnPropertyChanged();
            }
        }

        public DateTime? CloseDateTime
        {
            get => _closeDateTime;
            set
            {
                _closeDateTime = value;
                OnPropertyChanged();
                OnPropertyChanged("CloseDateTimeLocal");
                OnPropertyChanged("Status");
            }
        }

        [JsonIgnore]
        public CalculateOptions CalculateOptions { get; set; } = CalculateOptions.Default;

        public TradeCloseReason? CloseReason
        {
            get => _closeReason;
            set
            {
                _closeReason = value;
                OnPropertyChanged();
                OnPropertyChanged("Status");
            }
        }


        public DateTime? OrderDateTime
        {
            get => _orderDateTime;
            set
            {
                _orderDateTime = value;
                OnPropertyChanged();
            }
        }

        public OrderType? OrderType
        {
            get => _orderType;
            set
            {
                _orderType = value;
                OnPropertyChanged();
            }
        }

        public decimal? OrderPrice
        {
            get => _orderPrice;
            set
            {
                _orderPrice = value;
                OrderPriceFloat = (float?)value;

                if (!CalculateOptions.HasFlag(CalculateOptions.ExcludePipsCalculations))
                {
                    TradeCalculator.UpdateStopPips(this);
                    TradeCalculator.UpdateInitialStopPips(this);
                    TradeCalculator.UpdateLimitPips(this);
                    TradeCalculator.UpdateInitialLimitPips(this);
                }

                OnPropertyChanged();
                OnPropertyChanged("Status");
                _updatedSubject.OnNext((this, TradeUpdated.Order));
            }
        }

        /// <summary>
        /// Float value used for simulation
        /// </summary>
        [JsonIgnore]
        public float? OrderPriceFloat { get; private set; }

        public List<DatePrice> OrderPrices
        {
            get => _orderPrices;
            private set
            {
                _orderPrices = value;
                OnPropertyChanged();
            }
        }

        public DateTime? OrderExpireTime
        {
            get => _orderExpireTime;
            set
            {
                _orderExpireTime = value;
                OnPropertyChanged();
            }
        }

        public decimal? OrderAmount { get; set; }

        public List<DatePrice> StopPrices
        {
            get => _stopPrices;
            private set
            {
                _stopPrices = value;
                OnPropertyChanged();
            }
        }

        public List<DatePrice> LimitPrices
        {
            get => _limitPrices;
            set
            {
                _limitPrices = value;
                OnPropertyChanged();
            }
        }

        public decimal? RiskAmount
        {
            get => _riskAmount;
            set
            {
                _riskAmount = value;
                TradeCalculator.UpdateRMultiple(this);
            }
        }

        public decimal? RiskPercentOfBalance { get; set; }
        public DateTime? EntryDateTimeLocal => EntryDateTime != null ? (DateTime?)EntryDateTime.Value.ToLocalTime() : null;
        public DateTime? StartDateTimeLocal => OrderDateTime != null ? (DateTime?)OrderDateTime.Value.ToLocalTime() : EntryDateTimeLocal;
        public DateTime? StartDateTime => OrderDateTime != null ? (DateTime?)OrderDateTime.Value : EntryDateTime;

        public decimal? InitialStop
        {
            get => _initialStop;
            set
            {
                _initialStop = value;
                TradeCalculator.UpdateRMultiple(this);
                OnPropertyChanged();
            }
        }

        public decimal? RMultiple
        {
            get => _rMultiple;
            set
            {
                if (_rMultiple == value) return;

                _rMultiple = value;
                OnPropertyChanged();
            }
        }

        public decimal? StopPrice
        {
            get => _stopPrice;
            set
            {
                _stopPrice = value;
                StopPriceFloat = (float?)value;
                OnPropertyChanged();
                _updatedSubject.OnNext((this, TradeUpdated.Stop));
            }
        }

        /// <summary>
        /// Used to speed up simulations.
        /// </summary>
        [JsonIgnore]
        public float? StopPriceFloat { get; private set; }

        public decimal? StopInPips
        {
            get => _stopInPips;
            set
            {
                _stopInPips = value;
                OnPropertyChanged();
            }
        }

        public decimal? LimitInPips
        {
            get => _limitInPips;
            set
            {
                _limitInPips = value;
                OnPropertyChanged();
            }
        }

        public bool IsAnalysed
        {
            get => _isAnalysed;
            set
            {
                _isAnalysed = value;
                OnPropertyChanged();
            }
        }

        public decimal? LimitPrice
        {
            get => _limitPrice;
            set
            {
                _limitPrice = value;
                LimitPriceFloat = (float?)value;
                OnPropertyChanged();
                _updatedSubject.OnNext((this, TradeUpdated.Limit));
            }
        }

        /// <summary>
        /// Used to speed up simulations.
        /// </summary>
        [JsonIgnore]
        public float? LimitPriceFloat { get; private set; }

        public const int CurrentDataVersion = 1;

        public int DataVersion { get; set; } = CurrentDataVersion;

        #region Calculated properties
        public DateTime? CloseDateTimeLocal
        {
            get { return CloseDateTime != null ? (DateTime?)CloseDateTime.Value.ToLocalTime() : null; }
        }

        public DateTime? OrderDateTimeLocal
        {
            get { return OrderDateTime != null ? (DateTime?)OrderDateTime.Value.ToLocalTime() : null; }
        }

        public DateTime? OrderExpireTimeLocal
        {
            get { return OrderExpireTime != null ? (DateTime?)OrderExpireTime.Value.ToLocalTime() : null; }
        }
        #endregion

        public void SetOrder(DateTime dateTime, decimal? price, string market,
            TradeDirection tradeDirection, decimal orderAmount, DateTime? expires)
        {
            OrderDateTime = dateTime;
            AddOrderPrice(dateTime, price);
            Market = market;
            TradeDirection = tradeDirection;
            OrderExpireTime = expires;
            OrderAmount = orderAmount;
        }

        public void SetEntry(DateTime dateTime, decimal price, decimal amount)
        {
            EntryDateTime = dateTime;
            EntryPrice = price;
            EntryQuantity = amount;

            if (OrderDateTime == null)
            {
                OrderDateTime = EntryDateTime;
            }
        }

        public void AddStopPrice(DateTime date, decimal? price)
        {
            AddStopPrice(string.Empty, date, price);
        }

        public void ClearLimitPrices()
        {
            LimitPrices.Clear();
        }

        public void AddStopPrice(string id, DateTime date, decimal? price)
        {
            if (StopPrices.Count > 0 && StopPrices.Last().Price == price)
            {
                return;
            }

            if (UpdateMode == TradeUpdateMode.Unchanging) throw new ApplicationException("Trade set to untouched mode cannot change it's stop price after being set");

            var originalStops = StopPrices.ToList();
            if (StopPrices.Count > 0 && StopPrices.Last().Date == date)
            {
                StopPrices.RemoveAt(StopPrices.Count - 1);
            }

            StopPrices.Add(new DatePrice(id, date, price));

            if (StopPrices.Count > 1)
            {
                StopPrices = StopPrices.OrderBy(x => x.Date).ToList();
            }

            TradeCalculator.UpdateStop(this);

            if (!CalculateOptions.HasFlag(CalculateOptions.ExcludePipsCalculations))
            {
                TradeCalculator.UpdateStopPips(this);
            }

            if (originalStops.Count == 0 || originalStops[0].Price != StopPrices[0].Price)
            {
                InitialStop = StopPrices[0].Price;

                if (!CalculateOptions.HasFlag(CalculateOptions.ExcludePipsCalculations))
                {
                    TradeCalculator.UpdateInitialStopPips(this);
                }
            }
        }

        public void AddOrderPrice(DateTime date, decimal? price)
        {
            if (price == null)
            {
                return;
            }

            if (OrderPrices.Count > 0 && OrderPrices.Last().Price == price)
            {
                return;
            }

            if (UpdateMode == TradeUpdateMode.Unchanging) throw new ApplicationException("Trade set to untouched mode cannot change it's order price after being set");

            if (OrderPrices.Count > 0 && OrderPrices.Last().Date == date)
            {
                OrderPrices.RemoveAt(OrderPrices.Count - 1);
            }

            OrderPrices.Add(new DatePrice(date, price));

            if (OrderPrices.Count > 1)
            {
                OrderPrices = OrderPrices.OrderBy(x => x.Date).ToList();
            }

            OrderPrice = OrderPrices[OrderPrices.Count - 1].Price;
        }

        public void ClearStopPrices()
        {
            if (UpdateMode == TradeUpdateMode.Unchanging) throw new ApplicationException("Trade set to untouched mode cannot change it's stop price after being set");

            StopPrices.Clear();
            StopPrice = null;
        }

        public void RemoveStopPrice(int index)
        {
            if (UpdateMode == TradeUpdateMode.Unchanging) throw new ApplicationException("Trade set to untouched mode cannot change it's stop price after being set");

            if (index >= StopPrices.Count)
            {
                return;
            }

            StopPrices.RemoveAt(index);
            StopPrice = null;
        }

        public void AddLimitPrice(DateTime date, decimal? price)
        {
            AddLimitPrice(string.Empty, date, price);
        }

        public void AddLimitPrice(string id, DateTime date, decimal? price)
        {
            if (LimitPrices.Count > 0 && LimitPrices.Last().Price == price)
            {
                return;
            }

            if (LimitPrices.Count > 0 && LimitPrices.Last().Date == date)
            {
                LimitPrices.RemoveAt(OrderPrices.Count - 1);
            }

            if (UpdateMode == TradeUpdateMode.Unchanging) throw new ApplicationException("Trade set to untouched mode cannot change it's limit price after being set");

            LimitPrices.Add(new DatePrice(id, date, price));

            if (LimitPrices.Count > 1)
            {
                LimitPrices = LimitPrices.OrderBy(x => x.Date).ToList();
            }

            TradeCalculator.UpdateLimit(this);

            if (!CalculateOptions.HasFlag(CalculateOptions.ExcludePipsCalculations))
            {
                TradeCalculator.UpdateLimitPips(this);

                if (LimitPrices.Count == 1)
                {
                    TradeCalculator.UpdateInitialLimitPips(this);
                }
            }
        }

        public void RemoveLimitPrice(int index)
        {
            if (index >= LimitPrices.Count)
            {
                return;
            }

            if (UpdateMode == TradeUpdateMode.Unchanging) throw new ApplicationException("Trade set to untouched mode cannot change it's stop price after being set");

            LimitPrices.RemoveAt(index);
        }

        public void SetClose(DateTime dateTime, decimal? price, TradeCloseReason reason)
        {
            ClosePrice = price;
            CloseDateTime = dateTime;
            CloseReason = reason;
        }

        public void SetExpired(DateTime dateTime)
        {
            CloseDateTime = dateTime;
            CloseReason = TradeCloseReason.HitExpiry;
        }

        public decimal? InitialStopInPips
        {
            get => _initialStopInPips;
            set
            {
                _initialStopInPips = value;
                OnPropertyChanged();
            }
        }

        public decimal? InitialLimitInPips
        {
            get => _initialLimitInPips;
            set
            {
                _initialLimitInPips = value;
                OnPropertyChanged();
            }
        }

        public decimal? InitialLimit
        {
            get => _initialLimit;
            set
            {
                _initialLimit = value;
                OnPropertyChanged();
            }
        }

        public override string ToString()
        {
            var ret = new StringBuilder();

            ret.Append($"{Market} {Broker} {TradeDirection} ");

            if (OrderDateTime != null)
            {
                ret.Append($"Order: {OrderDateTime.Value}UTC {OrderAmount:0.00}@{OrderPrice:0.0000}");
            }

            if (EntryDateTime != null)
            {
                if (ret.Length > 0) ret.Append(" ");

                ret.Append($"Entry: {EntryDateTime.Value}UTC {EntryQuantity:0.0000} @ Price: {EntryPrice:0.0000}");
            }

            if (CloseDateTime != null)
            {
                if (ret.Length > 0) ret.Append(" ");

                ret.Append($"Close: {CloseDateTime.Value}UTC Price: {ClosePrice:0.0000} Reason: {CloseReason}");
            }

            var initialStopInPips = InitialStopInPips;
            if (initialStopInPips != null)
            {
                if (ret.Length > 0) ret.Append(" ");

                if (StopPrices.Count > 0)
                {
                    var stop = StopPrices.First();
                    ret.Append("Initial stop price: ");
                    ret.Append($"{stop.Date}UTC {stop.Price:0.0000} ({initialStopInPips:0}pips)");
                }
                else
                {
                    ret.Append("Initial stop price: -");
                }
            }

            if (Timeframe != null)
            {
                if (ret.Length > 0) ret.Append(" ");

                ret.Append($"Timeframe: {Timeframe}");
            }

            if (NetProfitLoss != null)
            {
                ret.Append($" NetProfit: {NetProfitLoss}");
            }

            return ret.ToString();
        }

        public string Status
        {
            get
            {
                if (OrderPrice != null && EntryPrice == null && CloseDateTime == null)
                {
                    return "Order";
                }

                if (EntryPrice != null && CloseDateTime == null)
                {
                    return "Open";
                }

                if (CloseDateTime != null)
                {
                    switch (CloseReason)
                    {
                        case TradeCloseReason.HitExpiry:
                            return "Hit expiry";
                        case TradeCloseReason.HitLimit:
                            return "Hit limit";
                        case TradeCloseReason.HitStop:
                            return "Hit stop";
                        case TradeCloseReason.OrderClosed:
                            return "Order closed";
                        case TradeCloseReason.ManualClose:
                            return "Closed";
                    }
                }

                return "";
            }
        }

        public List<ChartLine> ChartLines { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}