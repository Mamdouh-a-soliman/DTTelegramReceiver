using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System.Text; // Included as in your original

public class SignalProcessor
{
    private Dictionary<string, object> signal;

    public SignalProcessor()
    {
        signal = new Dictionary<string, object>();
    }

    // Your original CleanMessage method
    private string CleanMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return string.Empty; // Handle null or empty input
        // First remove emojis and other special characters
        message = Regex.Replace(message, @"[^\x00-\x7F]+", "");
        message = Regex.Replace(message, ":-", "");
        // Then keep only alphanumeric characters, spaces, dots, colons, hyphens, @, and /
        message = Regex.Replace(message, @"[^\w\s.:@/-]", "");
        return message;
    }

    public Dictionary<string, object> ProcessSignal(string rawMessage)
    {
        // Reset signal template to default values - your original
        signal = new Dictionary<string, object>
        {
            ["action"] = null,
            ["pair"] = null,
            ["entry"] = "0",
            ["stop_loss"] = "0",
            ["take_profit"] = new Dictionary<string, string>(),
            ["actions"] = new List<string>()
        };

        string cleanedMessage = CleanMessage(rawMessage);

        // Step 1: Extract Action - your original
        var actionMatch = Regex.Match(cleanedMessage,
            @"\b(BUY LIMIT|BUYLIMIT|SELL LIMIT|SELLLIMIT|BUY STOP|BUYSTOP|SELL STOP|SELLSTOP|BUY|SELL|LONG|SHORT)\b",
            RegexOptions.IgnoreCase);

        if (actionMatch.Success)
        {
            string action = actionMatch.Value.ToUpper();
            if (action == "LONG" || action == "SHORT")
            {
                signal["action"] = action == "LONG" ? "BUY" : "SELL";
            }
            else
            {
                signal["action"] = action; // e.g., "BUY LIMIT", "SELLLIMIT"
            }
        }

        // Step 2: Extract Entry - your original logic structure
        var entryRangeMatch = Regex.Match(cleanedMessage, @"\b([0-9.]+)\s*[-–_/]\s*([0-9.]+)\b", RegexOptions.IgnoreCase);
        var entryAtRegex = Regex.Match(cleanedMessage, @"@\s*([0-9.]+)", RegexOptions.IgnoreCase); // Python: entry_at_regex

        if (entryRangeMatch.Success)
        {
            signal["entry"] = $"{entryRangeMatch.Groups[1].Value} - {entryRangeMatch.Groups[2].Value}";
        }
        else if (entryAtRegex.Success)
        {
            signal["entry"] = entryAtRegex.Groups[1].Value;
        }

        // Python: if self.signal["entry"] == 0: (after trying range and @)
        // C#: Check if signal["entry"] is still its default "0".
        if (signal["entry"].ToString() == "0")
        {
            // Regexes from your C# and Python's fallback logic
            var entryRegex = Regex.Match(cleanedMessage, @"Entry[:\s]+([0-9.]+)", RegexOptions.IgnoreCase);
            var entryPriceRegex = Regex.Match(cleanedMessage, @"Entry Price[:\s]+([0-9.]+)", RegexOptions.IgnoreCase);
            var entryNowRegex = Regex.Match(cleanedMessage, @"now at\s*([0-9.]+)", RegexOptions.IgnoreCase);
            // Python: r"\b(BUY|SELL)\b\s+[A-Z]{3,6}\s+([0-9.]+)"
            // Your C#: @"\b(BUY|SELL)\b\s+[A-Z0-9]+\s+([0-9.]+)" (using your C# version)
            var actionEntryRegex = Regex.Match(cleanedMessage, @"\b(BUY|SELL)\b\s+[A-Z0-9]+\s+([0-9.]+)", RegexOptions.IgnoreCase);
            var priceMatch = Regex.Match(cleanedMessage, @"Price[:\s]+([0-9.]+)", RegexOptions.IgnoreCase); // From your C#

            if (entryRegex.Success)
                signal["entry"] = entryRegex.Groups[1].Value;
            else if (entryPriceRegex.Success)
                signal["entry"] = entryPriceRegex.Groups[1].Value;
            else if (entryNowRegex.Success)
                signal["entry"] = entryNowRegex.Groups[1].Value;
            else if (actionEntryRegex.Success)
                signal["entry"] = actionEntryRegex.Groups[2].Value;
            else if (priceMatch.Success) // From your original C#
                signal["entry"] = priceMatch.Groups[1].Value;
        }

        // Step 3: Extract Stop Loss - your original C# logic structure
        // (Python `sl_open_regex`, `stop_regex`, `sl_regex` sequence)
        var slOpenRegex = Regex.Match(cleanedMessage, @"SL[:\s]*open|StopLoss[:\s]*open|Stop[:\s]*open", RegexOptions.IgnoreCase);
        var stopRegex = Regex.Match(cleanedMessage, @"(?<!\bBUY\s)(?<!\bSELL\s)Stop[:\s]+([0-9.]+)", RegexOptions.IgnoreCase);

        // Your broad SL pattern from C#
        var slPattern = @"(?<!\bBUY\s)(?<!\bSELL\s)" +
            @"(StopLoss[:\s]+([0-9.]+)|SL[:\s@]+([0-9.]+)|STOP LOSS[:\s]+([0-9.]+)|STOP[:\s]+([0-9.]+)|" +
            @"StopLoss To[:\s]+([0-9.]+)|SL TO[:\s@]+([0-9.]+)|STOP LOSS TO[:\s]+([0-9.]+)|STOP TO[:\s]+([0-9.]+)|" +
            @"Move SL to[:\s]+([0-9.]+)|Move StopLoss to[:\s]+([0-9.]+)|Move Stop to[:\s]+([0-9.]+)|" +
            @"Change SL to[:\s]+([0-9.]+)|Change StopLoss to[:\s]+([0-9.]+)|Change Stop to[:\s]+([0-9.]+)|" +
            @"Adjust SL to[:\s]+([0-9.]+)|Adjust StopLoss to[:\s]+([0-9.]+)|Adjust Stop to[:\s]+([0-9.]+)|" +
            @"Modify SL to[:\s]+([0-9.]+)|Modify StopLoss to[:\s]+([0-9.]+)|Modify Stop to[:\s]+([0-9.]+)|" +
            @"STOP LOSS[.\s]*([0-9.]+)|" +
            @"Stop loss at[:\s]+([0-9.]+)|SL at[:\s]+([0-9.]+)|Stop at[:\s]+([0-9.]+))";
        var slMatch = Regex.Match(cleanedMessage, slPattern, RegexOptions.IgnoreCase);

        if (slOpenRegex.Success)
        {
            signal["stop_loss"] = "0"; // String "0" as per your C# default
        }
        else if (stopRegex.Success)
        {
            signal["stop_loss"] = stopRegex.Groups[1].Value;
        }
        else if (slMatch.Success) // Your existing C# broad SL match
        {
            // Your logic to extract the value from multiple groups in slMatch
            signal["stop_loss"] = slMatch.Groups.Cast<Group>()
                .Skip(1) // Skip the full match
                .FirstOrDefault(g => g.Success && !string.IsNullOrWhiteSpace(g.Value) && g.Value.Replace(".", "").All(char.IsDigit))?.Value ?? "0";
        }

        // Step 4: Extract Take Profit (LOGIC TO MATCH PYTHON)
        var takeProfits = new Dictionary<string, string>();

        // C# translation of the Python TP regex.
        // Each of the 4 alternatives has one capturing group for its value.
        // Group 1: Value from TP(...), Group 2: Value from Target(...), etc.
        string csharpTpPattern =
            @"(?:TP(?:\s*[¹²³⁴⁵⁶⁷⁸⁹\d]?)?\s*(?:➡️|at|to|:|=|@|\.)?[:\s]+([0-9.]+|open))|" +
            @"(?:Target(?:\s*[¹²³⁴⁵⁶⁷⁸⁹\d]?)?\s*(?:➡️|at|to|:|=|@|\.)?[:\s]+([0-9.]+|open))|" +
            @"(?:TakeProfit(?:\s*[¹²³⁴⁵⁶⁷⁸⁹\d]?)?\s*(?:➡️|at|to|:|=|@|\.)?[:\s]+([0-9.]+|open))|" +
            @"(?:Take Profit(?:\s*[¹²³⁴⁵⁶⁷⁸⁹\d]?)?\s*(?:➡️|at|to|:|=|@|\.)?[:\s]+([0-9.]+|open))";

        var tpMatches = Regex.Matches(cleanedMessage, csharpTpPattern, RegexOptions.IgnoreCase);
        int tpIdx = 0; // Python's enumerate starts idx at 0

        foreach (Match match in tpMatches)
        {
            string tpValue = null;
            // Find which of the 4 capturing groups was successful
            // Groups[0] is full match. Groups[1-4] correspond to ([0-9.]+|open) in each alternative.
            for (int i = 1; i <= 4; i++)
            {
                if (match.Groups[i].Success)
                {
                    tpValue = match.Groups[i].Value;
                    break;
                }
            }

            if (tpValue != null)
            {
                if (tpValue.Equals("open", StringComparison.OrdinalIgnoreCase))
                {
                    tpValue = "0"; // Python uses int 0; C# uses string "0" for consistency
                }
                tpIdx++; // Increment for TP1, TP2, ...
                takeProfits[$"TP{tpIdx}"] = tpValue;
            }
        }

        // Python: If no TPs are found, only set TP1 to 0 if it's an entry signal
        if (!takeProfits.Any())
        {
            string currentAction = signal["action"] as string;
            if (currentAction != null)
            {
                // List of actions from Python code that trigger default TP1
                var validActionsForDefaultTp = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                    "BUY", "SELL",
                    "BUYLIMIT", "SELLLIMIT", "BUYSTOP", "SELLSTOP", // Covers "SELLLIMIT"
                    "BUY LIMIT", "SELL LIMIT", "BUY STOP", "SELL STOP" // Covers "SELL LIMIT"
                };
                if (validActionsForDefaultTp.Contains(currentAction))
                {
                    takeProfits["TP1"] = "0";
                }
            }
        }
        signal["take_profit"] = takeProfits;

        // Step 5: Detect Additional Actions - your original C#
        var closeKeywords = @"\b(close trade|close|close now|exit|close position|terminate|liquidate)\b";
        var deleteKeywords = @"\b(cancel order|delete order|delete|cancel|void|remove order|scrap order)\b";
        var partialKeywords = @"\b(partial close|partial|reduce position|scale out|take some profit|exit partial)\b";
        var breakevenKeywords = @"\b(breakeven|be|move to be|set to be|move stop to be)\b";
        var modifyKeywords = @"\b(modify|change|move|adjust|edit|revise|tweak)\b";

        var actionsList = signal["actions"] as List<string>; // Your variable name was 'actions'

        if (Regex.IsMatch(cleanedMessage, closeKeywords, RegexOptions.IgnoreCase))
            actionsList.Add("CLOSE");
        if (Regex.IsMatch(cleanedMessage, deleteKeywords, RegexOptions.IgnoreCase))
            actionsList.Add("DELETE ORDER");
        if (Regex.IsMatch(cleanedMessage, partialKeywords, RegexOptions.IgnoreCase))
            actionsList.Add("PARTIAL CLOSE");
        if (Regex.IsMatch(cleanedMessage, breakevenKeywords, RegexOptions.IgnoreCase))
            actionsList.Add("MOVE TO BREAKEVEN");
        if (Regex.IsMatch(cleanedMessage, modifyKeywords, RegexOptions.IgnoreCase))
            actionsList.Add("MODIFY");

        // Return condition - your original C# (Python: `if self.signal["action"] or self.signal["actions"]`)
        if (signal["action"] != null || actionsList.Any())
            return signal;
        else
            return new Dictionary<string, object> { ["original_message"] = rawMessage };
    }

    // Your original FormatSignal method - UNCHANGED from your first post
    public string FormatSignal(Dictionary<string, object> signal, string originalMessage)
    {
        string formattedMessage = CleanMessage(originalMessage);

        // Replace Stop Loss if detected
        if (signal.TryGetValue("stop_loss", out var slObj) && slObj != null && slObj.ToString() != "0")
        {
            var slPattern = @"(?<!\bBUY\s)(?<!\bSELL\s)" +
                @"(StopLoss[:\s]+([0-9.]+)|SL[:\s@]+([0-9.]+)|STOP LOSS[:\s]+([0-9.]+)|" +
                @"StopLoss To[:\s]+([0-9.]+)|SL TO[:\s@]+([0-9.]+)|STOP LOSS TO[:\s]+([0-9.]+)|STOP TO[:\s]+([0-9.]+)|" +
                @"Move SL to[:\s]+([0-9.]+)|Move StopLoss to[:\s]+([0-9.]+)|Move Stop to[:\s]+([0-9.]+)|" +
                @"Change SL to[:\s]+([0-9.]+)|Change StopLoss to[:\s]+([0-9.]+)|Change Stop to[:\s]+([0-9.]+)|" +
                @"Adjust SL to[:\s]+([0-9.]+)|Adjust StopLoss to[:\s]+([0-9.]+)|Adjust Stop to[:\s]+([0-9.]+)|" +
                @"Modify SL to[:\s]+([0-9.]+)|Modify StopLoss to[:\s]+([0-9.]+)|Modify Stop to[:\s]+([0-9.]+)|" +
                @"STOP LOSS[.\s]*([0-9.]+)|" +
                @"Stop loss at[:\s]+([0-9.]+)|SL at[:\s]+([0-9.]+)|Stop at[:\s]+([0-9.]+))";

            var slMatch = Regex.Match(formattedMessage, slPattern, RegexOptions.IgnoreCase);
            if (slMatch.Success)
            {
                // Ensure slObj.ToString() is used here
                formattedMessage = Regex.Replace(formattedMessage, slMatch.Value, $"SL: {slObj.ToString()}");
            }
        }

        // Replace Take Profits if detected
        if (signal.TryGetValue("take_profit", out var tpObj) && tpObj is Dictionary<string, string> takeProfitsDict && takeProfitsDict.Any())
        {
            // This formatting logic is from your original C#. It attempts to find original TP text and replace it.
            // It might need careful review if the original TP text format varies wildly from "TP#: value".
            // The Python format_signal is also complex and re-parses.
            // For now, retaining your logic. The takeProfitsDict now correctly has TP1, TP2...
            var tpPattern = @"(?:Take profit|TP|Target)\s*(\d+)?\s*(?:at|@|:|\.)?\s*([0-9.]+)(?:\s*(?:CONFIRM|open))?|" +
                           @"Take Profit\s*([0-9.]+)";

            var tpMatches = Regex.Matches(formattedMessage, tpPattern, RegexOptions.IgnoreCase);
            var tpPositions = new List<(int position, string match)>();

            foreach (Match match in tpMatches)
            {
                tpPositions.Add((match.Index, match.Value));
            }

            // Sort original TP matches by their position in the text
            tpPositions.Sort((a, b) => a.position.CompareTo(b.position));

            // Sort the processed TPs by their key (TP1, TP2, TP3...)
            // To ensure TP10 comes after TP2, a natural sort is needed for the keys.
            var sortedProcessedTps = takeProfitsDict.OrderBy(kvp =>
                int.TryParse(kvp.Key.Replace("TP", ""), out int num) ? num : int.MaxValue
            );

            foreach (var tpEntry in sortedProcessedTps) // tpEntry is KeyValuePair<string, string>
            {
                if (tpPositions.Count > 0)
                {
                    var (position, fullMatchText) = tpPositions[0]; // Get the next original TP text
                    tpPositions.RemoveAt(0);
                    // Replace the original TP text with the processed one.
                    // Example: fullMatchText could be "TP Open", tpEntry.Key "TP1", tpEntry.Value "0"
                    // Replacement: "TP1: 0"
                    formattedMessage = formattedMessage.Replace(fullMatchText, $"{tpEntry.Key}: {tpEntry.Value}");
                }
            }
        }

        // Replace Entry if detected - Your original logic
        if (signal.TryGetValue("entry", out var entryObj) && entryObj != null && entryObj.ToString() != "0")
        {
            // ... (Your existing complex entry formatting logic, unchanged) ...
            // This part is extensive and kept as is.
            var entryRangeMatch_fmt = Regex.Match(formattedMessage, @"\b([0-9.]+)\s*[-–_/]\s*([0-9.]+)\b", RegexOptions.IgnoreCase);
            var entryAtMatch_fmt = Regex.Match(formattedMessage, @"@\s*([0-9.]+)", RegexOptions.IgnoreCase);
            var entryMatch_fmt = Regex.Match(formattedMessage, @"Entry[:\s]+([0-9.]+)", RegexOptions.IgnoreCase);
            var entryPriceMatch_fmt = Regex.Match(formattedMessage, @"Entry Price[:\s]+([0-9.]+)", RegexOptions.IgnoreCase);
            var entryNowMatch_fmt = Regex.Match(formattedMessage, @"now at\s*([0-9.]+)", RegexOptions.IgnoreCase);
            var actionEntryMatch_fmt = Regex.Match(formattedMessage, @"\b(BUY|SELL)\b\s+[A-Z0-9]+\s+([0-9.]+)", RegexOptions.IgnoreCase);
            var priceMatch_fmt = Regex.Match(formattedMessage, @"Price[:\s]+([0-9.]+)", RegexOptions.IgnoreCase);

            string entryValueStr = entryObj.ToString();

            if (entryRangeMatch_fmt.Success)
            {
                formattedMessage = Regex.Replace(formattedMessage,
                    @"\b(?:Entry\s*:|Entry\s+now\s*:|now\s+at\s*|Entry\s*@\s*|Entry\s+Price\s*:|Entry\s+point\s*:|Entry\s+level\s*:)",
                    "", RegexOptions.IgnoreCase);
                formattedMessage = formattedMessage.Replace(entryRangeMatch_fmt.Value, $"\nEntry: {entryValueStr}");
            }
            else if (entryAtMatch_fmt.Success)
            {
                formattedMessage = Regex.Replace(formattedMessage,
                    @"\b(?:Entry\s*:|Entry\s+now\s*:|now\s+at\s*|Entry\s*@\s*|Entry\s+Price\s*:|Entry\s+point\s*:|Entry\s+level\s*:)",
                    "", RegexOptions.IgnoreCase);
                formattedMessage = formattedMessage.Replace(entryAtMatch_fmt.Value, $"\nEntry: {entryValueStr}");
            }
            // ... and so on for all your entry formatting cases from original C# ...
            else if (entryMatch_fmt.Success)
            {
                formattedMessage = Regex.Replace(formattedMessage,
                    @"\b(?:Entry\s*:|Entry\s+now\s*:|now\s+at\s*|Entry\s*@\s*|Entry\s+Price\s*:|Entry\s+point\s*:|Entry\s+level\s*:)",
                    "", RegexOptions.IgnoreCase);
                formattedMessage = formattedMessage.Replace(entryMatch_fmt.Value, $"\nEntry: {entryValueStr}");
            }
            else if (entryPriceMatch_fmt.Success)
            {
                formattedMessage = Regex.Replace(formattedMessage,
                    @"\b(?:Entry\s*:|Entry\s+now\s*:|now\s+at\s*|Entry\s*@\s*|Entry\s+Price\s*:|Entry\s+point\s*:|Entry\s+level\s*:)",
                    "", RegexOptions.IgnoreCase);
                formattedMessage = formattedMessage.Replace(entryPriceMatch_fmt.Value, $"\nEntry: {entryValueStr}");
            }
            else if (entryNowMatch_fmt.Success)
            {
                formattedMessage = Regex.Replace(formattedMessage,
                    @"\b(Entry\s*:|Entry\s+now\s*:|now\s+at\s*|Entry\s*@\s*|Entry\s+Price\s*:|Entry\s+point\s*:|Entry\s+level\s*:|Entry\s*-\s*[0-9.]+|" +
                    @"Enter\s*:|Enter\s+now\s*:|Enter\s+at\s*|Enter\s*@\s*|Enter\s+Price\s*:|Enter\s+point\s*:|Enter\s+level\s*:|Enter\s*-\s*[0-9.]+)\b",
                    "", RegexOptions.IgnoreCase);
                formattedMessage = formattedMessage.Replace(entryNowMatch_fmt.Value, $"\nEntry: {entryValueStr}");
            }
            else if (actionEntryMatch_fmt.Success) // This is your original 'actionEntryMatch' (now _fmt)
            {
                // Your logic for action-symbol-entry like "BUY XAUUSD 1234"
                // This part needs to access signal["action"], signal["pair"] if they were set by ProcessSignal
                string sigAction = signal.TryGetValue("action", out var sa) && sa != null ? sa.ToString() : "";
                string sigPair = signal.TryGetValue("pair", out var sp) && sp != null ? sp.ToString() : "";
                // If pair was not extracted by ProcessSignal, this might be an issue.
                // Your original C# ProcessSignal does not set signal["pair"]. Python's ProcessSignal does not either.
                // But this formatting code seems to expect it.
                // For now, I'll assume `sigPair` might be empty if not found.
                // The regex `actionEntryMatch_fmt` captures these parts from the `formattedMessage` itself.

                var actionSymbolEntryMatch_fmt = Regex.Match(formattedMessage,
                    @"\b(BUY|SELL)\b\s+([A-Z0-9]+)\s+([0-9.]+)", // Your regex from format
                    RegexOptions.IgnoreCase);

                if (actionSymbolEntryMatch_fmt.Success)
                {
                    // Use values from the current match on formattedMessage
                    string currentAction = actionSymbolEntryMatch_fmt.Groups[1].Value.ToUpper();
                    string currentPair = actionSymbolEntryMatch_fmt.Groups[2].Value.ToUpper();
                    // entryValueStr is already from signal["entry"]
                    formattedMessage = formattedMessage.Replace(actionSymbolEntryMatch_fmt.Value,
                        $"{currentAction} {currentPair} \nEntry: {entryValueStr}");
                }
            }
            else if (priceMatch_fmt.Success)
            {
                formattedMessage = formattedMessage.Replace(priceMatch_fmt.Value, $"Entry: {entryValueStr}");
            }
        }

        // Remove excessive newlines and whitespace - Your original
        formattedMessage = Regex.Replace(formattedMessage, @"\n\s*\n+", "\n").Trim();
        return formattedMessage;
    }

    // Your original UpdateMessageInJson method - UNCHANGED
    public string UpdateMessageInJson(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            Console.WriteLine("Message is empty or null.");
            return message;
        }

        var processedSignal = ProcessSignal(message);
        string formattedMessage = FormatSignal(processedSignal, message);
        // Console.WriteLine(formattedMessage); // This was in your original for debugging
        return formattedMessage;
    }
}