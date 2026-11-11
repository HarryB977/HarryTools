using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarryTools.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HarryTools.Flags.Tests
{
    /// <summary>Temporary scenes under Assets/__FlagsTests__ for the usage index tests.</summary>
    internal static class FlagUsageTestScenes
    {
        // ==================== VARIABLES ====================

        internal const string Folder = "Assets/__FlagsTests__";

        // Written as a file and opened additively: NewScene refuses to run while the Editor has an unsaved untitled scene.
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
            "  m_Name: Host\n" +
            "  m_IsActive: 1\n" +
            "--- !u!4 &200\n" +
            "Transform:\n" +
            "  m_GameObject: {fileID: 100}\n" +
            "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n" +
            "  m_LocalPosition: {x: 0, y: 0, z: 0}\n" +
            "  m_LocalScale: {x: 1, y: 1, z: 1}\n" +
            "  m_Children: []\n" +
            "  m_Father: {fileID: 0}\n";

        private static readonly FieldInfo RunnerActions = typeof(ActionRunner).GetField("actions", BindingFlags.Instance | BindingFlags.NonPublic);

        // ==================== METHODS ====================

        public static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__FlagsTests__");
        }

        /// <summary>Writes a scene with one root object named "Host" and imports it.</summary>
        public static string Write(string _name)
        {
            string _path = Folder + "/" + _name + ".unity";
            File.WriteAllText(_path, SceneYaml);
            AssetDatabase.ImportAsset(_path);
            return _path;
        }

        public static Scene Open(string _path, List<Scene> _opened)
        {
            Scene _scene = EditorSceneManager.OpenScene(_path, OpenSceneMode.Additive);
            _opened.Add(_scene);
            return _scene;
        }

        public static GameObject Host(Scene _scene) => _scene.GetRootGameObjects().Single(_root => _root.name == "Host");

        /// <summary>A child of the scene's Host object.</summary>
        public static GameObject Child(Scene _scene, string _name)
        {
            GameObject _child = new GameObject(_name);
            _child.transform.SetParent(Host(_scene).transform, false);
            return _child;
        }

        public static ActionRunner AddRunner(GameObject _go, ActionList _actions)
        {
            ActionRunner _runner = _go.AddComponent<ActionRunner>();
            RunnerActions.SetValue(_runner, _actions);
            return _runner;
        }

        public static void CloseAll(List<Scene> _opened)
        {
            foreach (Scene _scene in _opened)
                if (_scene.IsValid()) EditorSceneManager.CloseScene(_scene, true);
            _opened.Clear();
        }
    }
}
