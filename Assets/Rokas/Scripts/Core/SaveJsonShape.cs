using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Rokas.Core
{
    public static class SaveJsonShape
    {
        private static readonly string[] RequiredSaveFields =
        {
            "version", "yen", "reputation", "spiritAsh", "weaponLevel", "completedRuns",
            "phase", "activeContractId", "preparedFoodId", "enemyHp", "playerHp",
            "enemyTimer", "autoTimer", "clickTimer", "combatTime", "weakPointClaimed",
            "lampOn", "mameInteractions", "settings"
        };

        private static readonly string[] RequiredSettingsFields =
        {
            "masterVolume", "musicVolume", "sfxVolume", "screenShake",
            "glitchIntensity", "damageNumbers", "fullscreen"
        };

        public static bool TryReadVersion(string json, out int version, out string error)
        {
            HashSet<string> saveFields;
            HashSet<string> settingsFields;
            bool settingsIsObject;
            return TryInspect(json, out version, out saveFields, out settingsFields, out settingsIsObject, out error);
        }

        public static bool HasRequiredShape(string json, out string error)
        {
            int version;
            HashSet<string> saveFields;
            HashSet<string> settingsFields;
            bool settingsIsObject;
            if (!TryInspect(json, out version, out saveFields, out settingsFields, out settingsIsObject, out error))
            {
                return false;
            }

            if (!settingsIsObject)
            {
                error = "The settings field must contain an object.";
                return false;
            }

            string missing;
            if (!ContainsEvery(saveFields, RequiredSaveFields, out missing))
            {
                error = "Missing required save field '" + missing + "'.";
                return false;
            }
            if (!ContainsEvery(settingsFields, RequiredSettingsFields, out missing))
            {
                error = "Missing required settings field '" + missing + "'.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryInspect(
            string json,
            out int version,
            out HashSet<string> saveFields,
            out HashSet<string> settingsFields,
            out bool settingsIsObject,
            out string error)
        {
            version = 0;
            saveFields = new HashSet<string>(StringComparer.Ordinal);
            settingsFields = new HashSet<string>(StringComparer.Ordinal);
            settingsIsObject = false;
            if (json == null)
            {
                error = "JSON text is null.";
                return false;
            }

            Parser parser = new Parser(json);
            if (!parser.ParseSaveObject(saveFields, settingsFields, out settingsIsObject, out version, out error))
            {
                return false;
            }
            parser.SkipWhitespace();
            if (!parser.AtEnd)
            {
                error = "Unexpected content after the save object.";
                return false;
            }
            if (!saveFields.Contains("version"))
            {
                error = "Missing required save field 'version'.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool ContainsEvery(HashSet<string> actual, string[] required, out string missing)
        {
            for (int index = 0; index < required.Length; index++)
            {
                if (!actual.Contains(required[index]))
                {
                    missing = required[index];
                    return false;
                }
            }

            missing = string.Empty;
            return true;
        }

        private sealed class Parser
        {
            private const int MaxDepth = 64;
            private readonly string text;
            private int position;

            public bool AtEnd
            {
                get { return position >= text.Length; }
            }

            public Parser(string text)
            {
                this.text = text;
            }

            public bool ParseSaveObject(
                HashSet<string> saveFields,
                HashSet<string> settingsFields,
                out bool settingsIsObject,
                out int version,
                out string error)
            {
                settingsIsObject = false;
                version = 0;
                SkipWhitespace();
                if (!Take('{'))
                {
                    error = "Save JSON root must be an object.";
                    return false;
                }

                SkipWhitespace();
                if (Take('}'))
                {
                    error = string.Empty;
                    return true;
                }

                while (true)
                {
                    string key;
                    if (!ParseString(out key, out error))
                    {
                        return false;
                    }
                    if (!saveFields.Add(key))
                    {
                        error = "Duplicate top-level field '" + key + "'.";
                        return false;
                    }
                    SkipWhitespace();
                    if (!Take(':'))
                    {
                        error = "Expected ':' after top-level field '" + key + "'.";
                        return false;
                    }
                    SkipWhitespace();

                    if (key == "version")
                    {
                        string number;
                        if (!ParseNumber(out number, out error) ||
                            !int.TryParse(number, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out version))
                        {
                            error = "The version field must be a JSON integer in Int32 range.";
                            return false;
                        }
                    }
                    else if (key == "settings" && Peek('{'))
                    {
                        settingsIsObject = true;
                        if (!ParseSettingsObject(settingsFields, out error))
                        {
                            return false;
                        }
                    }
                    else if (!ParseValue(1, out error))
                    {
                        return false;
                    }

                    SkipWhitespace();
                    if (Take('}'))
                    {
                        error = string.Empty;
                        return true;
                    }
                    if (!Take(','))
                    {
                        error = "Expected ',' or '}' in the save object.";
                        return false;
                    }
                    SkipWhitespace();
                }
            }

            public void SkipWhitespace()
            {
                while (!AtEnd)
                {
                    char value = text[position];
                    if (value != ' ' && value != '\t' && value != '\r' && value != '\n')
                    {
                        break;
                    }
                    position++;
                }
            }

            private bool ParseSettingsObject(HashSet<string> settingsFields, out string error)
            {
                if (!Take('{'))
                {
                    error = "The settings field must contain an object.";
                    return false;
                }
                SkipWhitespace();
                if (Take('}'))
                {
                    error = string.Empty;
                    return true;
                }

                while (true)
                {
                    string key;
                    if (!ParseString(out key, out error))
                    {
                        return false;
                    }
                    if (!settingsFields.Add(key))
                    {
                        error = "Duplicate settings field '" + key + "'.";
                        return false;
                    }
                    SkipWhitespace();
                    if (!Take(':'))
                    {
                        error = "Expected ':' after settings field '" + key + "'.";
                        return false;
                    }
                    SkipWhitespace();
                    if (!ParseValue(2, out error))
                    {
                        return false;
                    }
                    SkipWhitespace();
                    if (Take('}'))
                    {
                        error = string.Empty;
                        return true;
                    }
                    if (!Take(','))
                    {
                        error = "Expected ',' or '}' in settings.";
                        return false;
                    }
                    SkipWhitespace();
                }
            }

            private bool ParseValue(int depth, out string error)
            {
                if (depth > MaxDepth)
                {
                    error = "JSON nesting exceeds the supported depth.";
                    return false;
                }
                SkipWhitespace();
                if (AtEnd)
                {
                    error = "Unexpected end of JSON value.";
                    return false;
                }

                char value = text[position];
                if (value == '"')
                {
                    string ignored;
                    return ParseString(out ignored, out error);
                }
                if (value == '{')
                {
                    return ParseObject(depth, out error);
                }
                if (value == '[')
                {
                    return ParseArray(depth, out error);
                }
                if (value == 't')
                {
                    return ParseLiteral("true", out error);
                }
                if (value == 'f')
                {
                    return ParseLiteral("false", out error);
                }
                if (value == 'n')
                {
                    return ParseLiteral("null", out error);
                }
                if (value == '-' || (value >= '0' && value <= '9'))
                {
                    string ignored;
                    return ParseNumber(out ignored, out error);
                }

                error = "Unexpected JSON value at character " + position + ".";
                return false;
            }

            private bool ParseObject(int depth, out string error)
            {
                Take('{');
                SkipWhitespace();
                if (Take('}'))
                {
                    error = string.Empty;
                    return true;
                }
                while (true)
                {
                    string ignored;
                    if (!ParseString(out ignored, out error))
                    {
                        return false;
                    }
                    SkipWhitespace();
                    if (!Take(':'))
                    {
                        error = "Expected ':' in object.";
                        return false;
                    }
                    if (!ParseValue(depth + 1, out error))
                    {
                        return false;
                    }
                    SkipWhitespace();
                    if (Take('}'))
                    {
                        error = string.Empty;
                        return true;
                    }
                    if (!Take(','))
                    {
                        error = "Expected ',' or '}' in object.";
                        return false;
                    }
                    SkipWhitespace();
                }
            }

            private bool ParseArray(int depth, out string error)
            {
                Take('[');
                SkipWhitespace();
                if (Take(']'))
                {
                    error = string.Empty;
                    return true;
                }
                while (true)
                {
                    if (!ParseValue(depth + 1, out error))
                    {
                        return false;
                    }
                    SkipWhitespace();
                    if (Take(']'))
                    {
                        error = string.Empty;
                        return true;
                    }
                    if (!Take(','))
                    {
                        error = "Expected ',' or ']' in array.";
                        return false;
                    }
                    SkipWhitespace();
                }
            }

            private bool ParseString(out string value, out string error)
            {
                value = string.Empty;
                if (!Take('"'))
                {
                    error = "Expected a JSON string at character " + position + ".";
                    return false;
                }

                StringBuilder builder = new StringBuilder();
                while (!AtEnd)
                {
                    char current = text[position++];
                    if (current == '"')
                    {
                        value = builder.ToString();
                        error = string.Empty;
                        return true;
                    }
                    if (current < ' ')
                    {
                        error = "Unescaped control character in JSON string.";
                        return false;
                    }
                    if (current != '\\')
                    {
                        builder.Append(current);
                        continue;
                    }
                    if (AtEnd)
                    {
                        error = "Incomplete JSON string escape.";
                        return false;
                    }

                    char escape = text[position++];
                    switch (escape)
                    {
                        case '"': builder.Append('"'); break;
                        case '\\': builder.Append('\\'); break;
                        case '/': builder.Append('/'); break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'n': builder.Append('\n'); break;
                        case 'r': builder.Append('\r'); break;
                        case 't': builder.Append('\t'); break;
                        case 'u':
                            int code;
                            if (!ParseHex4(out code))
                            {
                                error = "Invalid Unicode escape in JSON string.";
                                return false;
                            }
                            builder.Append((char)code);
                            break;
                        default:
                            error = "Invalid JSON string escape.";
                            return false;
                    }
                }

                error = "Unterminated JSON string.";
                return false;
            }

            private bool ParseHex4(out int code)
            {
                code = 0;
                if (position + 4 > text.Length)
                {
                    return false;
                }
                for (int index = 0; index < 4; index++)
                {
                    char current = text[position++];
                    int digit;
                    if (current >= '0' && current <= '9') digit = current - '0';
                    else if (current >= 'a' && current <= 'f') digit = current - 'a' + 10;
                    else if (current >= 'A' && current <= 'F') digit = current - 'A' + 10;
                    else return false;
                    code = code * 16 + digit;
                }
                return true;
            }

            private bool ParseNumber(out string number, out string error)
            {
                int start = position;
                if (Take('-') && AtEnd)
                {
                    number = string.Empty;
                    error = "Incomplete JSON number.";
                    return false;
                }
                if (Take('0'))
                {
                    if (!AtEnd && text[position] >= '0' && text[position] <= '9')
                    {
                        number = string.Empty;
                        error = "JSON numbers cannot contain leading zeroes.";
                        return false;
                    }
                }
                else if (!TakeDigits())
                {
                    number = string.Empty;
                    error = "Invalid JSON number.";
                    return false;
                }
                if (Take('.'))
                {
                    if (!TakeDigits())
                    {
                        number = string.Empty;
                        error = "JSON fraction requires digits.";
                        return false;
                    }
                }
                if (Take('e') || Take('E'))
                {
                    if (!Take('+'))
                    {
                        Take('-');
                    }
                    if (!TakeDigits())
                    {
                        number = string.Empty;
                        error = "JSON exponent requires digits.";
                        return false;
                    }
                }

                number = text.Substring(start, position - start);
                error = string.Empty;
                return true;
            }

            private bool TakeDigits()
            {
                int start = position;
                while (!AtEnd && text[position] >= '0' && text[position] <= '9')
                {
                    position++;
                }
                return position > start;
            }

            private bool ParseLiteral(string literal, out string error)
            {
                if (position + literal.Length <= text.Length &&
                    string.CompareOrdinal(text, position, literal, 0, literal.Length) == 0)
                {
                    position += literal.Length;
                    error = string.Empty;
                    return true;
                }
                error = "Invalid JSON literal at character " + position + ".";
                return false;
            }

            private bool Peek(char expected)
            {
                return !AtEnd && text[position] == expected;
            }

            private bool Take(char expected)
            {
                if (AtEnd || text[position] != expected)
                {
                    return false;
                }
                position++;
                return true;
            }
        }
    }
}
