using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using WTelegram;
using TL;

namespace DTTelegramReceiver
{
    public partial class MainWindow : Window
    {
        // Core components
        private readonly BacktestEngine _backtestEngine;
        private readonly MT5DataProvider _dataProvider;
        private readonly MQL5Exporter _mql5Exporter;
        private readonly SignalProcessor _signalProcessor;

        // Data
        private List<TelegramMessage> _messages = new();
        private List<ParsedSignal> _parsedSignals = new();
        private BacktestResult? _lastBacktestResult;
        private Dictionary<long, string> _channels = new();
        private Dictionary<long, long> _channelAccessHashes = new();

        // Telegram client
        private Client? _telegramClient;
        private readonly string _apiId = "28397482";
        private readonly string _apiHash = "03b482c75d04b0caf909b180cae07fe6";
        private bool _isConnected = false;
        private string? _phoneNumber;
        private string? _verificationCode;
        private string? _password;

        // Config directory - use current directory for guaranteed access
        private string _configDirectory = Path.Combine(Directory.GetCurrentDirectory(), "telegram_config");

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                
                // Initialize components
                _backtestEngine = new BacktestEngine();
                _dataProvider = new MT5DataProvider();
                _mql5Exporter = new MQL5Exporter();
                _signalProcessor = new SignalProcessor();

                // Ensure config directory exists - use simple current directory
                try
                {
                    Directory.CreateDirectory(_configDirectory);
                    tb_statusMessage.Text = $"Config: {_configDirectory}";
                }
                catch (Exception ex)
                {
                    tb_statusMessage.Text = $"Config error: {ex.Message}";
                }

                // Setup event handlers
                SetupEventHandlers();
                
                // Initialize UI
                InitializeUI();
                
                // Try to auto-connect to Telegram
                _ = Task.Run(AttemptTelegramAutoConnect);
                
