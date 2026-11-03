using System;
using System.Collections.Generic;
using UnityEngine;
using PrimeTween;
using UnityEngine.UI;

namespace HarryTools.UI
{
    [ExecuteAlways]
    public class ColoredGraphic : MonoBehaviour
    {
        [SerializeField] private Graphic _graphic;
        [Tooltip("Smoothly transition between colors when the palette changes.")]
        [SerializeField] private float _transitionDuration = 0.1f;
        [SerializeField] private ColorInfo _color = new ColorInfo
        {
            enabled = true,
            color = ColorType.White
        };
        [SerializeField] private List<ColorInfo> _coloredInfos = new();

        private IPaletteProvider _paletteProvider;
        private bool _colorsDirty;
#if UNITY_EDITOR
        private bool _editorApplyQueued;
#endif

        private Color[] _current;
        private Color[] _start;
        private Color[] _target;
        private bool[] _wasEnabled;
        private Tween _transitionTween;

        public float transitionDuration
        {
            get => _transitionDuration;
            set => _transitionDuration = Mathf.Max(0f, value);
        }

        public void SetColor(ColorInfo info) => SetColor(0, info);

        public void SetColor(int index, ColorInfo info)
        {
            if (_graphic is not IColored colored)
            {
                if (index != 0)
                    throw new ArgumentOutOfRangeException(nameof(index), index, "Target has a single color slot (index must be 0).");
                _color = info;
            }
            else
            {
                var keys = colored.keys;
                int slotCount = keys?.Length ?? 0;
                if (index < 0 || index >= slotCount)
                    throw new ArgumentOutOfRangeException(nameof(index), index, $"Target exposes {slotCount} color slots.");

                while (_coloredInfos.Count <= index)
                    _coloredInfos.Add(default);
                _coloredInfos[index] = info;
            }

            _colorsDirty = true;
#if UNITY_EDITOR
            QueueEditorApply();
#endif
        }

        private void OnValidate()
        {
            _colorsDirty = true;
#if UNITY_EDITOR
            QueueEditorApply();
#endif
        }

        private void OnEnable()
        {
            // Reset() solo se ejecuta en el editor; al añadir el componente en runtime hay que buscar el Graphic aquí
            if (!_graphic)
                _graphic = GetComponent<Graphic>();

            ResolveProvider();

            _colorsDirty = true;
            ApplyImmediate();
        }

        private void OnDisable()
        {
            if (_transitionTween.isAlive)
                _transitionTween.Complete();
            if (_paletteProvider != null)
                _paletteProvider.onColorChange -= ColorsUpdated;
            _paletteProvider = null;
        }

        private void ResolveProvider()
        {
            var found = GetComponentInParent<IPaletteProvider>(true);

            if (ReferenceEquals(found, _paletteProvider))
                return;

            if (_paletteProvider != null)
                _paletteProvider.onColorChange -= ColorsUpdated;

            _paletteProvider = found;

            if (_paletteProvider != null)
                _paletteProvider.onColorChange += ColorsUpdated;
        }

        private void Update()
        {
            if (!_graphic) return;

            if (_colorsDirty && _paletteProvider == null)
                ResolveProvider();

            if (!ActivePalette)
            {
                return;
            }

            if (_colorsDirty)
            {
                RefreshTargets(snap: _transitionDuration <= 0f || !Application.isPlaying);
                _colorsDirty = false;
            }
        }

        private ColorPalette ActivePalette => ColorPalette.Resolve(_paletteProvider);

        private void ApplyBlend(float t)
        {
            if (!_graphic || _current == null) return;

            var colored = _graphic as IColored;
            int slotCount = _current.Length;

            for (int i = 0; i < slotCount; i++)
            {
                if (!IsSlotEnabled(colored, i)) continue;
                _current[i] = Color.Lerp(_start[i], _target[i], t);
            }

            WriteCurrent(colored, slotCount);
        }

