﻿using CryptoMarketClient.Analytics;
using CryptoMarketClient.Common;
using CryptoMarketClient.Strategies;
using DevExpress.Skins;
using DevExpress.XtraCharts;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CryptoMarketClient {
    public abstract class TickerBase {
        public TickerBase() {
            OrderBook = new OrderBook(this);
            BidAskChart = CreateChartSnapshotControl();
            OrderBookChart = CreateOrderBookChartControl();
            OrderBookSnapshot = CreateOrderBookSnapshotImage();
            BidAskSnapshot = CreateChartSnapshotImage();
        }

        public BindingList<TickerHistoryItem> History { get; } = new BindingList<TickerHistoryItem>();
        public BindingList<TradeHistoryItem> TradeHistory { get; } = new BindingList<TradeHistoryItem>();
        public BindingList<TradeStatisticsItem> TradeStatistic { get; } = new BindingList<TradeStatisticsItem>();
        public List<TickerStrategyBase> Strategies { get; } = new List<TickerStrategyBase>();
        public BindingList<CandleStickData> CandleStickData { get; set; } = new BindingList<CryptoMarketClient.CandleStickData>();
        public BindingList<CurrencyStatusHistoryItem> MarketCurrencyStatusHistory { get; set; } = new BindingList<CurrencyStatusHistoryItem>();

        Image BidAskSnapshot { get; }
        Image OrderBookSnapshot { get; }

        protected SnapshotChartControl BidAskChart { get; private set; }
        protected SnapshotChartControl OrderBookChart { get; private set; }

        public virtual void MakeBidAskSnapshot() {
            BidAskChart.Render(BidAskSnapshot);
        }
        public virtual void MakeOrderBookSnapshot() {
            OrderBookChart.Render(OrderBookSnapshot);
        }
        public Color AskColor {
            get { return System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(192)))), ((int)(((byte)(192))))); }
        }

        public Color BidColor {
            get { return System.Drawing.Color.FromArgb(((int)(((byte)(192)))), ((int)(((byte)(255)))), ((int)(((byte)(192))))); }
        }
        Series CreateLineSeries(string str, Color color) {
            Series s = new Series();
            s.Name = str;
            s.ArgumentDataMember = "Time";
            s.ValueDataMembers.AddRange(str);
            s.ValueScaleType = ScaleType.Numerical;
            s.ShowInLegend = true;
            StepLineSeriesView view = new StepLineSeriesView();
            view.Color = color;
            view.LineStyle.Thickness = (int)(21 * DpiProvider.Default.DpiScaleFactor);
            s.View = view;
            s.DataSource = History;
            return s;
        }
        Series CreateStepAreaSeries(OrderBookEntry[] list, Color color) {
            Series s = new Series();
            s.Name = "Amount";
            s.ArgumentDataMember = "Value";
            s.ValueDataMembers.AddRange("Amount");
            s.ValueScaleType = ScaleType.Numerical;
            s.ShowInLegend = true;
            StepAreaSeriesView view = new StepAreaSeriesView();
            view.Color = color;
            s.View = view;
            s.DataSource = list;
            return s;
        }
        protected virtual SnapshotChartControl CreateChartSnapshotControl() {
            SnapshotChartControl chart = new SnapshotChartControl();
            //chart.Series.Add(CreateLineSeries("Bid", BidColor));
            //chart.Series.Add(CreateLineSeries("Ask", AskColor));
            chart.Size = BidAskSnapshotSize;
            chart.CreateControl();
            return chart;
        }
        protected virtual SnapshotChartControl CreateOrderBookChartControl() {
            SnapshotChartControl chart = new SnapshotChartControl();
            //chart.Series.Add(CreateStepAreaSeries(OrderBook.Bids, BidColor));
            //chart.Series.Add(CreateStepAreaSeries(OrderBook.Asks, AskColor));
            chart.Size = OrderBookSnapshotSize;
            chart.CreateControl();
            return chart;
        }
        protected virtual Size BidAskSnapshotSize { get { return new Size(600, 400); } }
        protected virtual Size OrderBookSnapshotSize { get { return new Size(600, 200); } }
        protected virtual Image CreateOrderBookSnapshotImage() {
            Size sz = OrderBookSnapshotSize;
            return new Bitmap(sz.Width, sz.Height);
        }
        protected virtual Image CreateChartSnapshotImage() {
            Size sz = BidAskSnapshotSize;
            return new Bitmap(sz.Width, sz.Height);
        }

        public OrderBook OrderBook { get; private set; }
        public abstract string Name { get; }
        decimal lowestAsk;
        public decimal LowestAsk {
            get { return lowestAsk; }
            set {
                if(value != LowestAsk)
                    AskChange = value - LowestAsk;
                lowestAsk = value;
            }
        }
        decimal highestBid;
        public decimal HighestBid {
            get { return highestBid; }
            set {
                if(value != HighestBid)
                    BidChange = value - HighestBid;
                highestBid = value;
            }
        }
        public bool IsFrozen { get; set; }
        public decimal Last { get; set; }
        public decimal BaseVolume { get; set; }
        public decimal Volume { get; set; }
        public decimal Hr24High { get; set; }
        public decimal Hr24Low { get; set; }
        public decimal Change { get; set; }
        public decimal Spread { get { return LowestAsk - HighestBid; } }
        public decimal BidChange { get; set; }
        public decimal AskChange { get; set; }
        public abstract decimal Fee { get; }

        public abstract decimal BaseCurrencyBalance { get; }
        public abstract decimal MarketCurrencyBalance { get; }
        public abstract decimal MarketCurrencyTotalBalance { get; }
        public abstract bool MarketCurrencyEnabled { get; }

        public string BaseCurrency { get; set; }
        public string MarketCurrency { get; set; }
        public abstract string HostName { get; }
        public DateTime Time { get; set; }
        public int CandleStickPeriodMin { get; set; } = 1;
        public DateTime LastTradeStatisticTime { get; set; }
        public long LastTradeId { get; set; }
        public abstract string WebPageAddress { get; }
        public abstract void UpdateOrderBook(int depth);
        public abstract void ProcessOrderBook(string text);
        public abstract void UpdateTicker();
        public abstract void UpdateTrades();
        public abstract string DownloadString(string address);

        TickerUpdateHelper updateHelper;
        protected TickerUpdateHelper UpdateHelper {
            get {
                if(updateHelper == null)
                    updateHelper = new TickerUpdateHelper(this);
                return updateHelper;
            }
        }

        RateLimiting.RateGate apiRate = new RateLimiting.RateGate(6, TimeSpan.FromSeconds(1));
        protected RateLimiting.RateGate ApiRate {
            get { return apiRate; }
        }

        protected internal void RaiseHistoryItemAdded() {
            if(HistoryItemAdd != null)
                HistoryItemAdd(this, EventArgs.Empty);
        }
        protected internal void RaiseChanged() {
            if(Changed != null)
                Changed(this, EventArgs.Empty);
        }

        protected internal void RaiseTradeHistoryAdd() {
            if(TradeHistoryAdd != null)
                TradeHistoryAdd(this, EventArgs.Empty);
        }

        public abstract bool UpdateArbitrageOrderBook(int depth);
        public abstract void ProcessArbitrageOrderBook(string text);

        public event EventHandler HistoryItemAdd;
        public event EventHandler TradeHistoryAdd;
        public event EventHandler Changed;

        public abstract bool UpdateBalance(CurrencyType type);
        public abstract string GetDepositAddress(CurrencyType type);
        public abstract bool Buy(decimal lowestAsk, decimal amount);
        public abstract bool Sell(decimal highestBid, decimal amount);
        public abstract bool Withdraw(string currency, string address, decimal amount);
        public abstract bool UpdateTradeStatistic();

        protected WebClient WebClient { get; } = new MyWebClient();

        public void UpdateHistoryForTradeItem(TradeHistoryItem item) {
            for(int i = History.Count - 1; i >= 0; i--) {
                TickerHistoryItem h = History[i];
                if(h.Time.Ticks <= item.Time.Ticks) {
                    item.Bid = h.Bid;
                    item.Ask = h.Ask;
                    item.Current = h.Current;
                    break;
                }
            }
        }
        public void UpdateHistoryItem() {
            TickerHistoryItem last = History.Count == 0 ? null : History.Last();
            if(History.Count > 72000) {
                for(int i = 0; i < 2000; i++)
                    History.RemoveAt(0);
            }
            if(last != null) {
                if(last.Ask == LowestAsk && last.Bid == HighestBid && last.Current == Last)
                    return;
                Change = ((Last - last.Current) / last.Current) * 100;
                if(last.Bid != HighestBid)
                    BidChange = (HighestBid - last.Bid) * 100;
                if(last.Ask != LowestAsk)
                    AskChange = LowestAsk - last.Ask;
            }
            History.Add(new TickerHistoryItem() { Time = Time, Ask = LowestAsk, Bid = HighestBid, Current = Last });
            RaiseHistoryItemAdded();
        }
        public void UpdateMarketCurrencyStatusHistory() {
            if(MarketCurrencyStatusHistory.Count == 0) {
                MarketCurrencyStatusHistory.Add(new CurrencyStatusHistoryItem() { Enabled = MarketCurrencyEnabled, Time = DateTime.Now });
                return;
            }
            if(MarketCurrencyStatusHistory.Last().Enabled == MarketCurrencyEnabled)
                return;
            MarketCurrencyStatusHistory.Add(new CurrencyStatusHistoryItem() { Enabled = MarketCurrencyEnabled, Time = DateTime.Now });
        }
    }

    public class SnapshotChartControl : ChartControl {
        public void Render(Image image) {
            using(Graphics g = Graphics.FromImage(image)) {
                PaintEventArgs e = new PaintEventArgs(g, new Rectangle(0, 0, Width, Height));
                OnPaint(e);
            }
        }
    }
}
