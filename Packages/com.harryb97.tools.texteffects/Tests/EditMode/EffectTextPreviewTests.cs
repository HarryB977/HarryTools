using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using HarryTools.Actions;
using HarryTools.Conditions;
using HarryTools.TextEffects.Editor;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HarryTools.TextEffects.Tests {
    public class EffectTextPreviewTests {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string TempFolder = "Assets/__TextEffectsTests__";
        private const string ScenePath = TempFolder + "/PreviewTestScene.unity";
        private const string OriginalText = "Original";

        private const string SceneYaml =
            "%YAML 1.1\n" +
            "%TAG !u! tag:unity3d.com,2011:\n" +
            "--- !u!1 &100\n" +
            "GameObject:\n" +
            "  m_ObjectHideFlags: 0\n" +
            "  serializedVersion: 6\n" +
            "  m_Component:\n" +
            "  - component: {fileID: 200}\n" +
            "  m_Layer: 0\n" +
            "  m_Name: PreviewText\n" +
            "  m_IsActive: 1\n" +
            "--- !u!4 &200\n" +
            "Transform:\n" +
            "  m_GameObject: {fileID: 100}\n" +
            "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n" +
            "  m_LocalPosition: {x: 0, y: 0, z: 0}\n" +
            "  m_LocalScale: {x: 1, y: 1, z: 1}\n" +
            "  m_Children: []\n" +
            "  m_Father: {fileID: 0}\n";

        private sealed class FlagAction : IAction {
            public static bool Ran;

            public UniTask Run(LogicContext _context, CancellationToken _token) {
                Ran = true;
                return UniTask.CompletedTask;
            }
        }

        private TextEffectsSettings settings;
        private Scene scene;
        private EffectText effect;
        private TMP_Text tmp;
        private EffectTextPreview preview;
        #endregion

        // ==================== SETUP ===================
        #region Setup
        [SetUp]
        public void SetUp() {
            FlagAction.Ran = false;
            settings = ScriptableObject.CreateInstance<TextEffectsSettings>();
            settings.hideFlags = HideFlags.HideAndDontSave;
            TextEffectsSettings.UseForTests(settings);

            // Written as a file and opened additively: NewScene refuses to run while the Editor has an unsaved untitled scene
            Directory.CreateDirectory(TempFolder);
            File.WriteAllText(ScenePath, SceneYaml);
            AssetDatabase.ImportAsset(ScenePath);
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            GameObject _go = scene.GetRootGameObjects()[0];

            tmp = _go.AddComponent<TextMeshPro>();
            tmp.text = OriginalText;
            effect = _go.AddComponent<EffectText>();
            effect.CharactersPerSecond = 10f;
            effect.PlainText = "abcdef";
            EditorSceneManager.SaveScene(scene, ScenePath);
            tmp.ForceMeshUpdate();
            preview = new EffectTextPreview(effect);
        }

        [TearDown]
        public void TearDown() {
            preview?.Stop();
            if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.DeleteAsset(TempFolder);
            TextEffectsSettings.ClearForTests();
            Object.DestroyImmediate(settings);
        }
        #endregion

        // ==================== TESTS ===================
        #region Tests
        [Test]
        public void Preview_Play_AdvancesVisibleCount() {
            preview.Play(null);
            Assert.IsTrue(preview.IsPlaying);
            Assert.IsTrue(effect.PreviewMode);
            int _start = effect.VisibleCount;

            for (int _i = 0; _i < 3; _i++) preview.Step(0.1f);
            Assert.Greater(effect.VisibleCount, _start);

            for (int _i = 0; _i < 10; _i++) preview.Step(0.1f);
            Assert.AreEqual(6, effect.VisibleCount);
            Assert.AreEqual(EffectTextState.Complete, effect.State);
        }

        [Test]
        public void Preview_Stop_RestoresOriginalText() {
            effect.PlainText = "<b>new</b> text";
            preview.Play(null);
            preview.Step(0.1f);
            Assert.AreNotEqual(OriginalText, tmp.text);

            preview.Stop();

            Assert.IsFalse(preview.IsPlaying);
            Assert.IsFalse(effect.PreviewMode);
            Assert.AreEqual(OriginalText, tmp.text);
            Assert.AreEqual(OriginalText.Length, tmp.textInfo.characterCount);
            TMP_CharacterInfo _first = tmp.textInfo.characterInfo[0];
            Color32 _color = tmp.textInfo.meshInfo[_first.materialReferenceIndex].colors32[_first.vertexIndex];
            Assert.AreEqual(tmp.color.a, _color.a / 255f, 0.01f);
        }

        [Test]
        public void Preview_DoesNotDirtyScene() {
            Assert.IsFalse(scene.isDirty);

            preview.Play(null);
            preview.Step(0.1f);
            preview.Skip();
            preview.Hide();
            preview.Step(1f);
            preview.Restart();
            preview.Step(0.1f);
            preview.Stop();

            Assert.IsFalse(scene.isDirty);
            Assert.AreEqual(OriginalText, tmp.text);
        }

        [Test]
        public void Preview_Events_LoggedNotExecuted() {
            effect.Events = new TextEventTable(new List<TextEventEntry> {
                new TextEventEntry("a", new ActionList(new FlagAction()))
            });
            effect.PlainText = "x<event=a>y";

            preview.Play(null);
            preview.Step(1f);

            CollectionAssert.AreEqual(new[] { "a" }, preview.EventLog);
            Assert.IsFalse(FlagAction.Ran);
            Assert.IsFalse(effect.Queue.IsRunning);
            Assert.AreEqual(0, effect.Queue.PendingCount);
        }
        #endregion
    }
}
