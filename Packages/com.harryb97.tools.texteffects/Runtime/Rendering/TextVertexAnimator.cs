using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace HarryTools.TextEffects {
    /// <summary>
    /// Animates the vertices and colors of every character of a TMP_Text: appear, continuous, one-shot and disappear
    /// effects. Hidden characters have alpha 0 and are collapsed to their center
    /// </summary>
    internal sealed class TextVertexAnimator {
        // ==================== VARIABLES ===================
        #region Internal Variables
        internal readonly List<ResolvedSpan> ContinuousSpans = new List<ResolvedSpan>();
        internal readonly List<ResolvedSpan> OneShotSpans = new List<ResolvedSpan>();
        #endregion

        #region Private Variables
        private const float NotShown = -1f;

        private readonly Action<UnityEngine.Object> onTextChanged;
        private readonly List<TextIssue> cloneIssues = new List<TextIssue>();

        private TMP_Text text;
        private bool listening;
        private bool ignoreTextChanged;
        private bool loaded;

        private int characterCount;
        private float[] appearTimes = Array.Empty<float>();
        private float[] disappearTimes = Array.Empty<float>();
        private ResolvedSpan[] appearSpans = Array.Empty<ResolvedSpan>();
        private ResolvedSpan[] disappearSpans = Array.Empty<ResolvedSpan>();
        private ResolvedSpan defaultAppear;
        private ResolvedSpan defaultDisappear;

        private Vector3[][] originalVertices = Array.Empty<Vector3[]>();
        private Color32[][] originalColors = Array.Empty<Color32[]>();
        private int capturedMaterials;

        private bool meshDirty;
        private bool animatedLastRender;
        private bool continuousActive;
        private float appearEndTime;
        private float oneShotEndTime;
        private bool hiding;
        private float hideEndTime;
        private float lastRenderTime;
        #endregion

        // ==================== START ====================
        public TextVertexAnimator() {
            onTextChanged = OnTextChanged;
        }

        // ==================== METHODS ====================
        #region Binding
        /// <summary> Sets the text to animate. Keeps listening if it was </summary>
        public void Bind(TMP_Text _text) {
            if (ReferenceEquals(text, _text)) return;
            text = _text;
            loaded = false;
        }

        /// <summary> Starts recapturing the mesh whenever TextMeshPro regenerates it </summary>
        public void StartListening() {
            if (listening) return;
            listening = true;
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(onTextChanged);
        }

        public void StopListening() {
            if (!listening) return;
            listening = false;
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(onTextChanged);
        }
        #endregion

        #region Load
        /// <summary>
        /// Regenerates the TMP mesh, caches it and resolves the effects of every character. Allocates.
        /// '_defaultAppear' and '_defaultDisappear' are the component presets; when empty the settings defaults are used
        /// </summary>
        public void Load(ParsedText _parsed, TextEffectsSettings _settings, TextEffectPreset _defaultAppear, TextEffectPreset _defaultDisappear, List<TextIssue> _issues) {
            loaded = false;
            if (text == null) return;

            ignoreTextChanged = true;
            try {
                text.ForceMeshUpdate();
            } finally {
                ignoreTextChanged = false;
            }

            TMP_TextInfo _info = text.textInfo;
            characterCount = _info.characterCount;
            EnsureCharacterCapacity(characterCount);
            CaptureOriginals(_info);

            defaultAppear = ResolveDefault(_defaultAppear, _settings != null ? _settings.DefaultAppear : null, EffectKind.Appear, _issues);
            defaultDisappear = ResolveDefault(_defaultDisappear, _settings != null ? _settings.DefaultDisappear : null, EffectKind.Disappear, _issues);
            for (int _i = 0; _i < characterCount; _i++) {
                appearSpans[_i] = defaultAppear;
                disappearSpans[_i] = defaultDisappear;
            }

            ContinuousSpans.Clear();
            OneShotSpans.Clear();
            List<EffectSpan> _spans = _parsed.Spans;
            for (int _i = 0; _i < _spans.Count; _i++) {
                ResolveSpan(_spans[_i], _info, _settings, _issues);
            }

            loaded = true;
            HideAll();
        }

        private void ResolveSpan(EffectSpan _span, TMP_TextInfo _info, TextEffectsSettings _settings, List<TextIssue> _issues) {
            if (_span.Kind != SpanKind.Preset || _settings == null) return;
            if (!_settings.TryGetPreset(_span.TagName, out TextEffectPreset _preset) || _preset.Effect == null) return;

            int _first = FirstCharacterAtOrAfter(_info, _span.Start);
            int _end = FirstCharacterAtOrAfter(_info, _span.End);
            if (_end <= _first) return;

            cloneIssues.Clear();
            TextEffect _effect = _preset.Effect.CloneWithOverrides(_span.Overrides, cloneIssues);
            for (int _i = 0; _i < cloneIssues.Count; _i++) {
                TextIssue _issue = cloneIssues[_i];
                _issues?.Add(new TextIssue(_issue.Severity, "Tag '" + _span.TagName + "': " + _issue.Message, _issue.Position));
            }

            ResolvedSpan _resolved = new ResolvedSpan(_effect, _first, _end);
            switch (_effect.Kind) {
                case EffectKind.Appear:
                    // Spans are listed in opening order, so the last opened one wins
                    for (int _c = _first; _c < _end; _c++) appearSpans[_c] = _resolved;
                    break;
                case EffectKind.Disappear:
                    for (int _c = _first; _c < _end; _c++) disappearSpans[_c] = _resolved;
                    break;
                case EffectKind.Continuous:
                    ContinuousSpans.Add(_resolved);
                    break;
                case EffectKind.OneShot:
                    OneShotSpans.Add(_resolved);
                    break;
            }
        }

        /// <summary> Component preset, then settings preset, then none. A preset of the wrong kind is reported and skipped </summary>
        private ResolvedSpan ResolveDefault(TextEffectPreset _component, TextEffectPreset _project, EffectKind _kind, List<TextIssue> _issues) {
            TextEffect _effect = EffectOfKind(_component, _kind, _issues) ?? EffectOfKind(_project, _kind, _issues);
            return _effect != null ? new ResolvedSpan(_effect, 0, characterCount) : null;
        }

        private static TextEffect EffectOfKind(TextEffectPreset _preset, EffectKind _kind, List<TextIssue> _issues) {
            if (_preset == null || _preset.Effect == null) return null;
            if (_preset.Effect.Kind == _kind) return _preset.Effect;

            _issues?.Add(new TextIssue(IssueSeverity.Warning, "The preset '" + _preset.name + "' is used as " + _kind + " effect but its effect is " + _preset.Effect.Kind + ". It is ignored.", -1));
            return null;
        }

        private static int FirstCharacterAtOrAfter(TMP_TextInfo _info, int _position) {
            TMP_CharacterInfo[] _chars = _info.characterInfo;
            int _low = 0;
            int _high = _info.characterCount;
            while (_low < _high) {
                int _middle = (_low + _high) >> 1;
                if (_chars[_middle].index < _position) _low = _middle + 1;
                else _high = _middle;
            }
            return _low;
        }
        #endregion

        #region Character Timing
        /// <summary> Character '_index' became visible at '_time' </summary>
        public void OnCharacterShown(int _index, float _time) {
            if (!loaded || _index < 0 || _index >= characterCount) return;

            appearTimes[_index] = _time;
            disappearTimes[_index] = NotShown;
            meshDirty = true;

            ResolvedSpan _appear = appearSpans[_index];
            if (_appear != null) appearEndTime = Mathf.Max(appearEndTime, _time + _appear.Effect.Duration);

            for (int _i = 0; _i < ContinuousSpans.Count; _i++) {
                if (ContinuousSpans[_i].Contains(_index)) continuousActive = true;
            }

            for (int _i = 0; _i < OneShotSpans.Count; _i++) {
                ResolvedSpan _span = OneShotSpans[_i];
                if (_span.StartTime >= 0f || !_span.Contains(_index)) continue;
                _span.StartTime = _time;
                oneShotEndTime = Mathf.Max(oneShotEndTime, _time + _span.Effect.Duration);
            }
        }

        /// <summary> Every character back to hidden, with no pending animation </summary>
        public void HideAll() {
            for (int _i = 0; _i < characterCount; _i++) {
                appearTimes[_i] = NotShown;
                disappearTimes[_i] = NotShown;
            }
            for (int _i = 0; _i < OneShotSpans.Count; _i++) OneShotSpans[_i].StartTime = NotShown;

            continuousActive = false;
            appearEndTime = 0f;
            oneShotEndTime = 0f;
            hiding = false;
            meshDirty = true;
        }

        /// <summary> Starts the disappear animation of the visible characters. Hidden characters stay hidden </summary>
        public void BeginHide(HideMode _mode, HideOrder _order, float _delay, float _time) {
            hiding = true;
            hideEndTime = _time;
            meshDirty = true;
            if (!loaded) return;

            bool _anyEffect = false;
            for (int _i = 0; _i < characterCount && !_anyEffect; _i++) {
                if (appearTimes[_i] >= 0f && disappearSpans[_i] != null) _anyEffect = true;
            }

            // Without any disappear effect the text hides at once
            float _step = _anyEffect && _mode == HideMode.PerCharacter ? Mathf.Max(0f, _delay) : 0f;
            TMP_CharacterInfo[] _chars = text != null ? text.textInfo.characterInfo : null;
            int _slot = 0;
            for (int _n = 0; _n < characterCount; _n++) {
                int _i = _order == HideOrder.Forward ? _n : characterCount - 1 - _n;
                if (appearTimes[_i] < 0f) continue;

                float _start = _time + _slot * _step;
                disappearTimes[_i] = _start;
                // Characters without a quad (spaces) do not take a slot in the sequence
                if (_chars != null && _i < _chars.Length && _chars[_i].isVisible) _slot++;

                float _end = _start + (_anyEffect ? DurationOf(disappearSpans[_i]) : 0f);
                if (_end > hideEndTime) hideEndTime = _end;
            }
        }

        public bool IsHideFinished(float _time) {
            return !hiding || _time >= hideEndTime;
        }

        private static float DurationOf(ResolvedSpan _span) {
            return _span == null ? 0f : Mathf.Max(0f, _span.Effect.Duration);
        }
        #endregion

        #region Render
        /// <summary> Writes the animated characters into the TMP mesh. Does not touch it when nothing changed or animates </summary>
        public void Render(float _time) {
            lastRenderTime = _time;
            if (!loaded || text == null) return;

            bool _animating = IsAnimating(_time);
            // One more pass after the last animated frame writes the final state
            if (!meshDirty && !_animating && !animatedLastRender) return;
            meshDirty = false;
            animatedLastRender = _animating;

            TMP_TextInfo _info = text.textInfo;
            TMP_CharacterInfo[] _chars = _info.characterInfo;
            TMP_MeshInfo[] _meshes = _info.meshInfo;
            int _count = Mathf.Min(characterCount, _info.characterCount);

            for (int _c = 0; _c < _count; _c++) {
                ref TMP_CharacterInfo _charInfo = ref _chars[_c];
                if (!_charInfo.isVisible) continue;

                int _material = _charInfo.materialReferenceIndex;
                int _v = _charInfo.vertexIndex;
                if (_material >= capturedMaterials || _material >= _meshes.Length) continue;

                Vector3[] _srcVertices = originalVertices[_material];
                Color32[] _srcColors = originalColors[_material];
                Vector3[] _dstVertices = _meshes[_material].vertices;
                Color32[] _dstColors = _meshes[_material].colors32;
                if (_srcVertices == null || _srcColors == null || _dstVertices == null || _dstColors == null) continue;
                if (_v + 3 >= _srcVertices.Length || _v + 3 >= _srcColors.Length || _v + 3 >= _dstVertices.Length || _v + 3 >= _dstColors.Length) continue;

                CharacterState _state = new CharacterState {
                    BottomLeft = _srcVertices[_v],
                    TopLeft = _srcVertices[_v + 1],
                    TopRight = _srcVertices[_v + 2],
                    BottomRight = _srcVertices[_v + 3],
                    ColorBL = _srcColors[_v],
                    ColorTL = _srcColors[_v + 1],
                    ColorTR = _srcColors[_v + 2],
                    ColorBR = _srcColors[_v + 3],
                    Index = _c,
                    IndexInSpan = _c,
                    SpanLength = characterCount,
                    Size = Mathf.Abs(_charInfo.ascender - _charInfo.descender)
                };

                if (!Animate(ref _state, _c, _time)) Collapse(ref _state);

                _dstVertices[_v] = _state.BottomLeft;
                _dstVertices[_v + 1] = _state.TopLeft;
                _dstVertices[_v + 2] = _state.TopRight;
                _dstVertices[_v + 3] = _state.BottomRight;
                _dstColors[_v] = _state.ColorBL;
                _dstColors[_v + 1] = _state.ColorTL;
                _dstColors[_v + 2] = _state.ColorTR;
                _dstColors[_v + 3] = _state.ColorBR;
            }

            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
        }

        /// <summary> Applies appear, continuous, one-shot and disappear effects. Returns false when the character must be hidden </summary>
        private bool Animate(ref CharacterState _state, int _c, float _time) {
            float _shownAt = appearTimes[_c];
            if (_shownAt < 0f) return false;
            float _sinceShown = _time - _shownAt;

            ResolvedSpan _appear = appearSpans[_c];
            if (_appear != null) ApplyTimed(ref _state, _appear, _c, _sinceShown, _time);

            for (int _i = 0; _i < ContinuousSpans.Count; _i++) {
                ResolvedSpan _span = ContinuousSpans[_i];
                if (!_span.Contains(_c)) continue;
                SetSpan(ref _state, _span, _c);
                _span.Effect.Apply(ref _state, new EffectTime(_sinceShown, 0f, _time));
            }

            for (int _i = 0; _i < OneShotSpans.Count; _i++) {
                ResolvedSpan _span = OneShotSpans[_i];
                if (_span.StartTime < 0f || !_span.Contains(_c)) continue;
                ApplyTimed(ref _state, _span, _c, _time - _span.StartTime, _time);
            }

            float _hideAt = disappearTimes[_c];
            if (_hideAt < 0f || _time < _hideAt) return true;

            ResolvedSpan _disappear = disappearSpans[_c];
            float _sinceHide = _time - _hideAt;
            if (_disappear == null || _sinceHide >= _disappear.Effect.Duration) return false;
            ApplyTimed(ref _state, _disappear, _c, _sinceHide, _time);
            return true;
        }

        /// <summary> Applies an effect with a duration while it runs. A duration of zero or less counts as already finished </summary>
        private static void ApplyTimed(ref CharacterState _state, ResolvedSpan _span, int _c, float _elapsed, float _time) {
            float _duration = _span.Effect.Duration;
            if (_elapsed < 0f || _elapsed >= _duration) return;
            SetSpan(ref _state, _span, _c);
            _span.Effect.Apply(ref _state, new EffectTime(_elapsed, _elapsed / _duration, _time));
        }

        private static void SetSpan(ref CharacterState _state, ResolvedSpan _span, int _c) {
            _state.IndexInSpan = _c - _span.First;
            _state.SpanLength = _span.Length;
        }

        private static void Collapse(ref CharacterState _state) {
            Vector3 _center = _state.Center;
            _state.BottomLeft = _center;
            _state.TopLeft = _center;
            _state.TopRight = _center;
            _state.BottomRight = _center;
            _state.ColorBL.a = 0;
            _state.ColorTL.a = 0;
            _state.ColorTR.a = 0;
            _state.ColorBR.a = 0;
        }

        private bool IsAnimating(float _time) {
            return continuousActive || _time < appearEndTime || _time < oneShotEndTime || (hiding && _time < hideEndTime);
        }
        #endregion

        #region Mesh Cache
        /// <summary> Writes the mesh TextMeshPro generated back, without effects </summary>
        public void Restore() {
            if (!loaded || text == null) return;

            TMP_MeshInfo[] _meshes = text.textInfo.meshInfo;
            int _materials = Mathf.Min(capturedMaterials, _meshes.Length);
            for (int _m = 0; _m < _materials; _m++) {
                CopyInto(originalVertices[_m], _meshes[_m].vertices);
                CopyInto(originalColors[_m], _meshes[_m].colors32);
            }

            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
            meshDirty = true;
        }

        /// <summary> Caches the mesh again after TextMeshPro regenerated it, keeping the timing of every character </summary>
        public void Recapture() {
            if (!loaded || text == null) return;

            TMP_TextInfo _info = text.textInfo;
            int _count = _info.characterCount;
            if (_count != characterCount) {
                int _previous = characterCount;
                EnsureCharacterCapacity(_count);
                for (int _i = _previous; _i < _count; _i++) {
                    appearTimes[_i] = NotShown;
                    disappearTimes[_i] = NotShown;
                    appearSpans[_i] = defaultAppear;
                    disappearSpans[_i] = defaultDisappear;
                }
                characterCount = _count;
            }

            CaptureOriginals(_info);
            meshDirty = true;
            Render(lastRenderTime);
        }

        private void OnTextChanged(UnityEngine.Object _changed) {
            if (ignoreTextChanged || !ReferenceEquals(_changed, text)) return;
            Recapture();
        }

        private void CaptureOriginals(TMP_TextInfo _info) {
            TMP_MeshInfo[] _meshes = _info.meshInfo;
            int _materials = Mathf.Min(_info.materialCount, _meshes.Length);
            if (originalVertices.Length < _materials) {
                Array.Resize(ref originalVertices, _materials);
                Array.Resize(ref originalColors, _materials);
            }

            for (int _m = 0; _m < _materials; _m++) {
                CopyGrowing(_meshes[_m].vertices, ref originalVertices[_m]);
                CopyGrowing(_meshes[_m].colors32, ref originalColors[_m]);
            }
            capturedMaterials = _materials;
        }

        private static void CopyGrowing<T>(T[] _source, ref T[] _cache) {
            if (_source == null) return;
            if (_cache == null || _cache.Length < _source.Length) _cache = new T[_source.Length];
            Array.Copy(_source, _cache, _source.Length);
        }

        private static void CopyInto<T>(T[] _source, T[] _destination) {
            if (_source == null || _destination == null) return;
            Array.Copy(_source, _destination, Mathf.Min(_source.Length, _destination.Length));
        }

        private void EnsureCharacterCapacity(int _count) {
            if (appearTimes.Length >= _count) return;
            int _size = Mathf.Max(_count, appearTimes.Length * 2);
            Array.Resize(ref appearTimes, _size);
            Array.Resize(ref disappearTimes, _size);
            Array.Resize(ref appearSpans, _size);
            Array.Resize(ref disappearSpans, _size);
        }
        #endregion
    }
}
