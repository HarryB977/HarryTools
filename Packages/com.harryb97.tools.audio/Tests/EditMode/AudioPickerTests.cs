using System.Collections.Generic;
using HarryTools.Audio.Editor;
using NUnit.Framework;
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
    }
}
