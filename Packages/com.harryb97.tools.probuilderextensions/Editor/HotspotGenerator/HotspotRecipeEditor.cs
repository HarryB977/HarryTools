using UnityEditor;
using UnityEngine;

namespace HarryTools.ProBuilderExtensions.Generator {
    /// <summary>Recipe inspector grouped by what each setting changes, with small lengths shown in centimetres.</summary>
    [CustomEditor(typeof(HotspotRecipe))]
    public class HotspotRecipeEditor : UnityEditor.Editor {
        private static bool showOutputs;

        public override void OnInspectorGUI() {
            serializedObject.Update();
            var wear = serializedObject.FindProperty(nameof(HotspotRecipe.wear));
            SerializedProperty W(string name) => wear.FindPropertyRelative(name);

            Section("Layout");
            Field(nameof(HotspotRecipe.layout));
            Field(nameof(HotspotRecipe.resolution));
            Field(nameof(HotspotRecipe.worldWidthOverride));

            Section("Layers");
            Field(nameof(HotspotRecipe.top), "Top Layer");
            Field(nameof(HotspotRecipe.bottom), "Bottom Layer");

            Section("Bevel and Rim");
            Centimetres(W(nameof(HotspotWear.bevelWidth)), "Bevel Width (cm)", 0.1f, 10f);
            Centimetres(W(nameof(HotspotWear.edgeWidth)), "Edge Width (cm)", 0f, 5f);
            EditorGUILayout.PropertyField(W(nameof(HotspotWear.edgeTone)));

            Section("Wear");
            var style = W(nameof(HotspotWear.style));
            EditorGUILayout.PropertyField(style);
            EditorGUILayout.PropertyField(W(nameof(HotspotWear.intensity)));
            switch ((HotspotWearStyle)style.enumValueIndex) {
                case HotspotWearStyle.Cracks:
                    Centimetres(W(nameof(HotspotWear.wearSize)), "Crack Spacing (cm)", 0.5f, 50f);
                    break;
                case HotspotWearStyle.Bites:
                    Centimetres(W(nameof(HotspotWear.wearSize)), "Bite Length (cm)", 0.5f, 50f);
                    break;
            }

            Section("Wear Texture");
            var texture = W(nameof(HotspotWear.wearTexture));
            EditorGUILayout.PropertyField(texture);
            using (new EditorGUI.DisabledScope(texture.objectReferenceValue == null && !texture.hasMultipleDifferentValues)) {
                EditorGUILayout.PropertyField(W(nameof(HotspotWear.textureBorder)));
                Centimetres(W(nameof(HotspotWear.textureDepth)), "Texture Depth (cm)", 0.1f, 30f);
            }

            Section("Shading");
            EditorGUILayout.PropertyField(W(nameof(HotspotWear.cavity)));
            EditorGUILayout.PropertyField(W(nameof(HotspotWear.gapDepth)));

            Section("Variation");
            Field(nameof(HotspotRecipe.variation));
            Field(nameof(HotspotRecipe.seed));

            EditorGUILayout.Space();
            showOutputs = EditorGUILayout.Foldout(showOutputs, "Outputs (written by Generate)", true);
            if (showOutputs) {
                using (new EditorGUI.IndentLevelScope())
                using (new EditorGUI.DisabledScope(true)) {
                    Field(nameof(HotspotRecipe.baseColorMap));
                    Field(nameof(HotspotRecipe.normalMap));
                    Field(nameof(HotspotRecipe.maskMap));
                    Field(nameof(HotspotRecipe.occlusionMap));
                    Field(nameof(HotspotRecipe.heightMap));
                    Field(nameof(HotspotRecipe.material));
                    Field(nameof(HotspotRecipe.hotspotTexture));
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void Section(string title) {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        private void Field(string name, string label = null) {
            var property = serializedObject.FindProperty(name);
            if (label == null) EditorGUILayout.PropertyField(property, true);
            else EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip), true);
        }

        /// <summary>A length stored in metres, edited as a centimetre slider.</summary>
        private static void Centimetres(SerializedProperty metres, string label, float min, float max) {
            EditorGUI.showMixedValue = metres.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            var value = EditorGUILayout.Slider(new GUIContent(label, metres.tooltip), metres.floatValue * 100f, min, max);
            if (EditorGUI.EndChangeCheck()) metres.floatValue = value / 100f;
            EditorGUI.showMixedValue = false;
        }
    }
}
