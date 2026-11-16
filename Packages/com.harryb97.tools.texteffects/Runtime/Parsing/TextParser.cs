using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarryTools.DomainReload;

namespace HarryTools.TextEffects {
    /// <summary>
    /// Single pass parser of the text markup. It consumes only the tags it recognizes (pause, speed, waitinput,
    /// event, voice, instant and registered preset names) and leaves everything else for TextMeshPro
    /// </summary>
    public static class TextParser {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string NoParseClose = "</noparse>";

        [ClearOnReload(AssignNewInstance = true)] private static StringBuilder builder = new StringBuilder(256);
        [ClearOnReload(AssignNewInstance = true)] private static List<EffectSpan> openSpans = new List<EffectSpan>();
        #endregion

        // ==================== METHODS ====================
        #region Public Methods
        public static ParsedText Parse(string _text, ITagLookup _lookup) {
            ParsedText result = new ParsedText();
            Parse(_text, _lookup, result);
            return result;
        }

        public static void Parse(string _text, ITagLookup _lookup, ParsedText _into) {
            _into.Clear();
            string text = _text ?? string.Empty;
            _into.Source = text;
            builder.Clear();
            openSpans.Clear();

            int index = 0;
            while (index < text.Length) {
                char character = text[index];
                if (character != '<') {
                    builder.Append(character);
                    index++;
                    continue;
                }

                int tagEnd = FindTagEnd(text, index);
                if (tagEnd < 0) {
                    builder.Append(character);
                    index++;
                    continue;
                }

                ReadOnlySpan<char> content = text.AsSpan(index + 1, tagEnd - index - 1);
                if (IsNoParseOpen(content)) {
                    int closeIndex = text.IndexOf(NoParseClose, tagEnd + 1, StringComparison.OrdinalIgnoreCase);
                    int copyEnd = closeIndex < 0 ? text.Length : closeIndex + NoParseClose.Length;
                    builder.Append(text, index, copyEnd - index);
                    index = copyEnd;
                    continue;
                }

                if (HandleTag(content, index, _lookup, _into)) {
                    index = tagEnd + 1;
                    continue;
                }

                builder.Append(text, index, tagEnd - index + 1);
                index = tagEnd + 1;
            }

            for (int i = 0; i < openSpans.Count; i++) openSpans[i].End = builder.Length;
            openSpans.Clear();
            _into.TmpText = builder.ToString();
            builder.Clear();
        }
        #endregion

        #region Tag Handling
        /// <summary> Returns true when the tag is ours and must be removed from the text </summary>
        private static bool HandleTag(ReadOnlySpan<char> _content, int _position, ITagLookup _lookup, ParsedText _into) {
            if (_content.Length == 0) return false;
            if (_content[0] == '/') return HandleClose(_content.Slice(1).Trim(), _position, _lookup, _into);

            int nameEnd = 0;
            while (nameEnd < _content.Length && _content[nameEnd] != '=' && !char.IsWhiteSpace(_content[nameEnd])) nameEnd++;
            ReadOnlySpan<char> name = _content.Slice(0, nameEnd);
            if (!ReservedTags.IsValidName(name)) return false;
            ReadOnlySpan<char> rest = _content.Slice(nameEnd);

            if (name.Equals("pause", StringComparison.OrdinalIgnoreCase)) return HandlePause(rest, _position, _into);
            if (name.Equals("speed", StringComparison.OrdinalIgnoreCase)) return HandleSpeed(rest, _position, _into);
            if (name.Equals("waitinput", StringComparison.OrdinalIgnoreCase)) {
                _into.Commands.Add(new TextCommand(TextCommandType.WaitInput, builder.Length, 0f, null, -1));
                return true;
            }
            if (name.Equals("event", StringComparison.OrdinalIgnoreCase)) return HandleEvent(rest, _position, _into);
            if (name.Equals("voice", StringComparison.OrdinalIgnoreCase)) return HandleVoice(rest, _position, _lookup, _into);
            if (name.Equals("instant", StringComparison.OrdinalIgnoreCase)) {
                openSpans.Add(NewSpan(SpanKind.Instant, name, null, _into));
                return true;
            }

            string tagName = name.ToString();
            if (ReservedTags.IsReserved(tagName)) return false;
            if (_lookup == null || !_lookup.IsPreset(tagName)) return false;
            if (!TryParseParams(rest, out IReadOnlyList<EffectParam> overrides, out string error)) {
                _into.Issues.Add(new TextIssue(IssueSeverity.Error, $"Malformed tag <{tagName}>: {error}.", _position));
                return true;
            }
            openSpans.Add(NewSpan(SpanKind.Preset, name, overrides, _into));
            return true;
        }

