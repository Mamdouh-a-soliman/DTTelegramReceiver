using System;
using System.Collections.Generic;
using System.Linq;

namespace DTTelegramReceiver
{
    public class TelegramMessage
    {
        public long Id { get; set; }
        public DateTime Date { get; set; }
        public string Text { get; set; } = string.Empty;
        public string ChannelName { get; set; } = string.Empty;
        public long ChannelId { get; set; }
    }

    public class ParsedSignal
    {
        public string Action { get; set; } = string.Empty; // BUY, SELL, CLOSE, etc.
        public string Pair { get; set; } = string.Empty;   // XAUUSD, EURUSD, etc.
        public string Entry { get; set; } = "0";
        public string StopLoss { get; set; } = "0";
        public Dictionary<string, string> TakeProfit { get; set; } = new();
        public List<string> Actions { get; set; } = new();
        public DateTime Timestamp { get; set; }
        public string OriginalMessage { get; set; } = string.Empty;
        public string ChannelName { get; set; } = string.Empty;
    }

    public class Trade
    {
        public int Id { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty; // BUY or SELL
        public double EntryPrice { get; set; }
        public double ExitPrice { get; set; }
        public double StopLoss { get; set; }
        public double TakeProfit { get; set; }
        public DateTime EntryTime { get; set; }
        public DateTime ExitTime { get; set; }
        public double PnL { get; set; }
        public string ExitReason { get; set; } = string.Empty; // TP, SL, Manual
        public bool IsWin => PnL > 0;
        public string ChannelName { get; set; } = string.Empty;
        public ParsedSignal OriginalSignal { get; set; } = new();
    }

    public class BacktestResult
    {
        public List<Trade> Trades { get; set; } = new();
        public int TotalTrades => Trades.Count;
        public int WinningTrades => Trades.Count(t => t.IsWin);
        public int LosingTrades => Trades.Count(t => !t.IsWin);
        public double WinRate => TotalTrades > 0 ? (double)WinningTrades / TotalTrades * 100 : 0;
        public double TotalPnL => Trades.Sum(t => t.PnL);
        public double MaxDrawdown { get; set; }
        public double LargestWin => Trades.Any() ? Trades.Max(t => t.PnL) : 0;
        public double LargestLoss => Trades.Any() ? Trades.Min(t => t.PnL) : 0;
        public double AveragePnL => TotalTrades > 0 ? TotalPnL / TotalTrades : 0;
        public TimeSpan TotalDuration { get; set; }
    }

    public class CandleData
    {
        public DateTime Time { get; set; }
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public double Volume { get; set; }
    }

    public class BacktestSettings
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Symbol { get; set; } = "All Symbols";
        public string Channel { get; set; } = "All Channels";
        public double InitialBalance { get; set; } = 10000;
        public double RiskPerTrade { get; set; } = 1.0; // 1% risk per trade
        public double Spread { get; set; } = 2.0; // 2 pips spread
        public double Commission { get; set; } = 0.0; // Commission per lot
        public bool UseStopLoss { get; set; } = true;
        public bool UseTakeProfit { get; set; } = true;
        public int MaxOpenTrades { get; set; } = 5;
    }
}