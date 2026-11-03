using System.Linq;
using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.ProBuilder;
using UnityEditor.ProBuilder;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace HarryTools.ProBuilderExtensions.HotspotUV.Editor {
    [ProBuilderMenuAction]
    public class HotspotAction : MenuAction {
        private const string TargetPref = "HotspotUV.TargetGUID";
        private const string AllowRotationPref = "HotspotUV.AllowRotation";
        private const string PreserveAspectPref = "HotspotUV.PreserveAspect";
        private const string RandomTolerancePref = "HotspotUV.RandomTolerance";
        private const string ScaleTolerancePref = "HotspotUV.ScaleTolerance";
        private const string AllowMirrorPref = "HotspotUV.AllowMirror";

        public HotspotTexture target;

        public override ToolbarGroup group => ToolbarGroup.Geometry;

        // Custom actions have no generated "Tools/ProBuilder/..." menu item, so the Scene view context menu
        // must invoke the action directly instead of looking up a menu path.
        protected override bool hasFileMenuEntry => false;

        [MenuItem("Tools/HarryTools/ProBuilder Extensions/Hotspot Selection")]
        private static void HotspotSelectionMenu() {
            var result = new HotspotAction().RunAction();
            if (result.status != ActionResult.Status.Success)
                Debug.LogWarning($"Hotspot UV: {result.notification}");
        }

        [MenuItem("Tools/HarryTools/ProBuilder Extensions/Hotspot Selection", true)]
        private static bool HotspotSelectionMenuValidate() => MeshSelection.selectedFaceCount > 0;
        public override string iconPath => string.Empty;

        public override Texture2D icon => null;

        public override TooltipContent tooltip => new("Hotspot Selection", "Hotspot UV the current selection");
        public override bool enabled => base.enabled;
        public override SelectMode validSelectModes => SelectMode.Face | SelectMode.TextureFace;

        protected override MenuActionState optionsMenuState {
            get { return MenuActionState.VisibleAndEnabled; }
        }

        private static bool AllowRotation {
            get => EditorPrefs.GetBool(AllowRotationPref, true);
            set => EditorPrefs.SetBool(AllowRotationPref, value);
        }

        private static bool PreserveAspect {
            get => EditorPrefs.GetBool(PreserveAspectPref, false);
            set => EditorPrefs.SetBool(PreserveAspectPref, value);
        }

        private static float RandomTolerance {
            get => EditorPrefs.GetFloat(RandomTolerancePref, 0.15f);
            set => EditorPrefs.SetFloat(RandomTolerancePref, value);
        }

        private static float ScaleTolerance {
            get => EditorPrefs.GetFloat(ScaleTolerancePref, 0.25f);
            set => EditorPrefs.SetFloat(ScaleTolerancePref, value);
        }

        private static bool AllowMirror {
            get => EditorPrefs.GetBool(AllowMirrorPref, true);
            set => EditorPrefs.SetBool(AllowMirrorPref, value);
        }

        protected override void OnSettingsEnable() {
            CheckForSettings();
        }

        // ProBuilder 6 shows action settings in a Scene view overlay built from this, not from OnSettingsGUI.
        public override VisualElement CreateSettingsContent() {
            CheckForSettings();
            return new IMGUIContainer(OnSettingsGUI);
        }

        public void CheckForSettings() {
            if (target != null) return;
            var path = AssetDatabase.GUIDToAssetPath(EditorPrefs.GetString(TargetPref, string.Empty));
            if (!string.IsNullOrEmpty(path))
                target = AssetDatabase.LoadAssetAtPath<HotspotTexture>(path);
        }

        protected override void OnSettingsGUI() {
            GUILayout.Label("Hotspot Settings", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            target = (HotspotTexture)EditorGUILayout.ObjectField(target, typeof(HotspotTexture), false);
            if (EditorGUI.EndChangeCheck()) {
                var guid = target != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(target)) : string.Empty;
                EditorPrefs.SetString(TargetPref, guid);
            }

            if (target != null && !target.IsValid)
                EditorGUILayout.HelpBox("The Hotspot Texture needs a texture and at least one rect.", MessageType.Warning);

            AllowRotation = EditorGUILayout.Toggle(new GUIContent("Allow Rotation", "Allow rotating the face 90° to fit a rect better. Patches can also forbid it."), AllowRotation);
            AllowMirror = EditorGUILayout.Toggle(new GUIContent("Allow Mirror", "Allow randomly mirroring patches that permit it."), AllowMirror);
            ScaleTolerance = EditorGUILayout.Slider(new GUIContent("Scale Tolerance", "How different in world size a patch can be from the face (0.25 = ±25%). Only applies to Hotspot Textures that use a layout."), ScaleTolerance, 0f, 1f);
            PreserveAspect = EditorGUILayout.Toggle(new GUIContent("Preserve Aspect", "Fit inside the rect without stretching instead of filling it."), PreserveAspect);
            RandomTolerance = EditorGUILayout.Slider(new GUIContent("Random Tolerance", "How much worse than the best match a rect can be and still be randomly chosen. 0 = always the best match."), RandomTolerance, 0f, 1f);

            EditorGUILayout.Space();

            GUILayout.Label("Rotate", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("-45"))
                    RotateUV(-45);
                if (GUILayout.Button("+45"))
                    RotateUV(45);
            }
            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("-90"))
                    RotateUV(-90);
                if (GUILayout.Button("+90"))
                    RotateUV(90);
            }

            GUILayout.Label("Flip", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("X"))
                    FlipUV(Vector2.right);
                if (GUILayout.Button("Y"))
                    FlipUV(Vector2.up);
            }

            EditorGUILayout.Space();

            if (GUILayout.Button(new GUIContent("Hotspot selection", "Apply again (picks a new random rect among the best matches).")))
                PerformAction();
        }

        public void RotateUV(float rotation) {
            ModifySelectedFaces("Rotate UV", (mesh, face, uvs) => {
                var distinctIndexes = face.distinctIndexes;
                var center = FaceUVCenter(distinctIndexes, uvs);
                var rot = Quaternion.Euler(0, 0, -rotation);
                foreach (var index in distinctIndexes)
                    uvs[index] = (Vector2)(rot * ((Vector2)uvs[index] - center)) + center;
                return true;
            });
        }

        public void FlipUV(Vector2 direction) {
            ModifySelectedFaces("Flip UV", (mesh, face, uvs) => {
                var distinctIndexes = face.distinctIndexes;
                var center = FaceUVCenter(distinctIndexes, uvs);
                foreach (var index in distinctIndexes)
                    uvs[index] = UnityEngine.ProBuilder.Math.ReflectPoint(uvs[index], center, center + direction);
                return true;
            });
        }

        private static Vector2 FaceUVCenter(IList<int> indexes, List<Vector4> uvs) {
            var center = Vector2.zero;
            foreach (var index in indexes)
                center += (Vector2)uvs[index];
            return center / indexes.Count;
        }

        /// <summary>
        /// Runs <paramref name="modify"/> on every selected face with undo support and rebuilds the meshes.
        /// </summary>
        /// <returns>The number of faces modified.</returns>
        private static int ModifySelectedFaces(string undoName, Func<ProBuilderMesh, Face, List<Vector4>, bool> modify) {
            var meshes = MeshSelection.top.ToArray();
            if (meshes.Length == 0) return 0;

            Undo.RecordObjects(meshes, undoName);

            var modified = 0;
            var uvs = new List<Vector4>();
            foreach (var mesh in meshes) {
                var faces = mesh.GetSelectedFaces();
                if (faces == null || faces.Length == 0) continue;

                mesh.GetUVs(0, uvs);
                foreach (var face in faces) {
                    if (!modify(mesh, face, uvs)) continue;
                    face.manualUV = true;
                    modified++;
                }
                mesh.SetUVs(0, uvs);
                mesh.ToMesh();
                mesh.Refresh();
                mesh.Optimize();
            }

            ProBuilderEditor.Refresh();
            return modified;
        }

        protected override ActionResult PerformActionImplementation() => RunAction();

        public ActionResult RunAction() {
            CheckForSettings();

            if (MeshSelection.selectedFaceCount < 1)
                return ActionResult.NoSelection;

            if (target == null || !target.IsValid) {
                Debug.LogWarning("Hotspot UV: select a Hotspot Texture with a texture and at least one rect.");
                return new ActionResult(ActionResult.Status.Canceled, "No valid Hotspot Texture");
            }

            var hotspot = target;
            var preserveAspect = PreserveAspect;
            var settings = new HotspotMatchSettings {
                allowRotation = AllowRotation,
                allowMirror = AllowMirror,
                randomTolerance = RandomTolerance,
                scaleTolerance = ScaleTolerance,
            };

            var outOfTolerance = 0;
            var count = ModifySelectedFaces("Hotspot UV", (mesh, face, uvs) => {
                if (!HotspotFace(mesh, face, uvs, hotspot, settings, preserveAspect, out var match)) return false;
                if (match.outOfTolerance) outOfTolerance++;
                return true;
            });

            if (outOfTolerance > 0)
                Debug.LogWarning($"Hotspot UV: {outOfTolerance} face(s) had no patch within the scale tolerance and used the closest one.");

            return count > 0
                ? new ActionResult(ActionResult.Status.Success, "Hotspot UV")
                : new ActionResult(ActionResult.Status.NoChange, "No faces could be hotspotted");
        }

        /// <summary>
        /// Projects the face onto its plane (in world space so object scale is respected), aligns it to its
        /// tightest bounding rectangle, picks the best matching hotspot and fits the face into it.
        /// </summary>
        public static bool HotspotFace(ProBuilderMesh mesh, Face face, List<Vector4> uvs, HotspotTexture hotspot,
            HotspotMatchSettings settings, bool preserveAspect, out HotspotMatch match) {
            match = HotspotMatch.Invalid;
            var indexes = face.distinctIndexes;
            if (indexes.Count < 3) return false;

            var positions = mesh.positions;
            var trs = mesh.transform;

            var world = new Vector3[indexes.Count];
            var localIndex = new Dictionary<int, int>();
            for (int i = 0; i < indexes.Count; i++) {
                world[i] = trs.TransformPoint(positions[indexes[i]]);
                localIndex[indexes[i]] = i;
            }

            // Normal from the triangles' winding, so the projection is never mirrored.
            var normal = Vector3.zero;
            var tris = face.indexes;
            for (int t = 0; t + 2 < tris.Count; t += 3) {
                var a = world[localIndex[tris[t]]];
                var b = world[localIndex[tris[t + 1]]];
                var c = world[localIndex[tris[t + 2]]];
                normal += Vector3.Cross(b - a, c - a);
            }
            if (normal.sqrMagnitude < 1e-12f) return false;

            var projected = Projection.PlanarProject(world, null, normal);

            // Align the face to the edge that gives the smallest bounding box, keeping the result
            // as close as possible to the original orientation (angle within ±45°).
            var bestAngle = 0f;
            var bestArea = BoundsArea(projected, 0f);
            foreach (var edge in face.edges) {
                if (!localIndex.TryGetValue(edge.a, out var ia) || !localIndex.TryGetValue(edge.b, out var ib)) continue;
                var dir = projected[ib] - projected[ia];
                if (dir.sqrMagnitude < 1e-12f) continue;

                var angle = Mathf.Repeat(Mathf.Atan2(dir.y, dir.x) + Mathf.PI / 4, Mathf.PI / 2) - Mathf.PI / 4;
                var area = BoundsArea(projected, angle);
                if (area < bestArea * 0.9999f) {
                    bestArea = area;
                    bestAngle = angle;
                }
            }
            Rotate(projected, bestAngle);

            GetBounds(projected, out _, out var size);
            match = hotspot.FindBestPatch(size, settings);
            if (!match.IsValid) return false;

            var mapped = new Vector2[projected.Length];
            if (!HotspotMapper.Map(projected, hotspot, match, preserveAspect, mapped)) return false;

            for (int i = 0; i < indexes.Count; i++)
                uvs[indexes[i]] = mapped[i];

            return true;
        }

        private static void Rotate(Vector2[] points, float angle) {
            float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
            for (int i = 0; i < points.Length; i++) {
                var p = points[i];
                points[i] = new Vector2(p.x * cos + p.y * sin, -p.x * sin + p.y * cos);
            }
        }

        private static float BoundsArea(Vector2[] points, float angle) {
            float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var p in points) {
                var x = p.x * cos + p.y * sin;
                var y = -p.x * sin + p.y * cos;
                minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
            }
            return (maxX - minX) * (maxY - minY);
        }

        private static void GetBounds(Vector2[] points, out Vector2 min, out Vector2 size) {
            min = points[0];
            var max = points[0];
            foreach (var p in points) {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            size = max - min;
        }
    }
}
