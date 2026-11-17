using System;
using System.Collections.Generic;

namespace HarryTools.TextEffects.Editor {
    /// <summary> Checks the markup of a text, alone or compared across locales. Works on plain strings, with no dependency on Localization </summary>
    public static class TextValidator {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string VoicePrefix = "Voice '";
        private const string VoiceSuffix = "' is not registered.";
        private const string NoParseOpen = "noparse";
        private const string NoParseClose = "</noparse>";
        #endregion

        // ==================== METHODS ====================
        #region Public Methods
        /// <summary> Adds the problems of one text: parser issues, unknown voices, events missing from the table and tags that look like misspelled presets. A null '_eventExists' skips the event check </summary>
        public static void ValidateText(string _text, ITagLookup _lookup, Func<string, bool> _eventExists, List<TextIssue> _into) {
            ParsedText parsed = TextParser.Parse(_text, _lookup);
            for (int i = 0; i < parsed.Issues.Count; i++) {
                TextIssue issue = parsed.Issues[i];
                if (IsUnregisteredVoice(issue)) {
                    string voice = issue.Message.Substring(VoicePrefix.Length, issue.Message.Length - VoicePrefix.Length - VoiceSuffix.Length);
                    _into.Add(new TextIssue(IssueSeverity.Error, $"Unknown voice '{voice}'.", issue.Position));
                    continue;
                }
                _into.Add(issue);
            }

            if (_eventExists != null) {
                int searchFrom = 0;
                for (int i = 0; i < parsed.Commands.Count; i++) {
                    TextCommand command = parsed.Commands[i];
                    if (command.Type != TextCommandType.Event) continue;
                    int position = FindEventPosition(parsed.Source, ref searchFrom);
                    if (!_eventExists(command.Name)) {
                        _into.Add(new TextIssue(IssueSeverity.Warning, $"Event '{command.Name}' has no entry in the event table.", position));
                    }
                }
            }

            AddUnknownPresetIssues(parsed.Source, _lookup, _into);
        }

        /// <summary> Adds the problems of every locale, plus the differences of each one against the base locale. Empty texts are not compared. A null '_eventExists' skips the event check </summary>
        public static void ValidateLocales(string _baseLocale, IReadOnlyDictionary<string, string> _textByLocale, ITagLookup _lookup, List<LocaleIssue> _into, Func<string, bool> _eventExists = null) {
            if (_textByLocale == null) return;

            List<TextIssue> issues = new List<TextIssue>();
            foreach (KeyValuePair<string, string> pair in _textByLocale) {
                if (string.IsNullOrWhiteSpace(pair.Value)) continue;
                issues.Clear();
                ValidateText(pair.Value, _lookup, _eventExists, issues);
                for (int i = 0; i < issues.Count; i++) _into.Add(new LocaleIssue(pair.Key, issues[i]));
            }

            if (_baseLocale == null || !_textByLocale.TryGetValue(_baseLocale, out string baseText) || string.IsNullOrWhiteSpace(baseText)) return;
            Summary baseSummary = Summarize(baseText, _lookup);

            foreach (KeyValuePair<string, string> pair in _textByLocale) {
                if (pair.Key == _baseLocale || string.IsNullOrWhiteSpace(pair.Value)) continue;
                Summary summary = Summarize(pair.Value, _lookup);

                if (!SameItems(baseSummary.Events, summary.Events)) {
                    _into.Add(new LocaleIssue(pair.Key, new TextIssue(IssueSeverity.Error,
                        $"Events differ from '{_baseLocale}': expected [{string.Join(", ", baseSummary.Events)}], found [{string.Join(", ", summary.Events)}].", 0)));
                }
                if (baseSummary.WaitInputs != summary.WaitInputs) {
                    _into.Add(new LocaleIssue(pair.Key, new TextIssue(IssueSeverity.Error,
                        $"<waitinput> count differs from '{_baseLocale}': expected {baseSummary.WaitInputs}, found {summary.WaitInputs}.", 0)));
                }
                if (baseSummary.Pauses != summary.Pauses) {
                    _into.Add(new LocaleIssue(pair.Key, new TextIssue(IssueSeverity.Warning,
                        $"<pause> count differs from '{_baseLocale}': expected {baseSummary.Pauses}, found {summary.Pauses}.", 0)));
                }
                if (!baseSummary.Presets.SetEquals(summary.Presets)) {
                    _into.Add(new LocaleIssue(pair.Key, new TextIssue(IssueSeverity.Warning,
                        $"Effect presets differ from '{_baseLocale}': expected [{string.Join(", ", baseSummary.Presets)}], found [{string.Join(", ", summary.Presets)}].", 0)));
                }
            }
        }
        #endregion

