using System;
using System.Globalization;
using System.IO; // <<< Added for Path class
using System.Linq; // <<< Added for Where() method
using System.Text;
using System.Text.RegularExpressions;
using Unidecode.NET; // <<< Added for Unidecode() extension method

namespace DTTelegramReceiver // <<< MAKE SURE THIS MATCHES YOUR PROJECT NAMESPACE
{
    public static class TextNormalization
    {
        // Matches Python's processor.clean_message (simple space/newline replace)
        public static string CleanMessageSimple(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            // Replace sequences of whitespace/newlines with a single space
            return Regex.Replace(text, @"\s+", " ").Trim();
        }

        // Matches Python's normalize_to_ascii (NFKD Normalization + ASCII encoding ignore)
        public static string NormalizeToAscii(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // Normalize the text to decompose characters (e.g., é -> e + ´)
            var normalizedString = text.Normalize(NormalizationForm.FormKD);

            // Replicate Python's .encode('ascii', 'ignore').decode('ascii'):
            // Get bytes using ASCII encoding, which ignores non-ASCII characters.
            byte[] asciiBytes = Encoding.ASCII.GetBytes(normalizedString);
            // Convert the resulting ASCII bytes back to a string.
            return Encoding.ASCII.GetString(asciiBytes);
        }


        // Matches Python's transliterate_to_ascii (using Unidecode)
        public static string TransliterateToAscii(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            // Uses UnidecodeSharpFork's extension method
            return text.Unidecode();
        }

        // Combined cleaning for filenames/channel names (Matches Python Logic)
        public static string CleanAndNormalizeNameForFile(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unknown";

            // 1. Transliterate (Handles non-Latin chars like emojis, cyrillic etc.)
            string transliterated = TransliterateToAscii(name);

            // 2. Normalize to basic ASCII (Removes leftover accents from transliteration if any)
            // This step might be optional depending on how well Unidecode works for your specific inputs,
            // but it matches the python code's separate normalize step more closely.
            string normalized = NormalizeToAscii(transliterated);

            // 3. Filesystem Safe (Remove invalid chars, replace with _)
            string filesystemSafe = MakeFileSystemSafe(normalized);

            // 4. Replace multiple underscores with one (often desirable after cleaning)
            filesystemSafe = Regex.Replace(filesystemSafe, "_+", "_").Trim('_');


            // 5. Limit Length (optional but good practice)
            int maxLength = 60; // Match Python's approach or choose a limit
            if (filesystemSafe.Length > maxLength)
            {
                filesystemSafe = filesystemSafe.Substring(0, maxLength).TrimEnd('_'); // Trim potential trailing _ after substring
            }

            // Ensure not empty after all cleaning
            return string.IsNullOrWhiteSpace(filesystemSafe) ? "Unknown" : filesystemSafe;
        }

        // Removes invalid file system characters and reserved names
        public static string MakeFileSystemSafe(string name)
        {
            if (string.IsNullOrEmpty(name)) return "_"; // Return underscore if null/empty

            // Define invalid characters based on typical Windows restrictions
            string invalidChars = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            // Create a regex pattern from these invalid characters
            string invalidCharsRegex = string.Format("[{0}]", Regex.Escape(invalidChars));

            // Replace invalid characters with an underscore "_"
            string safeName = Regex.Replace(name, invalidCharsRegex, "_");

            // Replace multiple consecutive underscores with a single underscore
            safeName = Regex.Replace(safeName, "_+", "_");

            // Prevent names that are reserved in Windows (CON, PRN, AUX, NUL, COM1-9, LPT1-9) etc.
            // Added common leading/trailing issues prevention.
            string[] reservedNames = {
                "CON", "PRN", "AUX", "NUL",
                "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
                ".", ".."}; // Added dot folders

            // Split name by extension for robust check
            string nameWithoutExt = Path.GetFileNameWithoutExtension(safeName);
            string extension = Path.GetExtension(safeName); // includes the dot if present

            if (reservedNames.Contains(nameWithoutExt.ToUpperInvariant()))
            {
                // If the base name is reserved, append an underscore BEFORE the extension
                nameWithoutExt += "_";
                safeName = nameWithoutExt + extension; // Reconstruct
            }

            // Remove leading/trailing dots and spaces which can cause issues
            safeName = safeName.Trim('.', ' ', '_'); // Also trim underscores here

            // Return "_" if the result is empty after all cleaning
            return string.IsNullOrWhiteSpace(safeName) ? "_" : safeName;
        }

        // Helper to remove control characters (Not directly used in CleanAndNormalizeNameForFile but potentially useful)
        private static string RemoveControlCharacters(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return new string(input.Where(c => !char.IsControl(c)).ToArray());
        }
    }
}