        private void ApplyImmediate()
        {
            ResolveProvider();

            if (!_graphic || !ActivePalette)
                return;

            RefreshTargets(snap: true);
            _colorsDirty = false;
        }

        public void Refresh()
        {
            _colorsDirty = true;
            ApplyImmediate();
        }

        private void RefreshTargets(bool snap)
        {
            int slotCount = ResolveSlotCount(out var colored);
            EnsureBuffers(slotCount);

            var palette = ActivePalette;

            for (int i = 0; i < slotCount; i++)
            {
                bool nowEnabled = IsSlotEnabled(colored, i);

                if (nowEnabled)
                {
                    _target[i] = GetSlotColor(colored, i, palette);

                    if (snap || !_wasEnabled[i])
                    {
                        _current[i] = _target[i];
                        _start[i] = _target[i];
                    }
                    else
                    {
                        _start[i] = _current[i];
                    }
                }

                _wasEnabled[i] = nowEnabled;
            }

            WriteCurrent(colored, slotCount);

            if (_transitionTween.isAlive)
                _transitionTween.Stop();

            if (!snap)
                _transitionTween = Tween.Custom(this, 0f, 1f, _transitionDuration, (target, t) => target.ApplyBlend(t), useUnscaledTime: true);
        }

        private int ResolveSlotCount(out IColored colored)
        {
            colored = _graphic as IColored;
            if (colored == null)
                return 1;
            var keys = colored.keys;
            return keys?.Length ?? 0;
        }

        private bool IsSlotEnabled(IColored colored, int i)
        {
            if (colored == null)
                return i == 0 && _color.enabled;
            return i < _coloredInfos.Count && _coloredInfos[i].enabled;
        }

        private Color GetSlotColor(IColored colored, int i, ColorPalette palette)
        {
            if (colored == null)
                return _color.GetColor(palette);
            return _coloredInfos[i].GetColor(palette);
        }

        private void EnsureBuffers(int slotCount)
        {
            if (_current != null && _current.Length == slotCount)
                return;

            Array.Resize(ref _current, slotCount);
            Array.Resize(ref _start, slotCount);
            Array.Resize(ref _target, slotCount);
            Array.Resize(ref _wasEnabled, slotCount);
        }

        private void WriteCurrent(IColored colored, int slotCount)
        {
            if (colored == null)
            {
                if (slotCount > 0 && _color.enabled && _graphic.color != _current[0])
                {
                    _graphic.color = _current[0];
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        UnityEditor.EditorUtility.SetDirty(_graphic);
#endif
                }
                return;
            }

            int count = Mathf.Min(_coloredInfos.Count, slotCount);
            bool anyChanged = false;
            for (int i = 0; i < count; i++)
            {
                if (_coloredInfos[i].enabled && colored.GetColor(i) != _current[i])
                {
                    colored.SetColor(i, _current[i]);
                    anyChanged = true;
                }
            }

#if UNITY_EDITOR
            if (!Application.isPlaying && anyChanged)
                UnityEditor.EditorUtility.SetDirty(_graphic);
#endif
        }

        private void ColorsUpdated()
        {
            _colorsDirty = true;
#if UNITY_EDITOR
            QueueEditorApply();
#endif
        }

        private void OnTransformParentChanged()
        {
            ResolveProvider();
            ColorsUpdated();
        }

        private void OnDidApplyAnimationProperties()
        {
            ColorsUpdated();
        }

        private void Reset()
        {
            _graphic = GetComponent<Graphic>();
        }

#if UNITY_EDITOR
        private void QueueEditorApply()
        {
            if (Application.isPlaying || _editorApplyQueued)
                return;

            _editorApplyQueued = true;
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            UnityEditor.EditorApplication.delayCall += () =>
            {
                _editorApplyQueued = false;
                if (!this || !isActiveAndEnabled) return;

                ApplyImmediate();
                UnityEditor.SceneView.RepaintAll();
            };
        }
#endif
    }
}
