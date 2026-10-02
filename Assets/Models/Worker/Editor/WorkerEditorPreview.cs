using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SafeMining.EditorTools
{
    // Shows the rigged worker in the saved EDITOR PREVIEW too, standing in the idle pose.
    // The instance is DontSave and the procedural parts are hidden with forceRenderingOff (not serialized),
    // so the scene file never changes and Rebuild Editor Preview keeps working as before.
    [InitializeOnLoad]
    static class WorkerEditorPreview
    {
        const string Name = "Rigged worker (editor preview)";
        static bool pending = true;

        static WorkerEditorPreview()
        {
            // Changes only mark the preview as pending; the next editor tick applies it once the scene and assets are ready.
            EditorApplication.hierarchyChanged += () => pending = true;
            EditorSceneManager.sceneOpened += (_, _) => pending = true;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode) Clear();
                if (state == PlayModeStateChange.EnteredEditMode) pending = true;
            };
            EditorApplication.update += () =>
            {
                if (!pending || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                pending = false;
                Refresh();
            };
        }

        static void Refresh()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            foreach (var visual in Visuals())
            {
                var existing = visual.Find(Name);
                if (existing != null) { existing.localScale = Vector3.one * MiningWorkerAvatar.Scale; continue; } // picks up a changed Scale
                var worker = MiningWorkerAvatar.Attach(visual, Name);
                if (worker == null) return;
                foreach (var t in worker.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
                var idle = AssetDatabase.LoadAllAssetsAtPath("Assets/Models/Worker/Animations/Mixamo/Worker_Idle.fbx")
                    .OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                if (idle != null) idle.SampleAnimation(worker, 0); // idle pose instead of the T-pose
            }
        }

        static void Clear()
        {
            foreach (var visual in Visuals())
            {
                var worker = visual.Find(Name);
                if (worker != null) Object.DestroyImmediate(worker.gameObject);
            }
        }

        static System.Collections.Generic.IEnumerable<Transform> Visuals()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        if (t.name == "PPE worker model") yield return t;
            }
        }
    }
}