                // Set status
                tb_statusMessage.Text = "Ready - Select date range and click Load Messages";
            }
            catch (Exception ex)
            {
                // Log error to desktop for debugging
                var errorFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "backtester_error.txt");
                File.WriteAllText(errorFile, $"Error: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}\n\nConfig Directory: {_configDirectory}");
                throw;
            }
        }

        private void SetupEventHandlers()
        {
            try
            {
                bt_connectTelegram.Click += OnConnectTelegramClick;
                bt_loadMessages.Click += OnLoadMessagesClick;
                bt_runBacktest.Click += OnRunBacktestClick;
                bt_exportMQL5.Click += OnExportMQL5Click;
                bt_help.Click += OnHelpClick;
                bt_more.Click += OnMoreClick;
                
                // Login dialog handlers
                bt_submitPhone.Click += OnSubmitPhoneClick;
                bt_submitCode.Click += OnSubmitCodeClick;
                bt_submitPassword.Click += OnSubmitPasswordClick;
                bt_closeLogin.Click += OnCloseLoginClick;
            }
            catch (Exception ex)
            {
                tb_statusMessage.Text = $"Error setting up handlers: {ex.Message}";
            }
        }

        private void InitializeUI()
        {
            try
            {
                // Set default date range (last 30 days)
                dp_fromDate.SelectedDate = DateTime.Now.AddDays(-30);
                dp_toDate.SelectedDate = DateTime.Now;

                // Hide results initially
                sp_stats.IsVisible = false;
                sp_resultsPlaceholder.IsVisible = true;
                sp_chartPlaceholder.IsVisible = true;
                sp_messagesPlaceholder.IsVisible = true;

                tb_statusMessage.Text = "Ready - Select date range and click Load Messages";
            }
            catch (Exception ex)
            {
                tb_statusMessage.Text = $"Error initializing UI: {ex.Message}";
            }
        }

        private async void OnConnectTelegramClick(object? sender, RoutedEventArgs e)
        {
            if (_isConnected)
            {
                // Disconnect
                DisconnectTelegram();
            }
            else
            {
                // Try auto-connect first, then show login dialog if needed
                var autoConnected = await AttemptTelegramAutoConnect();
                if (!autoConnected)
                {
                    ShowLoginDialog();
                }
            }
        }

        private void ShowLoginDialog()
        {
            loginOverlay.IsVisible = true;
            phoneStep.IsVisible = true;
            codeStep.IsVisible = false;
            passwordStep.IsVisible = false;
            tb_loginStatus.Text = "Enter your phone number to connect";
            tb_phoneNumber.Focus();
        }

        private void OnCloseLoginClick(object? sender, RoutedEventArgs e)
        {
            loginOverlay.IsVisible = false;
            tb_phoneNumber.Text = "";
            tb_verificationCode.Text = "";
            tb_password.Text = "";
        }

        private async void OnSubmitPhoneClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                var phone = tb_phoneNumber.Text?.Trim();
                if (string.IsNullOrEmpty(phone) || !phone.StartsWith("+"))
                {
                    tb_loginStatus.Text = "Please enter a valid phone number starting with +";
                    return;
                }

                bt_submitPhone.IsEnabled = false;
                tb_loginStatus.Text = "Sending verification code...";

                _phoneNumber = phone;
                _telegramClient = new Client(Config);
                
                var result = await _telegramClient.Login(_phoneNumber);
                
                if (result == "verification_code")
                {
                    phoneStep.IsVisible = false;
                    codeStep.IsVisible = true;
                    tb_loginStatus.Text = "Verification code sent! Check your Telegram app.";
                    tb_verificationCode.Focus();
                }
                else if (result == "password")
                {
                    phoneStep.IsVisible = false;
                    passwordStep.IsVisible = true;
                    tb_loginStatus.Text = "2FA password required";
                    tb_password.Focus();
                }
                else if (result == null)
                {
                    // Already logged in
                    await CompleteLogin();
                }
            }
            catch (Exception ex)
            {
                tb_loginStatus.Text = $"Error: {ex.Message}";
                bt_submitPhone.IsEnabled = true;
            }
        }

        private async void OnSubmitCodeClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                var code = tb_verificationCode.Text?.Trim();
                if (string.IsNullOrEmpty(code))
                {
                    tb_loginStatus.Text = "Please enter the verification code";
                    return;
                }

                bt_submitCode.IsEnabled = false;
                tb_loginStatus.Text = "Verifying code...";

                _verificationCode = code;
                var result = await _telegramClient.Login(_verificationCode);
                
                if (result == "password")
                {
                    codeStep.IsVisible = false;
                    passwordStep.IsVisible = true;
                    tb_loginStatus.Text = "2FA password required";
                    tb_password.Focus();
                }
                else if (result == null)
                {
                    await CompleteLogin();
                }
            }
            catch (Exception ex)
            {
                tb_loginStatus.Text = $"Invalid code: {ex.Message}";
                bt_submitCode.IsEnabled = true;
            }
        }

        private async void OnSubmitPasswordClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                var password = tb_password.Text?.Trim();
                if (string.IsNullOrEmpty(password))
                {
                    tb_loginStatus.Text = "Please enter your 2FA password";
                    return;
                }

                bt_submitPassword.IsEnabled = false;
                tb_loginStatus.Text = "Logging in...";

                _password = password;
                var result = await _telegramClient.Login(_password);
                
                if (result == null)
                {
                    await CompleteLogin();
                }
            }
            catch (Exception ex)
            {
                tb_loginStatus.Text = $"Invalid password: {ex.Message}";
                bt_submitPassword.IsEnabled = true;
            }
        }

        private async Task CompleteLogin()
        {
            try
            {
                var user = await _telegramClient.LoginUserIfNeeded();
                if (user != null)
                {
                    _isConnected = true;
                    loginOverlay.IsVisible = false;
                    
                    tb_telegramStatus.Text = $"Telegram: Connected ({user.username ?? user.phone})";
                    tb_telegramStatus.Foreground = Avalonia.Media.Brushes.LightGreen;
                    bt_connectTelegram.Content = "Disconnect";
                    tb_statusMessage.Text = "Connected to Telegram! Loading channels...";

                    await LoadChannels();
                }
            }
            catch (Exception ex)
            {
                tb_loginStatus.Text = $"Login failed: {ex.Message}";
            }
        }

        private async Task<bool> AttemptTelegramAutoConnect()
        {
            try
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    tb_telegramStatus.Text = "Telegram: Connecting...";
                    tb_telegramStatus.Foreground = Avalonia.Media.Brushes.Orange;
                    bt_connectTelegram.IsEnabled = false;
                    tb_statusMessage.Text = "Connecting to Telegram...";
                });

                // Look for existing session files
                var sessionFiles = Directory.GetFiles(_configDirectory, "*.session");
                
                if (!sessionFiles.Any())
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        tb_telegramStatus.Text = "Telegram: No sessions found";
                        tb_telegramStatus.Foreground = Avalonia.Media.Brushes.Red;
                        bt_connectTelegram.Content = "Connect";
                        bt_connectTelegram.IsEnabled = true;
                        tb_statusMessage.Text = "No Telegram sessions found. Click Connect to login.";
                    });
                    return false;
                }

                foreach (var sessionFile in sessionFiles)
                {
                    try
                    {
                        var sessionName = Path.GetFileNameWithoutExtension(sessionFile);
                        _telegramClient = new Client(Config);
                        
                        var user = await _telegramClient.LoginUserIfNeeded();
                        
                        if (user != null)
                        {
                            _isConnected = true;
                            
                            await Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                tb_telegramStatus.Text = $"Telegram: Connected ({user.username ?? user.phone})";
                                tb_telegramStatus.Foreground = Avalonia.Media.Brushes.LightGreen;
                                bt_connectTelegram.Content = "Disconnect";
                                bt_connectTelegram.IsEnabled = true;
                                tb_statusMessage.Text = "Connected to Telegram! Loading channels...";
                            });

                            await LoadChannels();
                            return true;
                        }
                    }
                    catch
                    {
                        // Try next session file
                        continue;
                    }
                }

                // No valid session found
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    tb_telegramStatus.Text = "Telegram: Connection failed";
                    tb_telegramStatus.Foreground = Avalonia.Media.Brushes.Red;
                    bt_connectTelegram.Content = "Connect";
                    bt_connectTelegram.IsEnabled = true;
                    tb_statusMessage.Text = "Failed to connect. Click Connect to login manually.";
                });
                return false;
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    tb_telegramStatus.Text = $"Telegram: Error";
                    tb_telegramStatus.Foreground = Avalonia.Media.Brushes.Red;
                    bt_connectTelegram.Content = "Connect";
                    bt_connectTelegram.IsEnabled = true;
                    tb_statusMessage.Text = $"Connection error: {ex.Message}";
                });
                return false;
            }
        }

        private string Config(string what)
        {
            switch (what)
            {
                case "api_id": return _apiId;
                case "api_hash": return _apiHash;
                case "phone_number": return _phoneNumber;
                case "verification_code": return _verificationCode;
                case "password": return _password;
                case "session_pathname": return Path.Combine(_configDirectory, "telegram_session.session");
                default: return null;
            }
        }

        private void DisconnectTelegram()
        {
            try
            {
                _telegramClient?.Dispose();
                _telegramClient = null;
                _isConnected = false;
                _channels.Clear();
                _channelAccessHashes.Clear();

                tb_telegramStatus.Text = "Telegram: Disconnected";
                tb_telegramStatus.Foreground = Avalonia.Media.Brushes.Red;
                bt_connectTelegram.Content = "Connect";
                tb_statusMessage.Text = "Disconnected from Telegram";

                // Clear channels
                cb_channel.Items.Clear();
                cb_channel.Items.Add(new ComboBoxItem { Content = "All Channels", IsSelected = true });
            }
            catch (Exception ex)
            {
                tb_statusMessage.Text = $"Disconnect error: {ex.Message}";
            }
        }

        private async Task LoadChannels()
        {
            if (_telegramClient == null) return;

            try
            {
                tb_statusMessage.Text = "Loading your channels...";
                
                // Get real channels from Telegram
                var dialogs = await _telegramClient.Messages_GetAllDialogs();
                _channels.Clear();

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    cb_channel.Items.Clear();
                    cb_channel.Items.Add(new ComboBoxItem { Content = "All Channels", IsSelected = true });
                });

                foreach (var (id, chat) in dialogs.chats)
                {
                    // Include both broadcast channels and supergroups
                    if (chat is Channel channel)
                    {
                        var channelType = "";
                        if ((channel.flags & Channel.Flags.broadcast) != 0)
                            channelType = " [Channel]";
                        else if ((channel.flags & Channel.Flags.megagroup) != 0)
                            channelType = " [Group]";
                        else
                            continue; // Skip other types
                            
                        var displayName = $"{channel.title}{channelType}";
                        _channels[id] = channel.title;
                        _channelAccessHashes[id] = channel.access_hash;
                        
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            cb_channel.Items.Add(new ComboBoxItem { Content = displayName });
                        });
                    }
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    tb_statusMessage.Text = $"Loaded {_channels.Count} channels. Ready to load messages!";
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    tb_statusMessage.Text = $"Error loading channels: {ex.Message}";
                });
            }
        }

        private async void OnLoadMessagesClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                bt_loadMessages.IsEnabled = false;
                tb_statusMessage.Text = "Loading messages...";

                var fromDate = dp_fromDate.SelectedDate?.DateTime ?? DateTime.Now.AddDays(-30);
                var toDate = dp_toDate.SelectedDate?.DateTime ?? DateTime.Now;

                _messages.Clear();
                _parsedSignals.Clear();

                // Try to load real messages first
                await LoadRealMessages(fromDate, toDate);

                // If no real messages, create test data
                if (!_messages.Any())
                {
                    CreateTestMessages(fromDate, toDate);
                }

                // Parse signals
                await ParseSignals();

                // Update messages display
                UpdateMessagesDisplay();

                // Update UI
                tb_statusMessage.Text = $"Loaded {_messages.Count} messages, found {_parsedSignals.Count} signals";
                bt_runBacktest.IsEnabled = _parsedSignals.Any();
                sp_chartPlaceholder.IsVisible = _parsedSignals.Any();

                // Show signal summary
                if (_parsedSignals.Any())
                {
                    var symbolGroups = _parsedSignals.GroupBy(s => s.Pair).ToList();
                    var symbolSummary = string.Join(", ", symbolGroups.Select(g => $"{g.Key}: {g.Count()}"));
                    tb_statusMessage.Text += $" | Symbols: {symbolSummary}";
                }
            }
            catch (Exception ex)
            {
                tb_statusMessage.Text = $"Error loading messages: {ex.Message}";
            }
            finally
            {
                bt_loadMessages.IsEnabled = true;
            }
        }

        private async Task LoadRealMessages(DateTime fromDate, DateTime toDate)
        {
            // First try to load from Telegram if connected
            if (_isConnected && _telegramClient != null)
            {
                await LoadMessagesFromTelegram(fromDate, toDate);
            }
            
            // If no messages from Telegram, try loading from saved files
            if (!_messages.Any())
            {
                await LoadMessagesFromFiles(fromDate, toDate);
            }
        }

        private async Task LoadMessagesFromTelegram(DateTime fromDate, DateTime toDate)
        {
            try
            {
                tb_statusMessage.Text = "Loading messages from your channels...";
                
                var selectedChannel = (cb_channel.SelectedItem as ComboBoxItem)?.Content?.ToString();
                var messagesToLoad = new List<TelegramMessage>();

                // Load messages from selected channels
                foreach (var (channelId, channelName) in _channels)
                {
                    // Skip if specific channel selected and this isn't it
                    if (selectedChannel != "All Channels" && selectedChannel != channelName)
                        continue;

                    try
                    {
                        // Get the access hash for this channel
                        if (!_channelAccessHashes.TryGetValue(channelId, out var accessHash))
                        {
                            tb_statusMessage.Text = $"Skipping {channelName}: No access hash";
                            continue;
                        }

                        // Get messages from this channel
                        var messages = await _telegramClient.Messages_GetHistory(
                            peer: new InputPeerChannel(channelId, accessHash),
                            limit: 1000,
                            offset_date: toDate
                        );

                        foreach (var msg in messages.Messages)
                        {
                            if (msg is Message message && 
                                message.Date >= fromDate && 
                                message.Date <= toDate &&
                                !string.IsNullOrWhiteSpace(message.message))
                            {
                                messagesToLoad.Add(new TelegramMessage
                                {
                                    Id = message.ID,
                                    Date = message.Date,
                                    Text = message.message,
                                    ChannelName = channelName,
                                    ChannelId = channelId
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Skip channels we can't access
                        tb_statusMessage.Text = $"Skipping {channelName}: {ex.Message}";
                        await Task.Delay(500);
                        continue;
                    }
                }

                // Sort by date and add to main collection
                _messages.AddRange(messagesToLoad.OrderBy(m => m.Date));
                
                tb_statusMessage.Text = $"Loaded {_messages.Count} messages from {_channels.Count} channels";
            }
            catch (Exception ex)
            {
                tb_statusMessage.Text = $"Error loading from Telegram: {ex.Message}";
            }
        }

        private async Task LoadMessagesFromFiles(DateTime fromDate, DateTime toDate)
        {
            try
            {
                var messageFiles = Directory.GetFiles(_configDirectory, "*_Message_*.json");
                
                foreach (var file in messageFiles.Take(100)) // Limit for performance
                {
                    try
                    {
                        var content = await File.ReadAllTextAsync(file);
                        var messageData = JsonSerializer.Deserialize<JsonElement>(content);
                        
                        if (messageData.TryGetProperty("date", out var dateElement) &&
                            DateTime.TryParse(dateElement.GetString(), out var messageDate) &&
                            messageDate >= fromDate && messageDate <= toDate)
                        {
                            var text = messageData.TryGetProperty("message", out var msgElement) ? 
                                msgElement.GetString() ?? "" : "";
                            var channelName = messageData.TryGetProperty("chat", out var chatElement) ? 
                                chatElement.GetString() ?? "Unknown" : "Unknown";

                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                _messages.Add(new TelegramMessage
                                {
                                    Id = _messages.Count + 1,
                                    Date = messageDate,
                                    Text = text,
                                    ChannelName = channelName,
                                    ChannelId = 1
                                });
                            }
                        }
                    }
                    catch
                    {
                        // Skip invalid files
                        continue;
                    }
                }
            }
            catch
            {
                // No saved messages found
            }
        }

        private void CreateTestMessages(DateTime fromDate, DateTime toDate)
        {
            // Only create test messages if no real channels are available
            if (_channels.Any())
                return;

            tb_statusMessage.Text = "No messages found in selected date range";
        }

        private async Task ParseSignals()
        {
            try
            {
                tb_statusMessage.Text = "Parsing signals with Groq AI + Manual fallback...";
                
                foreach (var message in _messages)
                {
                    var signal = await ParseSingleMessage(message);
                    if (signal != null)
                    {
                        _parsedSignals.Add(signal);
                    }
                }
            }
            catch (Exception ex)
            {
                tb_statusMessage.Text = $"Error parsing signals: {ex.Message}";
            }
        }

        private async Task<ParsedSignal?> ParseSingleMessage(TelegramMessage message)
        {
            try
            {
                // First try HybridParser.py (Groq AI + Manual)
                var hybridResult = await TryHybridParser(message.Text);
                if (hybridResult != null)
                {
                    hybridResult.Timestamp = message.Date;
                    hybridResult.OriginalMessage = message.Text;
                    hybridResult.ChannelName = message.ChannelName;
                    return hybridResult;
                }

                // Fallback to C# SignalProcessor (Manual only)
                var result = _signalProcessor.ProcessSignal(message.Text);
                
                if (result.ContainsKey("action") && result["action"] != null)
                {
                    var signal = new ParsedSignal
                    {
                        Action = result["action"]?.ToString() ?? "",
                        Pair = ExtractSymbolFromMessage(message.Text),
                        Entry = result.ContainsKey("entry") ? result["entry"]?.ToString() ?? "0" : "0",
                        StopLoss = result.ContainsKey("stop_loss") ? result["stop_loss"]?.ToString() ?? "0" : "0",
                        TakeProfit = (result.ContainsKey("take_profit") && result["take_profit"] is Dictionary<string, string> tpDict) ? tpDict : new(),
                        Actions = (result.ContainsKey("actions") && result["actions"] is List<string> actionsList) ? actionsList : new(),
                        Timestamp = message.Date,
                        OriginalMessage = message.Text,
                        ChannelName = message.ChannelName
                    };

                    // Only add if it's a valid trading signal
                    if (!string.IsNullOrEmpty(signal.Action) && signal.Action != "not a signal")
                    {
                        return signal;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                tb_statusMessage.Text = $"Error parsing message: {ex.Message}";
                return null;
            }
        }

        private async Task<ParsedSignal?> TryHybridParser(string messageText)
        {
            try
            {
                // Check if HybridParser.py exists
                var hybridParserPath = Path.Combine(Directory.GetCurrentDirectory(), "HybridParser.py");
                if (!File.Exists(hybridParserPath))
                {
                    return null; // Fall back to manual parser
                }

                // Create temp file with message
                var tempFile = Path.GetTempFileName();
                await File.WriteAllTextAsync(tempFile, messageText);

                // Run HybridParser.py
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "python",
                        Arguments = $"\"{hybridParserPath}\" \"{tempFile}\"",
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

                // Clean up temp file
                File.Delete(tempFile);

                if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                {
                    // Parse JSON output from HybridParser
                    var jsonResult = JsonSerializer.Deserialize<JsonElement>(output);
                    
                    if (jsonResult.TryGetProperty("action", out var actionProp) && 
                        actionProp.GetString() != "not a signal")
                    {
                        var signal = new ParsedSignal
                        {
                            Action = jsonResult.GetProperty("action").GetString() ?? "",
                            Pair = jsonResult.TryGetProperty("pair", out var pairProp) ? pairProp.GetString() ?? "" : "",
                            Entry = jsonResult.TryGetProperty("entry", out var entryProp) ? entryProp.GetString() ?? "0" : "0",
                            StopLoss = jsonResult.TryGetProperty("stop_loss", out var slProp) ? slProp.GetString() ?? "0" : "0",
                            TakeProfit = new Dictionary<string, string>(),
                            Actions = new List<string>()
                        };

                        // Extract take profits
                        if (jsonResult.TryGetProperty("take_profit", out var tpProp))
                        {
                            foreach (var tp in tpProp.EnumerateObject())
                            {
                                signal.TakeProfit[tp.Name] = tp.Value.GetString() ?? "0";
                            }
                        }

                        // Extract actions
                        if (jsonResult.TryGetProperty("actions", out var actionsProp))
                        {
                            foreach (var action in actionsProp.EnumerateArray())
                            {
                                signal.Actions.Add(action.GetString() ?? "");
                            }
                        }

                        return signal;
                    }
                }

                return null; // Fall back to manual parser
            }
            catch
            {
                return null; // Fall back to manual parser
            }
        }

        private string ExtractSymbolFromMessage(string message)
        {
            var symbols = new[] { "XAUUSD", "EURUSD", "GBPUSD", "USDJPY", "BTCUSD", "NAS100", "US30", "GOLD", "BTC", "ETH" };
            
            foreach (var symbol in symbols)
            {
                if (message.ToUpper().Contains(symbol))
                {
                    return symbol == "GOLD" ? "XAUUSD" : symbol == "BTC" ? "BTCUSD" : symbol;
                }
            }
            
            return "XAUUSD"; // Default
        }

        private async void OnRunBacktestClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                bt_runBacktest.IsEnabled = false;
                
                // Get all unique symbols from parsed signals
                var allSymbols = _parsedSignals
                    .Where(s => !string.IsNullOrEmpty(s.Pair) && s.Pair != "0")
                    .Select(s => s.Pair)
                    .Distinct()
                    .ToList();

                if (!allSymbols.Any())
                {
                    tb_statusMessage.Text = "No symbols found in signals to backtest";
                    return;
                }

                tb_statusMessage.Text = $"🔄 Fetching MT5 data for {allSymbols.Count} symbols: {string.Join(", ", allSymbols)}";
                
                // Test MT5 connection first
                var isConnected = await _dataProvider.TestMT5ConnectionAsync();
                if (!isConnected)
                {
                    tb_statusMessage.Text = "⚠️ MT5 not connected - using simulated data for backtest";
                    await Task.Delay(1000); // Show warning briefly
                }
                else
                {
                    tb_statusMessage.Text = "✅ MT5 connected - fetching real market data";
                    await Task.Delay(500);
                }

                // Update status for each symbol being processed
                var processedSymbols = new List<string>();
                foreach (var symbol in allSymbols)
                {
                    tb_statusMessage.Text = $"📊 Fetching data for {symbol}... ({processedSymbols.Count + 1}/{allSymbols.Count})";
                    
                    try
                    {
                        // Pre-fetch data to validate symbol availability
                        var testData = await _dataProvider.GetCandleDataAsync(
                            symbol, 
                            dp_fromDate.SelectedDate!.Value.DateTime,
                            dp_toDate.SelectedDate!.Value.DateTime.AddDays(1), // Add buffer
                            "H1"
                        );
                        
                        if (testData.Any())
                        {
                            processedSymbols.Add(symbol);
                            tb_statusMessage.Text = $"✅ {symbol}: {testData.Count} candles loaded";
                        }
                        else
                        {
                            tb_statusMessage.Text = $"⚠️ {symbol}: No data available";
                        }
                        
                        await Task.Delay(300); // Brief pause to show progress
                    }
                    catch (Exception ex)
                    {
                        tb_statusMessage.Text = $"❌ {symbol}: {ex.Message}";
                        await Task.Delay(500);
                    }
                }

                tb_statusMessage.Text = $"🚀 Running backtest for {processedSymbols.Count} symbols with {_parsedSignals.Count} signals...";

                var settings = new BacktestSettings
                {
                    StartDate = dp_fromDate.SelectedDate!.Value.DateTime,
                    EndDate = dp_toDate.SelectedDate!.Value.DateTime,
                    Symbol = "All Symbols", // Process all symbols
                    Channel = (cb_channel.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All Channels",
                    InitialBalance = 10000,
                    RiskPerTrade = 1.0,
                    Spread = 2.0,
                    MaxOpenTrades = 10,
                    UseStopLoss = true,
                    UseTakeProfit = true
                };

                _lastBacktestResult = await _backtestEngine.RunBacktestAsync(_parsedSignals, settings);

                // Update results UI
                UpdateBacktestResults(_lastBacktestResult);

                // Enhanced status message with symbol breakdown
                var symbolStats = _lastBacktestResult.Trades
                    .GroupBy(t => t.Symbol)
                    .Select(g => $"{g.Key}: {g.Count()}")
                    .ToList();

                var winRateColor = _lastBacktestResult.WinRate >= 50 ? "✅" : "⚠️";
                var pnlColor = _lastBacktestResult.TotalPnL >= 0 ? "💰" : "📉";
                
                tb_statusMessage.Text = $"{winRateColor} Backtest completed: {_lastBacktestResult.TotalTrades} trades across {processedSymbols.Count} symbols | " +
                                      $"{_lastBacktestResult.WinRate:F1}% win rate {pnlColor} P&L: ${_lastBacktestResult.TotalPnL:F2} | " +
                                      $"Symbols: {string.Join(", ", symbolStats)}";
                
                bt_exportMQL5.IsEnabled = true;

                // Show chart placeholder with symbol info
                sp_chartPlaceholder.IsVisible = false;
                sp_resultsPlaceholder.IsVisible = false;
                sp_stats.IsVisible = true;
            }
            catch (Exception ex)
            {
                tb_statusMessage.Text = $"❌ Backtest error: {ex.Message}";
            }
            finally
            {
                bt_runBacktest.IsEnabled = true;
            }
        }

        private void UpdateBacktestResults(BacktestResult result)
        {
            try
            {
                tb_totalTrades.Text = result.TotalTrades.ToString();
                tb_winRate.Text = $"{result.WinRate:F1}%";
                tb_totalPnL.Text = $"${result.TotalPnL:F2}";
                tb_maxDrawdown.Text = $"{result.MaxDrawdown:F1}%";

                // Color code the results
                tb_winRate.Foreground = result.WinRate >= 50 ? Avalonia.Media.Brushes.LightGreen : Avalonia.Media.Brushes.Orange;
                tb_totalPnL.Foreground = result.TotalPnL >= 0 ? Avalonia.Media.Brushes.LightGreen : Avalonia.Media.Brushes.LightCoral;

                // Update trade list
                lb_trades.Items.Clear();
                foreach (var trade in result.Trades.Take(15)) // Show first 15 trades
                {
                    var tradeItem = new TextBlock
                    {
                        Text = $"{trade.EntryTime:MM/dd HH:mm} | {trade.Symbol} {trade.Action}\nP&L: ${trade.PnL:F2} | {trade.ExitReason}",
                        Foreground = trade.IsWin ? Avalonia.Media.Brushes.LightGreen : Avalonia.Media.Brushes.LightCoral,
                        Margin = new Avalonia.Thickness(0, 4),
                        FontSize = 11
                    };
                    lb_trades.Items.Add(tradeItem);
                }

                if (result.Trades.Count > 15)
                {
                    var moreItem = new TextBlock
                    {
                        Text = $"... and {result.Trades.Count - 15} more trades",
                        Foreground = Avalonia.Media.Brushes.Gray,
                        Margin = new Avalonia.Thickness(0, 4),
                        FontSize = 11,
                        FontStyle = Avalonia.Media.FontStyle.Italic
                    };
                    lb_trades.Items.Add(moreItem);
                }

                // Show results, hide placeholder
                sp_stats.IsVisible = true;
                sp_resultsPlaceholder.IsVisible = false;
            }
            catch (Exception ex)
            {
                tb_statusMessage.Text = $"Error updating results: {ex.Message}";
            }
        }

        private void OnExportMQL5Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (_lastBacktestResult == null)
                {
                    tb_statusMessage.Text = "No backtest results to export";
                    return;
                }

                var mql5Code = _mql5Exporter.ExportBacktestResults(_lastBacktestResult);
                
                var fileName = $"DaneTradesEA_{DateTime.Now:yyyyMMdd_HHmmss}.mq5";
                var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);
                
                _mql5Exporter.SaveToFile(mql5Code, filePath);
                tb_statusMessage.Text = $"🎯 MQL5 EA exported to Desktop: {fileName} | {_lastBacktestResult.TotalTrades} signals included";
            }
            catch (Exception ex)
            {
                tb_statusMessage.Text = $"Export error: {ex.Message}";
            }
        }

        private void OnHelpClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://danetrades.com/help",
                    UseShellExecute = true
                });
                tb_statusMessage.Text = "Help page opened in browser";
            }
            catch 
            {
                tb_statusMessage.Text = "Help: Select date range → Load Messages → Run Backtest → Export MQL5";
            }
        }

        private void OnMoreClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://danetrades.com/products",
                    UseShellExecute = true
                });
                tb_statusMessage.Text = "Products page opened in browser";
            }
            catch 
            {
                tb_statusMessage.Text = "Visit danetrades.com for more trading tools";
            }
        }

        private void UpdateMessagesDisplay()
        {
            try
            {
                lb_messages.Items.Clear();
                
                if (!_messages.Any())
                {
                    sp_messagesPlaceholder.IsVisible = true;
                    tb_messageCount.Text = "0 messages loaded";
                    return;
                }

                sp_messagesPlaceholder.IsVisible = false;
                
                // Sort messages by date (newest first)
                var sortedMessages = _messages.OrderByDescending(m => m.Date).Take(50).ToList();
                
                foreach (var message in sortedMessages)
                {
                    // Check if this message was parsed as a signal
                    var parsedSignal = _parsedSignals.FirstOrDefault(s => s.OriginalMessage == message.Text);
                    var isSignal = parsedSignal != null;
                    
                    var messagePanel = new StackPanel
                    {
                        Margin = new Avalonia.Thickness(0, 5),
                        Background = isSignal ? 
                            new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(30, 56, 139, 192)) : 
                            Avalonia.Media.Brushes.Transparent
                    };

                    // Header with channel and time
                    var headerPanel = new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        Margin = new Avalonia.Thickness(10, 5, 10, 2)
                    };

                    var channelText = new TextBlock
                    {
                        Text = message.ChannelName,
                        FontSize = 10,
                        FontWeight = Avalonia.Media.FontWeight.Bold,
                        Foreground = isSignal ? Avalonia.Media.Brushes.LightBlue : Avalonia.Media.Brushes.Gray
                    };

                    var timeText = new TextBlock
                    {
                        Text = $" • {message.Date:MM/dd HH:mm}",
                        FontSize = 10,
                        Foreground = Avalonia.Media.Brushes.Gray
                    };

                    headerPanel.Children.Add(channelText);
                    headerPanel.Children.Add(timeText);

                    if (isSignal)
                    {
                        // Determine parsing method
                        var parsingMethod = DetermineParsingMethod(message.Text);
                        var methodColor = parsingMethod == "GROQ" ? Avalonia.Media.Brushes.LightGreen : Avalonia.Media.Brushes.Orange;
                        var methodIcon = parsingMethod == "GROQ" ? "🤖" : "⚙️";
                        
                        var signalBadge = new TextBlock
                        {
                            Text = $" {methodIcon} {parsingMethod}",
                            FontSize = 9,
                            FontWeight = Avalonia.Media.FontWeight.Bold,
                            Foreground = methodColor,
                            Margin = new Avalonia.Thickness(5, 0, 0, 0)
                        };
                        headerPanel.Children.Add(signalBadge);

                        // Show parsed signal details
                        if (parsedSignal != null)
                        {
                            var signalDetails = new TextBlock
                            {
                                Text = $" | {parsedSignal.Action} {parsedSignal.Pair}",
                                FontSize = 9,
                                FontWeight = Avalonia.Media.FontWeight.Bold,
                                Foreground = Avalonia.Media.Brushes.Yellow,
                                Margin = new Avalonia.Thickness(5, 0, 0, 0)
                            };
                            headerPanel.Children.Add(signalDetails);
                        }
                    }

                    // Message content
                    var contentText = new TextBlock
                    {
                        Text = message.Text.Length > 200 ? message.Text.Substring(0, 200) + "..." : message.Text,
                        FontSize = 11,
                        Foreground = Avalonia.Media.Brushes.White,
                        Margin = new Avalonia.Thickness(10, 2, 10, 8),
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    };

                    messagePanel.Children.Add(headerPanel);
                    messagePanel.Children.Add(contentText);

                    lb_messages.Items.Add(messagePanel);
                }

                // Update count
                var signalCount = _parsedSignals.Count;
                tb_messageCount.Text = $"{_messages.Count} messages loaded ({signalCount} signals found)";
            }
            catch (Exception ex)
            {
                tb_statusMessage.Text = $"Error displaying messages: {ex.Message}";
            }
        }



        private string DetermineParsingMethod(string messageText)
        {
            // Simple heuristic to determine parsing method
            // In a real implementation, this would be determined by the actual parsing process
            // For now, we'll use some basic rules to simulate the decision
            
            if (string.IsNullOrEmpty(messageText))
                return "MANUAL";
                
            // Check if message contains complex patterns that would likely use AI
            var complexPatterns = new[]
            {
                @"(?i)\b(buy|sell)\s+[a-z]{3,6}\s*@?\s*[0-9.]+",
                @"(?i)tp\s*[0-9]*\s*[:@]\s*[0-9.]+",
                @"(?i)sl\s*[:@]\s*[0-9.]+",
                @"(?i)entry\s*[:@]\s*[0-9.]+"
            };
            
            int patternMatches = 0;
            foreach (var pattern in complexPatterns)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(messageText, pattern))
                    patternMatches++;
            }
            
            // If multiple patterns match, likely used AI parsing
            // If message is very short or simple, likely manual parsing
            if (patternMatches >= 2 && messageText.Length > 50)
                return "GROQ";
            else
                return "MANUAL";
        }
    }
}