        #region Summary
        private readonly struct Summary {
            public readonly List<string> Events;
            public readonly int WaitInputs;
            public readonly int Pauses;
            public readonly HashSet<string> Presets;

            public Summary(List<string> _events, int _waitInputs, int _pauses, HashSet<string> _presets) {
                Events = _events;
                WaitInputs = _waitInputs;
                Pauses = _pauses;
                Presets = _presets;
            }
        }

        private static Summary Summarize(string _text, ITagLookup _lookup) {
            ParsedText parsed = TextParser.Parse(_text, _lookup);
            List<string> events = new List<string>();
            int waitInputs = 0;
            int pauses = 0;
            for (int i = 0; i < parsed.Commands.Count; i++) {
                TextCommand command = parsed.Commands[i];
                if (command.Type == TextCommandType.Event) events.Add(command.Name);
                else if (command.Type == TextCommandType.WaitInput) waitInputs++;
                else if (command.Type == TextCommandType.Pause) pauses++;
            }
            events.Sort(StringComparer.OrdinalIgnoreCase);

            HashSet<string> presets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < parsed.Spans.Count; i++) {
                if (parsed.Spans[i].Kind == SpanKind.Preset) presets.Add(parsed.Spans[i].TagName);
            }
            return new Summary(events, waitInputs, pauses, presets);
        }

        private static bool SameItems(List<string> _a, List<string> _b) {
            if (_a.Count != _b.Count) return false;
            for (int i = 0; i < _a.Count; i++) {
                if (!string.Equals(_a[i], _b[i], StringComparison.OrdinalIgnoreCase)) return false;
            }
            return true;
        }
        #endregion

        #region Helpers
        private static bool IsUnregisteredVoice(TextIssue _issue) {
            return _issue.Severity == IssueSeverity.Warning
                   && _issue.Message.StartsWith(VoicePrefix, StringComparison.Ordinal)
                   && _issue.Message.EndsWith(VoiceSuffix, StringComparison.Ordinal);
        }

        private static int FindEventPosition(string _source, ref int _searchFrom) {
            int index = _source.IndexOf("<event", _searchFrom, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return 0;
            _searchFrom = index + 1;
            return index;
        }

        /// <summary> Warns about tags that are not TextMeshPro's nor registered but are closed with '</>' or carry 'x=y' parameters, which only our presets do </summary>
        private static void AddUnknownPresetIssues(string _source, ITagLookup _lookup, List<TextIssue> _into) {
            bool hasGenericClose = _source.Contains("</>");
            int index = 0;
            while (index < _source.Length) {
                if (_source[index] != '<') {
                    index++;
                    continue;
                }
                int end = FindTagEnd(_source, index);
                if (end < 0) {
                    index++;
                    continue;
                }

                string content = _source.Substring(index + 1, end - index - 1);
                if (content.Trim().Equals(NoParseOpen, StringComparison.OrdinalIgnoreCase)) {
                    int close = _source.IndexOf(NoParseClose, end + 1, StringComparison.OrdinalIgnoreCase);
                    index = close < 0 ? _source.Length : close + NoParseClose.Length;
                    continue;
                }

                if (content.Length > 0 && content[0] != '/') CheckTag(_source, content, index, hasGenericClose, _lookup, _into);
                index = end + 1;
            }
        }

        private static void CheckTag(string _source, string _content, int _position, bool _hasGenericClose, ITagLookup _lookup, List<TextIssue> _into) {
            int nameEnd = 0;
            while (nameEnd < _content.Length && _content[nameEnd] != '=' && !char.IsWhiteSpace(_content[nameEnd])) nameEnd++;
            string name = _content.Substring(0, nameEnd);
            if (!ReservedTags.IsValidName(name) || ReservedTags.IsReserved(name)) return;
            if (_lookup != null && _lookup.IsPreset(name)) return;

            bool hasParams = _content.IndexOf('=', nameEnd) >= 0;
            bool closed = _hasGenericClose || _source.IndexOf("</" + name + ">", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!hasParams && !closed) return;
            _into.Add(new TextIssue(IssueSeverity.Warning, $"Unknown effect preset '{name}'.", _position));
        }

        private static int FindTagEnd(string _text, int _start) {
            for (int i = _start + 1; i < _text.Length; i++) {
                if (_text[i] == '>') return i;
                if (_text[i] == '<') return -1;
            }
            return -1;
        }
        #endregion
    }
}
