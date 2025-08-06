# DTTelegramReceiver

A modern, clean backtesting application for Telegram trading signals with MT5 integration.

## Features

✅ **Clean Modern UI** - GitHub-inspired dark theme, no clutter  
✅ **All Symbols Support** - Automatically processes all trading pairs from signals  
✅ **Telegram Integration** - Auto-connects to your existing sessions  
✅ **Real-time Charts** - Candlestick charts with trade entry/exit markers  
✅ **MT5 Data Integration** - Fetches real price data via MT5 Python API  
✅ **Python Parser Integration** - Uses your existing HybridParser.py  
✅ **MQL5 Export** - Generates complete Expert Advisors with hardcoded signals  
✅ **Comprehensive Stats** - Win rate, P&L, drawdown, trade analysis  

## Quick Start

1. **Connect Telegram**: Click "Connect" - it will auto-detect your existing sessions
2. **Select Date Range**: Choose the period you want to backtest
3. **Load Messages**: Fetches all messages from selected channels
4. **Run Backtest**: Processes signals and simulates trades
5. **Export MQL5**: Generate EA file for MT5 backtesting

## How It Works

### Signal Processing Flow
```
Telegram Messages → Python Parser → Signal Extraction → Backtesting → Results
```

### Supported Signal Formats
- **Entry Signals**: BUY/SELL with entry prices, SL, TP
- **Management**: CLOSE, MODIFY, BREAKEVEN commands  
- **All Symbols**: XAUUSD, EURUSD, GBPUSD, BTCUSD, etc.
- **Multiple Channels**: Processes all selected channels simultaneously

### Chart Features
- **Candlestick Charts**: Real MT5 price data
- **Trade Markers**: Green triangles (BUY), Red triangles (SELL)
- **Entry/Exit Points**: Visual representation of all trades
- **Multiple Timeframes**: M1, M5, M15, M30, H1, H4, D1

## Requirements

### Software
- **.NET 8.0** (included with app)
- **Python 3.9+** with MetaTrader5 package
- **MT5 Terminal** (for real data, optional)

### Python Dependencies
```bash
pip install MetaTrader5 pandas
```

## Configuration

### MT5 Setup (Optional)
If you want real price data instead of simulated data:

1. Install MT5 terminal
2. Enable "Allow automated trading"
3. Install Python MT5 package: `pip install MetaTrader5`

### Telegram Setup
The app auto-detects existing Telegram sessions from your previous DaneTrades apps.

## Usage Guide

### 1. Loading Messages
- **Date Range**: Select start and end dates
- **Channels**: Choose specific channels or "All Channels"
- **Symbols**: App automatically processes all symbols found in signals

### 2. Running Backtests
- **Risk Management**: 1% risk per trade (configurable)
- **Spread**: 2 pips default spread applied
- **Stop Loss/Take Profit**: Uses signal values when available
- **Max Open Trades**: 5 concurrent positions

### 3. Analyzing Results
- **Win Rate**: Percentage of profitable trades
- **Total P&L**: Net profit/loss in dollars
- **Max Drawdown**: Largest peak-to-trough decline
- **Trade List**: Individual trade details

### 4. MQL5 Export
Generated EA includes:
- **Hardcoded Signals**: All signals with timestamps
- **Risk Management**: Configurable lot sizes, SL/TP
- **Time-based Execution**: Signals execute at original timestamps
- **Multi-symbol Support**: Handles all trading pairs

## File Structure

```
DTTelegramReceiver/
├── MainWindow.axaml          # Clean UI layout
├── MainWindow.axaml.cs       # Main application logic
├── BacktestEngine.cs         # Core backtesting logic
├── BacktestModels.cs         # Data models
├── MT5DataProvider.cs        # Price data integration
├── MQL5Exporter.cs          # EA generation
└── SignalProcessor.cs        # Signal parsing (C# version)
```

## Integration with Your Existing Tools

### Python Parser Integration
The app can call your existing `HybridParser.py`:
```csharp
// In ParseSignalsWithPython() method
var process = Process.Start("python", "HybridParser.py message.txt");
```

### Telegram Receiver Integration
Uses the same session files as your existing DaneTrades Telegram Receiver.

## Troubleshooting

### Common Issues

**"No MT5 data"**: App falls back to simulated data automatically  
**"Telegram connection failed"**: Check existing session files in AppData  
**"No signals found"**: Verify message date range and signal format  
**"Chart not loading"**: Ensure symbol data is available  

### Performance Tips
- **Date Range**: Limit to 30-90 days for faster processing
- **Channels**: Select specific channels instead of "All Channels"
- **Symbols**: App automatically filters relevant symbols

## Advanced Features

### Custom Backtesting Parameters
```csharp
var settings = new BacktestSettings
{
    InitialBalance = 10000,
    RiskPerTrade = 1.0,      // 1% risk
    Spread = 2.0,            // 2 pips
    MaxOpenTrades = 5
};
```

### MQL5 Customization
Generated EAs include:
- Configurable lot sizes
- Magic number support
- Slippage control
- Custom signal execution logic

## Support

- **Documentation**: This README
- **Help**: Click "Help" button in app
- **More Products**: Click "More Products" for additional tools

## Version History

**v3.0** - Complete rewrite with clean UI and modern architecture  
**v2.70** - Legacy version with complex UI  
**v2.40** - Original Telegram receiver  

---

**Author**: Mamdouh Soliman (mamdouh.a.soliman@gmail.com)