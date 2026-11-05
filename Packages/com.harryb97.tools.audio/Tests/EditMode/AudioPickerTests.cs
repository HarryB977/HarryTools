using System.Collections.Generic;
using HarryTools.Audio.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace HarryTools.Audio.Tests {
    public class AudioPickerTests {
        // ==================== VARIABLES ===================
        private readonly List<Object> created = new List<Object>();

        // ==================== SETUP ===================
        [TearDown]
        public void TearDown() {
            for (int _i = 0; _i < created.Count; _i++) if (created[_i] != null) Object.DestroyImmediate(created[_i]);
            created.Clear();
        }

        private T Make<T>(string _name, string _group) where T : AudioCatalogItem {
            var _asset = ScriptableObject.CreateInstance<T>();
            _asset.name = _name;
            _asset.SetId(System.Guid.NewGuid().ToString("N"));
            _asset.SetGroupOverride(_group);
            created.Add(_asset);
            return _asset;
        }

        private static AdvancedDropdownItem Child(AdvancedDropdownItem _parent, string _name) {
            foreach (AdvancedDropdownItem _child in _parent.children) if (_child.name == _name) return _child;
            return null;
        }

        // ==================== TESTS ===================
        [Test]
        public void BuildTree_FiltersByType() {
            var _items = new List<AudioCatalogItem> { Make<SoundAsset>("Click", ""), Make<VoiceAsset>("Hello", "") };
            AdvancedDropdownItem _root = AudioPickerDropdown.BuildTree(typeof(SoundAsset), _items, "Assets/Audio");
            Assert.IsNotNull(Child(_root, "Click"));
            Assert.IsNull(Child(_root, "Hello"));
        }

        [Test]
        public void BuildTree_GroupsHierarchically() {
            var _items = new List<AudioCatalogItem> { Make<SoundAsset>("Step1", "Footsteps/Wood") };
            AdvancedDropdownItem _root = AudioPickerDropdown.BuildTree(typeof(SoundAsset), _items, "Assets/Audio");
            AdvancedDropdownItem _footsteps = Child(_root, "Footsteps");
            Assert.IsNotNull(_footsteps);
            AdvancedDropdownItem _wood = Child(_footsteps, "Wood");
            Assert.IsNotNull(_wood);
            Assert.IsNotNull(Child(_wood, "Step1"));
        }

        [Test]
        public void BuildTree_HasNoneOption() {
            AdvancedDropdownItem _root = AudioPickerDropdown.BuildTree(typeof(SoundAsset), new List<AudioCatalogItem>(), "Assets/Audio");
            Assert.IsNotNull(Child(_root, "None"));
        }

        // ==================== ROBUSTNESS ===================
        [System.Serializable]
        private class Payload {
            public SoundAsset sound;
            public SoundRef sref;
            public SoundRef[] refs = new SoundRef[2];
        }

        private class RefHost : ScriptableObject {
            [SerializeReference] public object payload = new Payload();
        }

        [Test]
        public void Drawers_InsideSerializeReference_WorkWithoutFieldInfo() {
            var _host = ScriptableObject.CreateInstance<RefHost>();
            created.Add(_host);
            using (var _so = new SerializedObject(_host)) {
                SerializedProperty _sound = _so.FindProperty("payload.sound");
                SerializedProperty _sref = _so.FindProperty("payload.sref");
                SerializedProperty _refs = _so.FindProperty("payload.refs");
                Assert.IsNotNull(_sound);
                Assert.IsNotNull(_sref);
                Assert.AreEqual(2, _refs.arraySize);
                SerializedProperty _element = _refs.GetArrayElementAtIndex(0);

                Assert.AreEqual(typeof(SoundAsset), AudioPickerField.AssetTypeOfObject(null, _sound.type));
                Assert.AreEqual(typeof(SoundAsset), AudioPickerField.AssetTypeOfRef(_sref.type));
                Assert.AreEqual(typeof(SoundAsset), AudioPickerField.AssetTypeOfRef(_element.type));

                var _assetDrawer = new AudioAssetDrawer();
                var _refDrawer = new AudioRefDrawer();
                Assert.DoesNotThrow(() => {
                    _assetDrawer.GetPropertyHeight(_sound, GUIContent.none);
                    _refDrawer.GetPropertyHeight(_sref, GUIContent.none);
                    _refDrawer.GetPropertyHeight(_element, GUIContent.none);
                    Assert.IsNotNull(_assetDrawer.CreatePropertyGUI(_sound));
                    Assert.IsNotNull(_refDrawer.CreatePropertyGUI(_sref));
                    Assert.IsNotNull(_refDrawer.CreatePropertyGUI(_element));
                });
            }
        }

        [Test]
        public void AssetTypeOfObject_UnknownType_FallsBackToBase() {
            Assert.AreEqual(typeof(AudioCatalogItem), AudioPickerField.AssetTypeOfObject(null, "PPtr<$Nope>"));
            Assert.AreEqual(typeof(AudioCatalogItem), AudioPickerField.AssetTypeOfObject(null, null));
            Assert.IsNull(AudioPickerField.AssetTypeOfRef("Other"));
        }

        [Test]
        public void ElementType_HandlesArraysAndLists() {
            Assert.AreEqual(typeof(SoundRef), AudioPickerField.ElementType(typeof(SoundRef[])));
            Assert.AreEqual(typeof(SoundAsset), AudioPickerField.ElementType(typeof(List<SoundAsset>)));
            Assert.AreEqual(typeof(MusicRef), AudioPickerField.ElementType(typeof(MusicRef)));
            Assert.IsNull(AudioPickerField.ElementType(null));
        }

        [Test]
        public void BuildTree_SubclassFilter_IncludesDerivedOnly() {
            var _items = new List<AudioCatalogItem> { Make<SoundAsset>("A", ""), Make<MusicAsset>("B", "") };
            AdvancedDropdownItem _all = AudioPickerDropdown.BuildTree(typeof(AudioCatalogItem), _items, "Assets/Audio");
            Assert.IsNotNull(Child(_all, "A"));
            Assert.IsNotNull(Child(_all, "B"));
            AdvancedDropdownItem _music = AudioPickerDropdown.BuildTree(typeof(MusicAsset), _items, "Assets/Audio");
            Assert.IsNull(Child(_music, "A"));
            Assert.IsNotNull(Child(_music, "B"));
        }

        [Test]
        public void Find_UnknownOrEmptyId_ReturnsNull() {
            Assert.IsNull(AudioPickerField.Find(typeof(SoundAsset), null));
            Assert.IsNull(AudioPickerField.Find(typeof(SoundAsset), "does-not-exist"));
            Assert.IsNull(AudioPickerField.Find(null, "x"));
        }
    }
}