        private static bool HandleClose(ReadOnlySpan<char> _name, int _position, ITagLookup _lookup, ParsedText _into) {
            if (_name.Length == 0) {
                if (openSpans.Count == 0) {
                    _into.Issues.Add(new TextIssue(IssueSeverity.Warning, "Closing tag </> has no open range to close.", _position));
                    return true;
                }
                CloseDownTo(openSpans.Count - 1);
                return true;
            }

            string name = _name.ToString();
            bool isOurs = string.Equals(name, "instant", StringComparison.OrdinalIgnoreCase)
                          || (!ReservedTags.IsReserved(name) && _lookup != null && _lookup.IsPreset(name));
            if (!isOurs) return false;

            for (int i = openSpans.Count - 1; i >= 0; i--) {
                if (!string.Equals(openSpans[i].TagName, name, StringComparison.OrdinalIgnoreCase)) continue;
                CloseDownTo(i);
                return true;
            }
            _into.Issues.Add(new TextIssue(IssueSeverity.Warning, $"Closing tag </{name}> has no open range to close.", _position));
            return true;
        }

        private static void CloseDownTo(int _index) {
            for (int i = openSpans.Count - 1; i >= _index; i--) {
                openSpans[i].End = builder.Length;
                openSpans.RemoveAt(i);
            }
        }

        private static EffectSpan NewSpan(SpanKind _kind, ReadOnlySpan<char> _name, IReadOnlyList<EffectParam> _overrides, ParsedText _into) {
            EffectSpan span = new EffectSpan(_kind, _name.ToString(), builder.Length, _overrides ?? Array.Empty<EffectParam>());
            _into.Spans.Add(span);
            return span;
        }

        private static bool HandlePause(ReadOnlySpan<char> _rest, int _position, ParsedText _into) {
            if (!TryGetValue(_rest, out ReadOnlySpan<char> value) || !TryParseFloat(value, out float seconds) || seconds < 0f) {
                _into.Issues.Add(new TextIssue(IssueSeverity.Error, "Tag <pause> needs a number of seconds greater than or equal to 0.", _position));
                return true;
            }
            _into.Commands.Add(new TextCommand(TextCommandType.Pause, builder.Length, seconds, null, -1));
            return true;
        }

        private static bool HandleSpeed(ReadOnlySpan<char> _rest, int _position, ParsedText _into) {
            if (!TryGetValue(_rest, out ReadOnlySpan<char> value) || !TryParseFloat(value, out float multiplier) || multiplier <= 0f) {
                _into.Issues.Add(new TextIssue(IssueSeverity.Error, "Tag <speed> needs a number greater than 0.", _position));
                return true;
            }
            _into.Commands.Add(new TextCommand(TextCommandType.Speed, builder.Length, multiplier, null, -1));
            return true;
        }

        private static bool HandleEvent(ReadOnlySpan<char> _rest, int _position, ParsedText _into) {
            if (!TryGetValue(_rest, out ReadOnlySpan<char> value) || !ReservedTags.IsValidName(value)) {
                _into.Issues.Add(new TextIssue(IssueSeverity.Error, "Tag <event> needs a name made of letters, digits, '_' or '-'.", _position));
                return true;
            }
            _into.Commands.Add(new TextCommand(TextCommandType.Event, builder.Length, 0f, value.ToString(), _into.EventCount));
            _into.EventCount++;
            return true;
        }

