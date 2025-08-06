using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DTTelegramReceiver
{
    public class BacktestEngine
    {
        private readonly MT5DataProvider _dataProvider;
        private List<CandleData> _candleData = new();
        private List<ParsedSignal> _signals = new();
        private BacktestSettings _settings = new();

        public BacktestEngine()
        {
            _dataProvider = new MT5DataProvider();
        }

        public async Task<BacktestResult> RunBacktestAsync(
            List<ParsedSignal> signals, 
            BacktestSettings settings)
        {
            _signals = signals.OrderBy(s => s.Timestamp).ToList();
            _settings = settings;

            var result = new BacktestResult();
            var openTrades = new List<Trade>();
            var completedTrades = new List<Trade>();
            var balance = settings.InitialBalance;
            var tradeId = 1;

            // Get unique symbols from signals
            var symbols = _signals
                .Where(s => !string.IsNullOrEmpty(s.Pair) && s.Pair != "0")
                .Select(s => s.Pair)
                .Distinct()
                .ToList();

            // Load price data for all symbols
            var priceData = new Dictionary<string, List<CandleData>>();
            foreach (var symbol in symbols)
            {
                try
                {
                    var candles = await _dataProvider.GetCandleDataAsync(
                        symbol, settings.StartDate, settings.EndDate);
                    priceData[symbol] = candles;
                }
                catch (Exception ex)
                {
                    // Log error and continue with other symbols
                    Console.WriteLine($"Failed to load data for {symbol}: {ex.Message}");
                }
            }

            // Process each signal
            foreach (var signal in _signals)
            {
                if (signal.Timestamp < settings.StartDate || signal.Timestamp > settings.EndDate)
                    continue;

                // Filter by symbol if specified
                if (settings.Symbol != "All Symbols" && signal.Pair != settings.Symbol)
                    continue;

                // Filter by channel if specified
                if (settings.Channel != "All Channels" && signal.ChannelName != settings.Channel)
                    continue;

                // Get current price at signal time
                if (!priceData.ContainsKey(signal.Pair))
                    continue;

                var currentPrice = GetPriceAtTime(priceData[signal.Pair], signal.Timestamp);
                if (currentPrice == null)
                    continue;

                // Process signal based on action
                switch (signal.Action?.ToUpper())
                {
                    case "BUY":
                    case "SELL":
                        if (openTrades.Count < settings.MaxOpenTrades)
                        {
                            var trade = CreateTradeFromSignal(signal, currentPrice, tradeId++);
                            if (trade != null)
                            {
                                openTrades.Add(trade);
                            }
                        }
                        break;

                    case "CLOSE":
                        // Close all trades for this symbol
                        var tradesToClose = openTrades
                            .Where(t => t.Symbol == signal.Pair)
                            .ToList();

                        foreach (var trade in tradesToClose)
                        {
                            CloseTrade(trade, currentPrice.Close, signal.Timestamp, "Manual Close");
                            completedTrades.Add(trade);
                            openTrades.Remove(trade);
                        }
                        break;
                }

                // Check for TP/SL hits on open trades
                CheckTradeExits(openTrades, completedTrades, priceData, signal.Timestamp);
            }

            // Close any remaining open trades at the end
            foreach (var trade in openTrades.ToList())
            {
                if (priceData.ContainsKey(trade.Symbol))
                {
                    var finalPrice = priceData[trade.Symbol].LastOrDefault();
                    if (finalPrice != null)
                    {
                        CloseTrade(trade, finalPrice.Close, settings.EndDate, "End of Period");
                        completedTrades.Add(trade);
                    }
                }
            }

            // Calculate results
            result.Trades = completedTrades;
            result.MaxDrawdown = CalculateMaxDrawdown(completedTrades);
            result.TotalDuration = settings.EndDate - settings.StartDate;

            return result;
        }

        private CandleData? GetPriceAtTime(List<CandleData> candles, DateTime time)
        {
            return candles
                .Where(c => c.Time <= time)
                .OrderByDescending(c => c.Time)
                .FirstOrDefault();
        }

        private Trade? CreateTradeFromSignal(ParsedSignal signal, CandleData currentPrice, int tradeId)
        {
            if (!double.TryParse(signal.Entry, out var entryPrice))
            {
                // Use current price if entry is not specified or invalid
                entryPrice = signal.Action?.ToUpper() == "BUY" ? currentPrice.Close : currentPrice.Close;
            }

            var trade = new Trade
            {
                Id = tradeId,
                Symbol = signal.Pair,
                Action = signal.Action?.ToUpper() ?? "",
                EntryPrice = entryPrice,
                EntryTime = signal.Timestamp,
                ChannelName = signal.ChannelName,
                OriginalSignal = signal
            };

            // Set stop loss
            if (_settings.UseStopLoss && double.TryParse(signal.StopLoss, out var sl) && sl > 0)
            {
                trade.StopLoss = sl;
            }

            // Set take profit (use first TP if multiple)
            if (_settings.UseTakeProfit && signal.TakeProfit.Any())
            {
                var firstTp = signal.TakeProfit.Values.FirstOrDefault();
                if (double.TryParse(firstTp, out var tp) && tp > 0)
                {
                    trade.TakeProfit = tp;
                }
            }

            return trade;
        }

        private void CheckTradeExits(
            List<Trade> openTrades, 
            List<Trade> completedTrades, 
            Dictionary<string, List<CandleData>> priceData, 
            DateTime currentTime)
        {
            var tradesToClose = new List<Trade>();

            foreach (var trade in openTrades)
            {
                if (!priceData.ContainsKey(trade.Symbol))
                    continue;

                var currentCandle = GetPriceAtTime(priceData[trade.Symbol], currentTime);
                if (currentCandle == null)
                    continue;

                var isBuy = trade.Action == "BUY";

                // Check Stop Loss
                if (trade.StopLoss > 0)
                {
                    var slHit = isBuy ? currentCandle.Low <= trade.StopLoss : currentCandle.High >= trade.StopLoss;
                    if (slHit)
                    {
                        CloseTrade(trade, trade.StopLoss, currentTime, "Stop Loss");
                        tradesToClose.Add(trade);
                        continue;
                    }
                }

                // Check Take Profit
                if (trade.TakeProfit > 0)
                {
                    var tpHit = isBuy ? currentCandle.High >= trade.TakeProfit : currentCandle.Low <= trade.TakeProfit;
                    if (tpHit)
                    {
                        CloseTrade(trade, trade.TakeProfit, currentTime, "Take Profit");
                        tradesToClose.Add(trade);
                        continue;
                    }
                }
            }

            // Move closed trades
            foreach (var trade in tradesToClose)
            {
                openTrades.Remove(trade);
                completedTrades.Add(trade);
            }
        }

        private void CloseTrade(Trade trade, double exitPrice, DateTime exitTime, string reason)
        {
            trade.ExitPrice = exitPrice;
            trade.ExitTime = exitTime;
            trade.ExitReason = reason;

            // Calculate P&L (simplified - not accounting for lot size, just pips)
            var isBuy = trade.Action == "BUY";
            var priceDiff = isBuy ? exitPrice - trade.EntryPrice : trade.EntryPrice - exitPrice;
            
            // Apply spread
            priceDiff -= _settings.Spread * GetPipValue(trade.Symbol);
            
            // Simple P&L calculation (would need proper lot size and pip value in real implementation)
            trade.PnL = priceDiff * 100; // Simplified multiplier
        }

        private double GetPipValue(string symbol)
        {
            return symbol.ToUpper() switch
            {
                "XAUUSD" => 0.01,
                "EURUSD" => 0.0001,
                "GBPUSD" => 0.0001,
                "USDJPY" => 0.01,
                "BTCUSD" => 1.0,
                _ => 0.0001
            };
        }

        private double CalculateMaxDrawdown(List<Trade> trades)
        {
            if (!trades.Any()) return 0;

            var runningPnL = 0.0;
            var peak = 0.0;
            var maxDrawdown = 0.0;

            foreach (var trade in trades.OrderBy(t => t.ExitTime))
            {
                runningPnL += trade.PnL;
                if (runningPnL > peak)
                {
                    peak = runningPnL;
                }
                else
                {
                    var drawdown = (peak - runningPnL) / Math.Max(peak, 1) * 100;
                    maxDrawdown = Math.Max(maxDrawdown, drawdown);
                }
            }

            return maxDrawdown;
        }
    }
}