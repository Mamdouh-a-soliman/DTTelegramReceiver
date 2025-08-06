using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace DTTelegramReceiver
{
    public class MT5DataProvider
    {
        private readonly string _pythonPath;
        private readonly string _scriptPath;

        public MT5DataProvider()
        {
            // Try to find Python installation
            _pythonPath = FindPythonPath();
            _scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mt5_data_fetcher.py");
            
            // Create the Python script if it doesn't exist
            CreateMT5DataScript();
        }

        private string FindPythonPath()
        {
            var possiblePaths = new[]
            {
                "python",
                "python3",
                @"C:\Python39\python.exe",
                @"C:\Python310\python.exe",
                @"C:\Python311\python.exe",
                @"C:\Users\" + Environment.UserName + @"\AppData\Local\Programs\Python\Python39\python.exe",
                @"C:\Users\" + Environment.UserName + @"\AppData\Local\Programs\Python\Python310\python.exe",
                @"C:\Users\" + Environment.UserName + @"\AppData\Local\Programs\Python\Python311\python.exe"
            };

            foreach (var path in possiblePaths)
            {
                try
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = path,
                            Arguments = "--version",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true
                        }
                    };
                    process.Start();
                    process.WaitForExit();
                    if (process.ExitCode == 0)
                    {
                        return path;
                    }
                }
                catch
                {
                    // Continue to next path
                }
            }

            return "python"; // Default fallback
        }

        private void CreateMT5DataScript()
        {
            var script = @"
import sys
import json
from datetime import datetime, timedelta

try:
    import MetaTrader5 as mt5
    MT5_AVAILABLE = True
except ImportError:
    MT5_AVAILABLE = False

def get_mt5_data(symbol, timeframe, start_date, end_date):
    if not MT5_AVAILABLE:
        return {'error': 'MetaTrader5 package not installed. Install with: pip install MetaTrader5'}
    
    # Initialize MT5 connection
    if not mt5.initialize():
        return {'error': 'Failed to initialize MT5. Make sure MT5 is running and allows automated trading.'}
    
    try:
        # Convert timeframe string to MT5 constant
        timeframe_map = {
            'M1': mt5.TIMEFRAME_M1,
            'M5': mt5.TIMEFRAME_M5,
            'M15': mt5.TIMEFRAME_M15,
            'M30': mt5.TIMEFRAME_M30,
            'H1': mt5.TIMEFRAME_H1,
            'H4': mt5.TIMEFRAME_H4,
            'D1': mt5.TIMEFRAME_D1
        }
        
        tf = timeframe_map.get(timeframe, mt5.TIMEFRAME_H1)
        
        # Check if symbol exists
        symbol_info = mt5.symbol_info(symbol)
        if symbol_info is None:
            return {'error': f'Symbol {symbol} not found in MT5'}
        
        # Select the symbol in Market Watch
        if not mt5.symbol_select(symbol, True):
            return {'error': f'Failed to select symbol {symbol}'}
        
        # Get rates
        rates = mt5.copy_rates_range(symbol, tf, start_date, end_date)
        
        if rates is None or len(rates) == 0:
            return {'error': f'No data found for {symbol} in the specified date range'}
        
        # Convert to list of dictionaries
        data = []
        for rate in rates:
            data.append({
                'time': datetime.fromtimestamp(rate['time']).isoformat(),
                'open': float(rate['open']),
                'high': float(rate['high']),
                'low': float(rate['low']),
                'close': float(rate['close']),
                'volume': int(rate['tick_volume'])
            })
        
        return {'data': data, 'count': len(data)}
        
    except Exception as e:
        return {'error': f'MT5 error: {str(e)}'}
    finally:
        mt5.shutdown()

if __name__ == '__main__':
    if len(sys.argv) != 5:
        print(json.dumps({'error': 'Usage: python script.py <symbol> <timeframe> <start_date> <end_date>'}))
        sys.exit(1)
    
    try:
        symbol = sys.argv[1]
        timeframe = sys.argv[2]
        start_date = datetime.fromisoformat(sys.argv[3])
        end_date = datetime.fromisoformat(sys.argv[4])
        
        result = get_mt5_data(symbol, timeframe, start_date, end_date)
        print(json.dumps(result))
    except Exception as e:
        print(json.dumps({'error': f'Script error: {str(e)}'}))
";

            File.WriteAllText(_scriptPath, script);
        }

        public async Task<List<CandleData>> GetCandleDataAsync(string symbol, DateTime startDate, DateTime endDate, string timeframe = "H1")
        {
            try
            {
                Console.WriteLine($"Fetching MT5 data for {symbol} from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
                
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _pythonPath,
                        Arguments = $"\"{_scriptPath}\" {symbol} {timeframe} {startDate:yyyy-MM-ddTHH:mm:ss} {endDate:yyyy-MM-ddTHH:mm:ss}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                Console.WriteLine($"Python output: {output}");
                if (!string.IsNullOrEmpty(error))
                {
                    Console.WriteLine($"Python error: {error}");
                }

                if (!string.IsNullOrEmpty(error) && !error.Contains("warning"))
                {
                    Console.WriteLine($"Using dummy data for {symbol} due to error: {error}");
                    return GenerateDummyData(symbol, startDate, endDate);
                }

                if (string.IsNullOrWhiteSpace(output))
                {
                    Console.WriteLine($"Empty output from Python script for {symbol}, using dummy data");
                    return GenerateDummyData(symbol, startDate, endDate);
                }

                var result = JsonSerializer.Deserialize<JsonElement>(output);
                
                if (result.TryGetProperty("error", out var errorProp))
                {
                    Console.WriteLine($"MT5 error for {symbol}: {errorProp.GetString()}, using dummy data");
                    return GenerateDummyData(symbol, startDate, endDate);
                }

                if (result.TryGetProperty("data", out var dataProp))
                {
                    var candles = new List<CandleData>();
                    
                    foreach (var item in dataProp.EnumerateArray())
                    {
                        candles.Add(new CandleData
                        {
                            Time = DateTime.Parse(item.GetProperty("time").GetString()!),
                            Open = item.GetProperty("open").GetDouble(),
                            High = item.GetProperty("high").GetDouble(),
                            Low = item.GetProperty("low").GetDouble(),
                            Close = item.GetProperty("close").GetDouble(),
                            Volume = item.GetProperty("volume").GetDouble()
                        });
                    }
                    
                    Console.WriteLine($"Successfully loaded {candles.Count} candles for {symbol}");
                    return candles.Any() ? candles : GenerateDummyData(symbol, startDate, endDate);
                }

                Console.WriteLine($"No data property found for {symbol}, using dummy data");
                return GenerateDummyData(symbol, startDate, endDate);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception fetching data for {symbol}: {ex.Message}, using dummy data");
                return GenerateDummyData(symbol, startDate, endDate);
            }
        }

        private List<CandleData> GenerateDummyData(string symbol, DateTime startDate, DateTime endDate)
        {
            var candles = new List<CandleData>();
            var random = new Random();
            var current = startDate;
            var basePrice = GetBasePrice(symbol);

            while (current <= endDate)
            {
                var open = basePrice + (random.NextDouble() - 0.5) * basePrice * 0.02;
                var change = (random.NextDouble() - 0.5) * basePrice * 0.01;
                var high = Math.Max(open, open + change) + random.NextDouble() * basePrice * 0.005;
                var low = Math.Min(open, open + change) - random.NextDouble() * basePrice * 0.005;
                var close = open + change;

                candles.Add(new CandleData
                {
                    Time = current,
                    Open = open,
                    High = high,
                    Low = low,
                    Close = close,
                    Volume = random.Next(100, 1000)
                });

                basePrice = close;
                current = current.AddHours(1);
            }

            return candles;
        }

        private double GetBasePrice(string symbol)
        {
            return symbol.ToUpper() switch
            {
                "XAUUSD" => 2000.0,
                "EURUSD" => 1.1000,
                "GBPUSD" => 1.2500,
                "USDJPY" => 150.0,
                "BTCUSD" => 45000.0,
                _ => 1.0
            };
        }

        public async Task<bool> TestMT5ConnectionAsync()
        {
            try
            {
                var testScript = @"
import MetaTrader5 as mt5
import json

if mt5.initialize():
    info = mt5.terminal_info()
    mt5.shutdown()
    print(json.dumps({'connected': True, 'terminal': info.name if info else 'Unknown'}))
else:
    print(json.dumps({'connected': False, 'error': 'Failed to connect'}))
";

                var tempScript = Path.GetTempFileName() + ".py";
                File.WriteAllText(tempScript, testScript);

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _pythonPath,
                        Arguments = $"\"{tempScript}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                File.Delete(tempScript);

                var result = JsonSerializer.Deserialize<JsonElement>(output);
                return result.TryGetProperty("connected", out var connected) && connected.GetBoolean();
            }
            catch
            {
                return false;
            }
        }
    }
}