        private static bool HandleVoice(ReadOnlySpan<char> _rest, int _position, ITagLookup _lookup, ParsedText _into) {
            if (!TryGetValue(_rest, out ReadOnlySpan<char> value) || !ReservedTags.IsValidName(value)) {
                _into.Issues.Add(new TextIssue(IssueSeverity.Error, "Tag <voice> needs a name made of letters, digits, '_' or '-'.", _position));
                return true;
            }
            string voiceName = value.ToString();
            if (_lookup == null || !_lookup.IsVoice(voiceName)) {
                _into.Issues.Add(new TextIssue(IssueSeverity.Warning, $"Voice '{voiceName}' is not registered.", _position));
            }
            _into.Commands.Add(new TextCommand(TextCommandType.Voice, builder.Length, 0f, voiceName, -1));
            return true;
        }
        #endregion

        #region Helpers
        /// <summary> Index of the '>' that closes the tag opened at '_start', or -1 if another '<' or the end comes first </summary>
        private static int FindTagEnd(string _text, int _start) {
            for (int i = _start + 1; i < _text.Length; i++) {
                if (_text[i] == '>') return i;
                if (_text[i] == '<') return -1;
            }
            return -1;
        }

        private static bool IsNoParseOpen(ReadOnlySpan<char> _content) {
            return _content.Trim().Equals("noparse", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary> Reads the value of '=value' (optionally in double quotes). False if there is no '=' or the value is empty </summary>
        private static bool TryGetValue(ReadOnlySpan<char> _rest, out ReadOnlySpan<char> _value) {
            _value = default;
            if (_rest.Length == 0 || _rest[0] != '=') return false;
            ReadOnlySpan<char> value = _rest.Slice(1).Trim();
            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"') value = value.Slice(1, value.Length - 2).Trim();
            if (value.Length == 0) return false;
            _value = value;
            return true;
        }

        private static bool TryParseFloat(ReadOnlySpan<char> _value, out float _result) {
            return float.TryParse(_value, NumberStyles.Float, CultureInfo.InvariantCulture, out _result)
                   && !float.IsNaN(_result) && !float.IsInfinity(_result);
        }

        private static bool TryParseParams(ReadOnlySpan<char> _rest, out IReadOnlyList<EffectParam> _overrides, out string _error) {
            _overrides = null;
            _error = null;
            List<EffectParam> list = null;
            int position = 0;

            while (true) {
                while (position < _rest.Length && char.IsWhiteSpace(_rest[position])) position++;
                if (position >= _rest.Length) break;

                int keyStart = position;
                while (position < _rest.Length && _rest[position] != '=' && !char.IsWhiteSpace(_rest[position])) position++;
                ReadOnlySpan<char> key = _rest.Slice(keyStart, position - keyStart);
                if (!ReservedTags.IsValidName(key)) {
                    _error = "invalid parameter name";
                    return false;
                }
                if (position >= _rest.Length || _rest[position] != '=') {
                    _error = $"parameter '{key.ToString()}' has no value";
                    return false;
                }
                position++;

                ReadOnlySpan<char> value;
                if (position < _rest.Length && _rest[position] == '"') {
                    int close = _rest.Slice(position + 1).IndexOf('"');
                    if (close < 0) {
                        _error = $"parameter '{key.ToString()}' has an unclosed quote";
                        return false;
                    }
                    value = _rest.Slice(position + 1, close);
                    position += close + 2;
                } else {
                    int valueStart = position;
                    while (position < _rest.Length && !char.IsWhiteSpace(_rest[position])) position++;
                    value = _rest.Slice(valueStart, position - valueStart);
                    if (value.Length == 0) {
                        _error = $"parameter '{key.ToString()}' has no value";
                        return false;
                    }
                }

                if (list == null) list = new List<EffectParam>();
                list.Add(new EffectParam(key.ToString(), value.ToString()));
            }

            _overrides = list;
            return true;
        }
        #endregion
    }
}
