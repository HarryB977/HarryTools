using System;
using UnityEngine;
using UnityEngine.UI;

namespace HarryTools.UI
{
    public enum GradientType
    {
        None,
        Vertical,
        Horizontal,
        Radial,
        Gradient
    }

    public enum RadialMode
    {
        CircleCover,
        CircleFit,
        Ellipse
    }

    public enum FramePlacement
    {
        Inside,
        Center,
        Outside
    }

    /// <summary> Dirección del degradado del contorno: del color del contorno al color final </summary>
    public enum OutlineGradientType
    {
        None,
        Vertical,
        Horizontal,
        Angle
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public class SignedDistanceFieldGraphic : MaskableGraphic, IColored
    {
        const string SHADER_NAME = "Hidden/HarryTools/UI/RectangleRenderer";

        [SerializeField, HideInInspector] Shader _shader;

        [SerializeField] Texture _texture;
        [SerializeField] Color _graphicColor = Color.white;
        [SerializeField] GradientType _gradientType;
        [SerializeField] Color _gradientColor = Color.white;
        [SerializeField, Range(2, 20)] int _gradientQuality = 8;
        [SerializeField] RadialMode _radialMode;
        [SerializeField, Min(0.01f)] float _gradientSize = 1f;
        [SerializeField] Gradient _gradient;
        [SerializeField, Range(0f, 360f)] float _gradientAngle;

        [SerializeField] bool _noFill;
        [SerializeField, Min(0f)] float _frameWidth = 5f;
        [SerializeField] FramePlacement _framePlacement;

        [SerializeField, Min(0f)] float _outlineSize;
        [SerializeField] Color _outlineColor = Color.black;

        [SerializeField] OutlineGradientType _outlineGradientType;
        [SerializeField] Color _outlineGradientColor = Color.white;
        [SerializeField, Range(0f, 360f)] float _outlineGradientAngle;

        [SerializeField, Min(0f)] float _shadowSize;
        [SerializeField, Min(0f)] float _shadowBlur;
        [SerializeField, Min(0f)] float _shadowPower = 1f;
        [SerializeField] Color _shadowColor = Color.black;
        [SerializeField] Vector2 _shadowOffset;

        [SerializeField, Min(0f)] float _innerShadowSize;
        [SerializeField, Min(0f)] float _innerShadowBlur;
        [SerializeField] Vector2 _innerShadowOffset;
        [SerializeField] Color _innerShadowColor = new Color(0f, 0f, 0f, 0.5f);

        [SerializeField, Min(0f)] float _embossSize;
        [SerializeField, Range(0f, 360f)] float _embossAngle = 135f;
        [SerializeField, Range(0f, 1f)] float _embossStrength = 0.5f;
        [SerializeField] Color _embossHighlightColor = Color.white;
        [SerializeField] Color _embossShadowColor = Color.black;

        static Material _sharedMaterial;
        public const AdditionalCanvasShaderChannels RequiredCanvasChannels =
            AdditionalCanvasShaderChannels.TexCoord1
            | AdditionalCanvasShaderChannels.TexCoord2
            | AdditionalCanvasShaderChannels.TexCoord3
            | AdditionalCanvasShaderChannels.Normal
            | AdditionalCanvasShaderChannels.Tangent;
#if UNITY_EDITOR
        // Graphics waiting for an editor rebuild. All of them are refreshed in ONE delayCall followed by
        // ONE Canvas.ForceUpdateCanvases(), instead of a forced rebuild of every canvas per graphic.
        static readonly System.Collections.Generic.HashSet<SignedDistanceFieldGraphic> s_PendingEditorRebuilds =
            new System.Collections.Generic.HashSet<SignedDistanceFieldGraphic>();
        static bool s_EditorFlushQueued;
#endif

        public Texture texture
        {
            get => _texture;
            set { if (_texture == value) return; _texture = value; SetVerticesDirty(); SetMaterialDirty(); }
        }

        public Color graphicColor
        {
            get => _graphicColor;
            set { if (_graphicColor == value) return; _graphicColor = value; SetVerticesDirty(); }
        }

        public GradientType gradientType
        {
            get => _gradientType;
            set { if (_gradientType == value) return; _gradientType = value; SetVerticesDirty(); }
        }

        public Color gradientColor
        {
            get => _gradientColor;
            set { if (_gradientColor == value) return; _gradientColor = value; SetVerticesDirty(); }
        }

        public int gradientQuality
        {
            get => _gradientQuality;
            set { value = Mathf.Clamp(value, 2, 20); if (_gradientQuality == value) return; _gradientQuality = value; SetVerticesDirty(); }
        }

        public RadialMode radialMode
        {
            get => _radialMode;
            set { if (_radialMode == value) return; _radialMode = value; SetVerticesDirty(); }
        }

        public float gradientSize
        {
            get => _gradientSize;
            set { value = Mathf.Max(0.01f, value); if (Mathf.Approximately(_gradientSize, value)) return; _gradientSize = value; SetVerticesDirty(); }
        }

        public Gradient gradient
        {
            get => _gradient;
            set { _gradient = value; SetVerticesDirty(); }
        }

        public float gradientAngle
        {
            get => _gradientAngle;
            set { if (Mathf.Approximately(_gradientAngle, value)) return; _gradientAngle = value; SetVerticesDirty(); }
        }

        public bool noFill
        {
            get => _noFill;
            set { if (_noFill == value) return; _noFill = value; SetVerticesDirty(); }
        }

        public float frameWidth
        {
            get => _frameWidth;
            set { value = Mathf.Max(0f, value); if (Mathf.Approximately(_frameWidth, value)) return; _frameWidth = value; SetVerticesDirty(); }
        }

        public FramePlacement framePlacement
        {
            get => _framePlacement;
            set { if (_framePlacement == value) return; _framePlacement = value; SetVerticesDirty(); }
        }

        public float outlineSize
        {
            get => _outlineSize;
            set { if (Mathf.Approximately(_outlineSize, value)) return; _outlineSize = value; SetVerticesDirty(); }
        }

        public Color outlineColor
        {
            get => _outlineColor;
            set { if (_outlineColor == value) return; _outlineColor = value; SetVerticesDirty(); }
        }

        public OutlineGradientType outlineGradientType
        {
            get => _outlineGradientType;
            set { if (_outlineGradientType == value) return; _outlineGradientType = value; SetAllDirty(); }
        }

        /// <summary> Color final del degradado del contorno (el inicial es <see cref="outlineColor"/>) </summary>
        public Color outlineGradientColor
        {
            get => _outlineGradientColor;
            set { if (_outlineGradientColor == value) return; _outlineGradientColor = value; SetMaterialDirty(); }
        }

        public float outlineGradientAngle
        {
            get => _outlineGradientAngle;
            set { if (Mathf.Approximately(_outlineGradientAngle, value)) return; _outlineGradientAngle = value; SetMaterialDirty(); }
        }

        /// <summary> Desplazamiento de la sombra exterior, en píxeles </summary>
        public Vector2 shadowOffset
        {
            get => _shadowOffset;
            set { if (_shadowOffset == value) return; _shadowOffset = value; SetAllDirty(); }
        }

        public float innerShadowSize
        {
            get => _innerShadowSize;
            set { value = Mathf.Max(0f, value); if (Mathf.Approximately(_innerShadowSize, value)) return; _innerShadowSize = value; SetAllDirty(); }
        }

        public float innerShadowBlur
        {
            get => _innerShadowBlur;
            set { value = Mathf.Max(0f, value); if (Mathf.Approximately(_innerShadowBlur, value)) return; _innerShadowBlur = value; SetAllDirty(); }
        }

        /// <summary> Desplazamiento de la sombra interior, en píxeles </summary>
        public Vector2 innerShadowOffset
        {
            get => _innerShadowOffset;
            set { if (_innerShadowOffset == value) return; _innerShadowOffset = value; SetMaterialDirty(); }
        }

        public Color innerShadowColor
        {
            get => _innerShadowColor;
            set { if (_innerShadowColor == value) return; _innerShadowColor = value; SetMaterialDirty(); }
        }

        public float shadowSize
        {
            get => _shadowSize;
            set { if (Mathf.Approximately(_shadowSize, value)) return; _shadowSize = value; SetVerticesDirty(); }
        }

        public float shadowBlur
        {
            get => _shadowBlur;
            set { if (Mathf.Approximately(_shadowBlur, value)) return; _shadowBlur = value; SetVerticesDirty(); }
        }

        public float shadowPower
        {
            get => _shadowPower;
            set { if (Mathf.Approximately(_shadowPower, value)) return; _shadowPower = value; SetVerticesDirty(); }
        }

        public Color shadowColor
        {
            get => _shadowColor;
            set { if (_shadowColor == value) return; _shadowColor = value; SetVerticesDirty(); }
        }

        public float embossSize
        {
            get => _embossSize;
            set { if (Mathf.Approximately(_embossSize, value)) return; _embossSize = value; SetVerticesDirty(); }
        }

        public float embossAngle
        {
            get => _embossAngle;
            set { if (Mathf.Approximately(_embossAngle, value)) return; _embossAngle = value; SetVerticesDirty(); }
        }

        public float embossStrength
        {
            get => _embossStrength;
            set { if (Mathf.Approximately(_embossStrength, value)) return; _embossStrength = value; SetVerticesDirty(); }
        }

        public Color embossHighlightColor
        {
            get => _embossHighlightColor;
            set { if (_embossHighlightColor == value) return; _embossHighlightColor = value; SetVerticesDirty(); }
        }

        public Color embossShadowColor
        {
            get => _embossShadowColor;
            set { if (_embossShadowColor == value) return; _embossShadowColor = value; SetVerticesDirty(); }
        }

        float FrameOutwardExtension
        {
            get
            {
                if (!_noFill) return 0f;
                float fw = Mathf.Round(_frameWidth);
                if (_framePlacement == FramePlacement.Center) return fw * 0.5f;
                if (_framePlacement == FramePlacement.Outside) return fw;
                return 0f;
            }
        }

        float extraMargin => _outlineSize + FrameOutwardExtension + _shadowSize + _shadowBlur + ExtendedPadding + 1f;

        // ---------- Opciones extendidas: material propio solo para los gráficos que las usan ----------
        public const string ExtendedKeyword = "HT_EXTENDED";

        static readonly int OutlineColorBId = Shader.PropertyToID("_OutlineColorB");
        static readonly int OutlineGradientId = Shader.PropertyToID("_OutlineGradient");
        static readonly int InnerShadowColorId = Shader.PropertyToID("_InnerShadowColor");
        static readonly int InnerShadowId = Shader.PropertyToID("_InnerShadow");
        static readonly int ShadowOffsetId = Shader.PropertyToID("_ShadowOffset");

        Material _extendedMaterial;

        /// <summary> Usa alguna opción que no cabe en los datos de vértice (degradado del contorno, sombra interior o desplazamiento de sombra) </summary>
        public bool usesExtendedFeatures =>
            (_outlineGradientType != OutlineGradientType.None && _outlineSize > 0f)
            || _innerShadowSize > 0f || _innerShadowBlur > 0f
            || (_shadowOffset != Vector2.zero && _shadowSize + _shadowBlur > 0f);

        /// <summary> Margen que necesita el desplazamiento de la sombra (el shader lo recibe en _ShadowOffset.z) </summary>
        float ExtendedPadding => usesExtendedFeatures ? Mathf.Max(Mathf.Abs(_shadowOffset.x), Mathf.Abs(_shadowOffset.y)) : 0f;

        public override Material GetModifiedMaterial(Material baseMaterial)
        {
            var modified = base.GetModifiedMaterial(baseMaterial);
            if (!usesExtendedFeatures || !modified)
                return modified;

            if (!_extendedMaterial)
                _extendedMaterial = new Material(modified) { hideFlags = HideFlags.HideAndDontSave, name = "RectangleRenderer (Extended)" };
            else if (_extendedMaterial.shader != modified.shader)
                _extendedMaterial.shader = modified.shader;

            _extendedMaterial.CopyPropertiesFromMaterial(modified);
            _extendedMaterial.EnableKeyword(ExtendedKeyword);

            float angle = _outlineGradientAngle * Mathf.Deg2Rad;
            _extendedMaterial.SetColor(OutlineColorBId, _outlineGradientColor);
            _extendedMaterial.SetVector(OutlineGradientId, new Vector4((int)_outlineGradientType, Mathf.Cos(angle), Mathf.Sin(angle), 0f));
            _extendedMaterial.SetColor(InnerShadowColorId, _innerShadowColor);
            _extendedMaterial.SetVector(InnerShadowId, new Vector4(_innerShadowSize, _innerShadowBlur, _innerShadowOffset.x, _innerShadowOffset.y));
            _extendedMaterial.SetVector(ShadowOffsetId, new Vector4(_shadowOffset.x, _shadowOffset.y, ExtendedPadding, 0f));
            return _extendedMaterial;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (!_extendedMaterial) return;
            if (Application.isPlaying) Destroy(_extendedMaterial);
            else DestroyImmediate(_extendedMaterial);
        }

        public override Texture mainTexture => _texture ? _texture : s_WhiteTexture;

        public override Material defaultMaterial
        {
            get
            {
                if (_sharedMaterial == null && _shader != null)
                    _sharedMaterial = new Material(_shader) { hideFlags = HideFlags.HideAndDontSave };
                return _sharedMaterial;
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Refresh();
        }

        protected override void OnCanvasHierarchyChanged()
        {
            base.OnCanvasHierarchyChanged();
            Refresh();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            Refresh();
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            Refresh();
        }

        protected override void OnDidApplyAnimationProperties()
        {
            base.OnDidApplyAnimationProperties();
            Refresh();
        }

#if UNITY_EDITOR
        protected override void Reset()
        {
            base.Reset();
            _shader = Shader.Find(SHADER_NAME);
            EnsureAdditionalCanvasChannels();
        }

        protected override void OnValidate()
        {
            if (!_shader)
                _shader = Shader.Find(SHADER_NAME);
            base.OnValidate();
            Refresh();
        }
#endif

        public void Refresh()
        {
            EnsureAdditionalCanvasChannels();
            SetAllDirty();
#if UNITY_EDITOR
            QueueEditorRebuild();
#endif
        }

#if UNITY_EDITOR
        void QueueEditorRebuild()
        {
            if (Application.isPlaying)
                return;

            s_PendingEditorRebuilds.Add(this);
            if (s_EditorFlushQueued)
                return;

            s_EditorFlushQueued = true;
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            UnityEditor.EditorApplication.delayCall += FlushEditorRebuilds;
        }

        // Canvas.ForceUpdateCanvases() rebuilds every canvas in the editor, so calling it once per
        // queued graphic made opening a prefab cost (graphic count x whole-canvas rebuild). With a few
        // dozen graphics on a heavy canvas that stalled the editor for minutes. One flush per tick keeps
        // the immediate refresh while making the cost independent of how many graphics asked for it.
        static void FlushEditorRebuilds()
        {
            s_EditorFlushQueued = false;
            if (s_PendingEditorRebuilds.Count == 0)
                return;

            // Copy first: refreshing can queue graphics again, and those wait for the next flush.
            var pending = new System.Collections.Generic.List<SignedDistanceFieldGraphic>(s_PendingEditorRebuilds);
            s_PendingEditorRebuilds.Clear();

            bool anyRefreshed = false;
            foreach (var graphic in pending)
            {
                if (!graphic || !graphic.isActiveAndEnabled)
                    continue;

                graphic.EnsureAdditionalCanvasChannels();
                graphic.SetAllDirty();
                anyRefreshed = true;
            }

            if (!anyRefreshed)
                return;

            Canvas.ForceUpdateCanvases();
            UnityEditor.SceneView.RepaintAll();
        }
#endif

        void EnsureAdditionalCanvasChannels()
        {
            if (!canvas) return;
            var rootCanvas = canvas.rootCanvas;
            rootCanvas.additionalShaderChannels |= RequiredCanvasChannels;
        }

        protected virtual Vector4 GetRoundness()
        {
            return Vector4.zero;
        }

        public static Vector2 PackColor(Color color)
        {
            Color32 c = color;
            return new Vector2(c.r + c.g * 256f, c.b + c.a * 256f);
        }

        UIVertex BuildBaseVertex()
        {
            var packedOutline = PackColor(_outlineColor);
            var packedShadow = PackColor(_shadowColor);

            var roundness = GetRoundness();

            // Encode frame info into existing channels:
            //   uv2.x: negative signals frame mode. abs = 1 + placement * 4096 + outlineSize
            //   uv2.w: shadowPower + round(frameWidth) * 256
            float frameWidthRounded = _noFill ? Mathf.Round(_frameWidth) : 0f;

            float encodedOutline = _noFill
                ? -(1f + (int)_framePlacement * 4096f + _outlineSize)
                : _outlineSize;
            float encodedShadowPow = _shadowPower + frameWidthRounded * 256f;

            var effects = new Vector4(encodedOutline, _shadowSize, _shadowBlur, encodedShadowPow);
            var colors = new Vector4(
                packedOutline.x, packedOutline.y,
                packedShadow.x, packedShadow.y);

            var vertex = UIVertex.simpleVert;
            vertex.normal = new Vector3(_embossSize, _embossAngle * Mathf.Deg2Rad, _embossStrength);
            var packedHighlight = PackColor(_embossHighlightColor);
            var packedShadowEmb = PackColor(_embossShadowColor);
            vertex.tangent = new Vector4(packedHighlight.x, packedHighlight.y, packedShadowEmb.x, packedShadowEmb.y);
            vertex.uv1 = roundness;
            vertex.uv2 = effects;
            vertex.uv3 = colors;

            return vertex;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            float width = rectTransform.rect.width;
            float height = rectTransform.rect.height;

            if (width <= 0f || height <= 0f)
                return;

            if (_gradientType == GradientType.Radial ||
                _gradientType == GradientType.Gradient)
            {
                PopulateSubdividedMesh(vh, width, height);
                return;
            }

            float margin = extraMargin;

            var pivot = new Vector3(
                rectTransform.pivot.x * width,
                rectTransform.pivot.y * height, 0);

            Color colorA = _graphicColor * color;
            Color colorB = _gradientType != GradientType.None ? _gradientColor * color : colorA;

            Color c0, c1, c2, c3;
            switch (_gradientType)
            {
                case GradientType.Vertical:
                    c0 = colorB; c1 = colorA; c2 = colorA; c3 = colorB;
                    break;
                case GradientType.Horizontal:
                    c0 = colorA; c1 = colorA; c2 = colorB; c3 = colorB;
                    break;
                default:
                    c0 = c1 = c2 = c3 = colorA;
                    break;
            }

            var vertex = BuildBaseVertex();

            vertex.color = c0;
            vertex.position = new Vector3(-margin, -margin) - pivot;
            vertex.uv0 = new Vector4(0, 0, width, height);
            vh.AddVert(vertex);

            vertex.color = c1;
            vertex.position = new Vector3(-margin, height + margin) - pivot;
            vertex.uv0 = new Vector4(0, 1, width, height);
            vh.AddVert(vertex);

            vertex.color = c2;
            vertex.position = new Vector3(width + margin, height + margin) - pivot;
            vertex.uv0 = new Vector4(1, 1, width, height);
            vh.AddVert(vertex);

            vertex.color = c3;
            vertex.position = new Vector3(width + margin, -margin) - pivot;
            vertex.uv0 = new Vector4(1, 0, width, height);
            vh.AddVert(vertex);

            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }

        void PopulateSubdividedMesh(VertexHelper vh, float width, float height)
        {
            int n = Mathf.Clamp(_gradientQuality, 2, 20);
            float margin = extraMargin;

            var pivot = new Vector3(
                rectTransform.pivot.x * width,
                rectTransform.pivot.y * height, 0);

            Color masterColor = color;
            Color colorA = _graphicColor * masterColor;
            Color colorB = _gradientColor * masterColor;

            float angleRad = _gradientAngle * Mathf.Deg2Rad;
            var gradDir = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));

            var vertex = BuildBaseVertex();

            // Hoist invariants out of the loop
            float posMinX = -margin;
            float posRangeX = width + margin * 2f;
            float posMinY = -margin;
            float posRangeY = height + margin * 2f;

            // Build (n+1) x (n+1) vertex grid
            int cols = n + 1;
            for (int y = 0; y <= n; y++)
            {
                float fy = (float)y / n;
                float posY = posMinY + posRangeY * fy - pivot.y;

                for (int x = 0; x <= n; x++)
                {
                    float fx = (float)x / n;

                    vertex.position = new Vector3(posMinX + posRangeX * fx - pivot.x, posY, 0);

                    vertex.uv0 = new Vector4(fx, fy, width, height);

                    // Compute vertex color based on gradient type
                    switch (_gradientType)
                    {
                        case GradientType.Radial:
                        {
                            float t;
                            if (_radialMode == RadialMode.Ellipse)
                            {
                                float dx = fx - 0.5f;
                                float dy = fy - 0.5f;
                                t = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                            }
                            else
                            {
                                float refDim = _radialMode == RadialMode.CircleCover
                                    ? Mathf.Max(width, height)
                                    : Mathf.Min(width, height);
                                float dx = (fx - 0.5f) * width;
                                float dy = (fy - 0.5f) * height;
                                t = Mathf.Sqrt(dx * dx + dy * dy) / (refDim * 0.5f);
                            }
                            vertex.color = Color.Lerp(colorA, colorB, Mathf.Clamp01(t / _gradientSize));
                            break;
                        }
                        case GradientType.Gradient:
                        {
                            float t = Mathf.Clamp01((fx - 0.5f) * gradDir.x + (fy - 0.5f) * gradDir.y + 0.5f);
                            vertex.color = (_gradient != null ? _gradient.Evaluate(t) : Color.white) * masterColor;
                            break;
                        }
                    }

                    vh.AddVert(vertex);
                }
            }

            // Triangles: standard grid
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    int i = y * cols + x;
                    vh.AddTriangle(i, i + cols, i + cols + 1);
                    vh.AddTriangle(i + cols + 1, i + 1, i);
                }
            }
        }

        private static readonly string[] _keys = {
            "Graphic",
            "Gradient",
            "Outline",
            "Shadow",
            "Emboss Highlight",
            "Emboss Shadow",
            "Outline Gradient",
            "Inner Shadow"
        };

        public string[] keys => _keys;

        public Color GetColor(int keyIndex)
        {
            return keyIndex switch
            {
                0 => graphicColor,
                1 => gradientColor,
                2 => outlineColor,
                3 => shadowColor,
                4 => embossHighlightColor,
                5 => embossShadowColor,
                6 => outlineGradientColor,
                7 => innerShadowColor,
                _ => throw new ArgumentOutOfRangeException(nameof(keyIndex), keyIndex, $"Key index must be between 0 and {_keys.Length - 1}.")
            };
        }

        public void SetColor(int keyIndex, Color color)
        {
            switch (keyIndex)
            {
                case 0: graphicColor = color; break;
                case 1: gradientColor = color; break;
                case 2: outlineColor = color; break;
                case 3: shadowColor = color; break;
                case 4: embossHighlightColor = color; break;
                case 5: embossShadowColor = color; break;
                case 6: outlineGradientColor = color; break;
                case 7: innerShadowColor = color; break;
            }
        }
    }
}
