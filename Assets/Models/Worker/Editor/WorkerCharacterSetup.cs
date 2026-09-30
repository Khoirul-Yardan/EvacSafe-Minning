using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace SafeMining.EditorTools
{
    // Import rules for the rigged worker in Assets/Models/Worker. They live in code so every teammate
    // gets the same Humanoid setup after a pull, without hand-tuned import settings.
    sealed class WorkerCharacterImport : AssetPostprocessor
    {
        public const string Root = "Assets/Models/Worker/";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root)) return;
            var importer = (ModelImporter)assetImporter;
            bool clip = assetPath.StartsWith(Root + "Animations/");
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.optimizeGameObjects = false; // smart glasses, headlamp and HeadlampSocket hang off the head bone
            importer.importCameras = importer.importLights = false;
            importer.importAnimation = clip;
            if (clip) importer.materialImportMode = ModelImporterMaterialImportMode.None;
        }

        void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(Root + "Animations/")) return;
            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            string name = Path.GetFileNameWithoutExtension(assetPath);
            bool oneShot = name.EndsWith("ToStop"); // the stop clip is not In Place and plays once
            foreach (var clip in clips)
            {
                clip.name = name; // Mixamo names every clip "mixamo.com"
                clip.loopTime = !oneShot;
                // The simulation moves the worker. In Place clips bake root motion into the pose; the stop clip's
                // forward travel stays on the root instead, so the body does not slide ahead of the worker.
                clip.lockRootRotation = clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = !oneShot;
                clip.keepOriginalOrientation = clip.keepOriginalPositionY = clip.keepOriginalPositionXZ = true;
            }
            importer.clipAnimations = clips;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root + "Textures/")) return;
            var importer = (TextureImporter)assetImporter;
            string name = Path.GetFileNameWithoutExtension(assetPath);
            if (name.EndsWith("_Normal")) importer.textureType = TextureImporterType.NormalMap;
            else if (!name.EndsWith("_BaseColor")) importer.sRGBTexture = false; // metallic, roughness, specular are data
        }
    }

    // One click after import: URP materials, the Animator controller and Resources/Mining/Worker.prefab.
    static class WorkerCharacterSetup
    {
        const string Root = WorkerCharacterImport.Root;
        const string Model = Root + "SafeMiningWorker.fbx";
        const string Controller = Root + "Animations/SafeMiningWorker.controller";
        const string Prefab = "Assets/Resources/Mining/Worker.prefab";

        [MenuItem("SafeMining/Worker/Build materials, animator and prefab")]
        public static void Build()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
            if (importer == null) { Debug.LogError("Worker model not found at " + Model); return; }

            Remap(importer, "M_Miner", Lit("M_Worker_Outfit", "T_Miner_BaseColor", "T_Miner_Normal", .3f));
            Remap(importer, "M_m_body", Lit("M_Worker_Body", "T_m_body_BaseColor", "T_m_body_Normal", .35f));
            Remap(importer, "M_m_head", Lit("M_Worker_Head", "T_m_head_BaseColor", "T_m_head_Normal", .4f));
            Remap(importer, "M_m_eye", Lit("M_Worker_Eye", "T_Eyeball_BaseColor", null, .85f));
            Remap(importer, "M_Headlamp", Lit("M_Worker_Headlamp", "T_Headlamp_BaseColor", "T_Headlamp_Normal", .5f));
            var frame = Lit("M_Worker_GlassesFrame", null, null, .65f, .6f); frame.SetColor("_BaseColor", new Color(.035f, .04f, .045f));
            Remap(importer, "M_SmartGlasses_Frame", frame);
            Remap(importer, "M_SmartGlasses_Lens", Lens());
            importer.SaveAndReimport();

            var controller = BuildController();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
            var animator = instance.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false; // MiningSimulation owns the worker's position
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = ShadowCastingMode.On;
            // Import bounds come from the T-pose; posed limbs can leave them and get culled. One character, so the cost is small.
            foreach (var skinned in instance.GetComponentsInChildren<SkinnedMeshRenderer>()) skinned.updateWhenOffscreen = true;
            PrefabUtility.SaveAsPrefabAsset(instance, Prefab);
            Object.DestroyImmediate(instance);
            AssetDatabase.SaveAssets();
            Debug.Log("Worker character built: " + Prefab);
        }

        static void Remap(ModelImporter importer, string source, Material material) =>
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source), material);

        static Material Lit(string name, string baseMap, string normalMap, float smoothness, float metallic = 0)
        {
            string path = Root + "Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            if (baseMap != null) material.SetTexture("_BaseMap", Texture(baseMap));
            if (normalMap != null) { material.SetTexture("_BumpMap", Texture(normalMap)); material.EnableKeyword("_NORMALMAP"); }
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material Lens()
        {
            // Tinted AR lens: transparent teal with a faint glow, the same hue as the HUD's system colour.
            var lens = Lit("M_Worker_GlassesLens", null, null, .9f);
            lens.SetColor("_BaseColor", new Color(.06f, .55f, .66f, .45f));
            lens.SetFloat("_Surface", 1); lens.SetFloat("_Blend", 0); lens.SetFloat("_ZWrite", 0);
            lens.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); lens.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            lens.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); lens.renderQueue = (int)RenderQueue.Transparent;
            lens.SetOverrideTag("RenderType", "Transparent");
            lens.SetColor("_EmissionColor", new Color(.1f, .75f, .9f) * .6f); lens.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(lens);
            return lens;
        }

        static Texture2D Texture(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Textures/" + name + ".png");

        static AnimationClip Clip(string name) => AssetDatabase.LoadAllAssetsAtPath(Root + "Animations/Mixamo/" + name + ".fbx")
            .OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));

        static AnimatorController BuildController()
        {
            // Rebuilt in place: deleting and recreating the asset leaves loaded prefabs pointing at a destroyed controller.
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller) ?? AnimatorController.CreateAnimatorControllerAtPath(Controller);
            foreach (var parameter in controller.parameters) controller.RemoveParameter(parameter);
            var old = controller.layers[0].stateMachine;
            foreach (var child in old.states) old.RemoveState(child.state);
            foreach (var stale in AssetDatabase.LoadAllAssetsAtPath(Controller).OfType<BlendTree>()) Object.DestroyImmediate(stale, true);
            // Speed is the worker's speed divided by the character scale, so the stride matches the ground.
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("LocoRate", AnimatorControllerParameterType.Float);
            controller.AddParameter("Radio", AnimatorControllerParameterType.Bool);
            controller.AddParameter("RunStop", AnimatorControllerParameterType.Trigger);
            var parameters = controller.parameters; parameters[1].defaultFloat = 1; controller.parameters = parameters;

            var machine = controller.layers[0].stateMachine;
            var idle = machine.AddState("Idle"); idle.motion = Clip("Worker_Idle");
            machine.defaultState = idle;

            // Walk, jog and run blend by speed; each threshold is the speed the clip was authored for.
            var locomotion = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree);
            tree.blendParameter = "Speed"; tree.useAutomaticThresholds = false;
            tree.AddChild(Clip("Worker_Walk"), MiningWorkerAvatar.WalkClipSpeed);
            if (Clip("Worker_Jog") != null) tree.AddChild(Clip("Worker_Jog"), MiningWorkerAvatar.JogClipSpeed);
            if (Clip("Worker_Run") != null) tree.AddChild(Clip("Worker_Run"), MiningWorkerAvatar.RunClipSpeed);
            locomotion.speedParameterActive = true; locomotion.speedParameter = "LocoRate";

            Link(idle, locomotion, AnimatorConditionMode.Greater, "Speed", .2f);
            var stopClip = Clip("Worker_RunToStop");
            if (stopClip != null)
            {
                // Listed before Locomotion -> Idle, so a stop from a run wins over the plain idle blend.
                var stop = machine.AddState("Run to stop"); stop.motion = stopClip;
                Link(locomotion, stop, AnimatorConditionMode.If, "RunStop", 0);
                var toIdle = stop.AddTransition(idle); toIdle.hasExitTime = true; toIdle.exitTime = .9f; toIdle.duration = .15f;
                Link(stop, locomotion, AnimatorConditionMode.Greater, "Speed", .2f);
            }
            Link(locomotion, idle, AnimatorConditionMode.Less, "Speed", .2f);

            var radioClip = Clip("Worker_RadioTalk");
            if (radioClip != null)
            {
                var radio = machine.AddState("Radio"); radio.motion = radioClip;
                Link(idle, radio, AnimatorConditionMode.If, "Radio", 0);
                Link(radio, idle, AnimatorConditionMode.IfNot, "Radio", 0);
                Link(radio, locomotion, AnimatorConditionMode.Greater, "Speed", .2f);
            }
            AssetDatabase.SaveAssets();
            return controller;
        }

        static void Link(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, string parameter, float threshold)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = false; transition.duration = .25f;
            transition.AddCondition(mode, threshold, parameter);
        }
    }
}
