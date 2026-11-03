using HarryTools.UI;
using UnityEditor;
using UnityEngine;

namespace HarryTools.UI.Editor
{
    [CustomEditor(typeof(GlowGraphic))]
    public class GlowGraphicEditor : UnityEditor.Editor
    {
        // Source
        SerializedProperty _source;
        SerializedProperty _extraSize;

        // Shape
        SerializedProperty _noFill;
        SerializedProperty _frameWidth;
        SerializedProperty _framePlacement;
        SerializedProperty _useMaxRoundness;
        SerializedProperty _uniformRoundness;
        SerializedProperty _roundnessInPixels;

        // Color
        SerializedProperty _glowColor;
        SerializedProperty _glowGradientType;
        SerializedProperty _glowGradientColor;
        SerializedProperty _glowGradientQuality;
        SerializedProperty _glowRadialMode;
        SerializedProperty _glowGradientSize;
        SerializedProperty _glowGradient;
        SerializedProperty _glowGradientAngle;

        // Glow
        SerializedProperty _spread;
        SerializedProperty _blur;
        SerializedProperty _power;

        // Built-in Graphic
        SerializedProperty _raycastTarget;
        SerializedProperty _raycastPadding;
        SerializedProperty _maskable;

        void OnEnable()
        {
            _source = serializedObject.FindProperty("_source");
            _extraSize = serializedObject.FindProperty("_extraSize");

            _noFill = serializedObject.FindProperty("_noFill");
            _frameWidth = serializedObject.FindProperty("_frameWidth");
            _framePlacement = serializedObject.FindProperty("_framePlacement");
            _useMaxRoundness = serializedObject.FindProperty("_useMaxRoundness");
            _uniformRoundness = serializedObject.FindProperty("_uniformRoundness");
            _roundnessInPixels = serializedObject.FindProperty("_roundnessInPixels");

            _glowColor = serializedObject.FindProperty("_glowColor");
            _glowGradientType = serializedObject.FindProperty("_glowGradientType");
            _glowGradientColor = serializedObject.FindProperty("_glowGradientColor");
            _glowGradientQuality = serializedObject.FindProperty("_glowGradientQuality");
            _glowRadialMode = serializedObject.FindProperty("_glowRadialMode");
            _glowGradientSize = serializedObject.FindProperty("_glowGradientSize");
            _glowGradient = serializedObject.FindProperty("_glowGradient");
            _glowGradientAngle = serializedObject.FindProperty("_glowGradientAngle");

            _spread = serializedObject.FindProperty("_spread");
            _blur = serializedObject.FindProperty("_blur");
            _power = serializedObject.FindProperty("_power");

            _raycastTarget = serializedObject.FindProperty("m_RaycastTarget");
            _raycastPadding = serializedObject.FindProperty("m_RaycastPadding");
            _maskable = serializedObject.FindProperty("m_Maskable");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Source
            BeginSection("Source");
            EditorGUILayout.PropertyField(_source, new GUIContent("Rectangle"));
            EditorGUILayout.PropertyField(_extraSize, new GUIContent("Extra Size"));
            EndSection();

            bool hasSource = _source.objectReferenceValue != null;

            // Color
            BeginSection("Color");
            EditorGUILayout.PropertyField(_glowColor, new GUIContent("Color"));
            EditorGUILayout.PropertyField(_glowGradientType, new GUIContent("Gradient"));

            var gt = (GradientType)_glowGradientType.enumValueIndex;

            if (gt == GradientType.Vertical || gt == GradientType.Horizontal ||
                gt == GradientType.Radial)
                EditorGUILayout.PropertyField(_glowGradientColor, new GUIContent("Gradient Color"));

            if (gt == GradientType.Radial || gt == GradientType.Gradient)
                EditorGUILayout.PropertyField(_glowGradientQuality, new GUIContent("Quality"));

            if (gt == GradientType.Radial)
            {
                EditorGUILayout.PropertyField(_glowRadialMode, new GUIContent("Mode"));
                EditorGUILayout.PropertyField(_glowGradientSize, new GUIContent("Size"));
            }

            if (gt == GradientType.Gradient)
            {
                EditorGUILayout.PropertyField(_glowGradient, new GUIContent("Gradient"));
                EditorGUILayout.PropertyField(_glowGradientAngle, new GUIContent("Angle"));
            }

            EndSection();

            // Shape (only when no source linked)
            if (!hasSource)
            {
                BeginSection("Shape");
                EditorGUILayout.PropertyField(_useMaxRoundness);

                if (!_useMaxRoundness.boolValue)
                {
                    EditorGUILayout.PropertyField(_uniformRoundness);

                    if (_uniformRoundness.boolValue)
                    {
                        var v = _roundnessInPixels.vector4Value;
                        float val = EditorGUILayout.FloatField("Roundness", v.x);
                        if (!Mathf.Approximately(val, v.x))
                            _roundnessInPixels.vector4Value = new Vector4(val, val, val, val);
                    }
                    else
                    {
                        var v = _roundnessInPixels.vector4Value;
                        v.z = EditorGUILayout.FloatField("Top Left", v.z);
                        v.x = EditorGUILayout.FloatField("Top Right", v.x);
                        v.y = EditorGUILayout.FloatField("Bottom Right", v.y);
                        v.w = EditorGUILayout.FloatField("Bottom Left", v.w);
                        _roundnessInPixels.vector4Value = v;
                    }
                }

                EditorGUILayout.PropertyField(_noFill, new GUIContent("No Fill"));

                if (_noFill.boolValue)
                {
                    EditorGUILayout.PropertyField(_frameWidth, new GUIContent("Frame Width"));
                    EditorGUILayout.PropertyField(_framePlacement, new GUIContent("Placement"));
                }

                EndSection();
            }

            // Glow
            BeginSection("Glow");
            EditorGUILayout.PropertyField(_spread, new GUIContent("Spread"));
            EditorGUILayout.PropertyField(_blur, new GUIContent("Blur"));
            EditorGUILayout.PropertyField(_power, new GUIContent("Power"));
            EndSection();

            // Raycast
            BeginSection("Raycast");
            EditorGUILayout.PropertyField(_raycastTarget);
            EditorGUILayout.PropertyField(_raycastPadding);
            EditorGUILayout.PropertyField(_maskable);
            EndSection();

            if (serializedObject.ApplyModifiedProperties())
            {
                foreach (var obj in targets)
                {
                    if (obj is not GlowGraphic graphic) continue;
                    graphic.Refresh();
                    EditorUtility.SetDirty(graphic);
                }
            }
        }

        static GUIStyle _sectionStyle;

        static GUIStyle sectionStyle
        {
            get
            {
                if (_sectionStyle == null)
                {
                    _sectionStyle = new GUIStyle(EditorStyles.helpBox)
                    {
                        padding = new RectOffset(14, 0, 0, 0),
                        margin = new RectOffset(0, 0, 2, 2)
                    };
                }
                return _sectionStyle;
            }
        }

        static void BeginSection(string title)
        {
            EditorGUILayout.BeginVertical(sectionStyle);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        static void EndSection()
        {
            EditorGUILayout.EndVertical();
        }
    }
}
