using UnityEngine;

namespace SafeMining
{
    // Shows the rigged worker (Resources/Mining/Worker) in place of the procedural PPE worker.
    // It sits inside MiningSimulation's "PPE worker model", so Story/FPP visibility keeps following the
    // simulation; the procedural parts stay (their pivots are still animated) but are no longer drawn.
    // Without the prefab this does nothing and the procedural worker remains.
    public sealed class MiningWorkerAvatar : MonoBehaviour
    {
        public const string PrefabPath = "Mining/Worker";
        public const float Scale = 1.3f; // team choice: a bit larger than the model's real 1.83 m; 2x hid the route on screen

        // Speeds (m/s at scale 1) the Mixamo clips were authored for; also the blend tree thresholds.
        public const float WalkClipSpeed = 1.45f, JogClipSpeed = 3f, RunClipSpeed = 5.5f;

        // Animator "Pose" values for a standing worker.
        public enum StandingPose { Idle, Radio, LookAround, Injured }

        static readonly int Speed = Animator.StringToHash("Speed");
        static readonly int LocoRate = Animator.StringToHash("LocoRate");
        static readonly int Pose = Animator.StringToHash("Pose");
        static readonly int RunStop = Animator.StringToHash("RunStop");

        MiningSimulation simulation;
        Animator animator;
        Vector3 lastPosition;
        float speed, runningUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Resources.Load<GameObject>(PrefabPath) == null) return;
            new GameObject("Rigged worker avatar").AddComponent<MiningWorkerAvatar>();
        }

        // Swaps the procedural parts under a "PPE worker model" for the rigged worker. Also used by the editor preview.
        public static GameObject Attach(Transform visual, string name)
        {
            var prefab = Resources.Load<GameObject>(PrefabPath);
            if (prefab == null) return null;
            foreach (var part in visual.GetComponentsInChildren<Renderer>(true)) part.forceRenderingOff = true; // not serialized
            var worker = Instantiate(prefab, visual, false);
            worker.name = name;
            worker.transform.localScale = Vector3.one * Scale;
            return worker;
        }

        void LateUpdate()
        {
            if (animator == null && !TryAttach()) return;
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            Vector3 delta = simulation.Actor.position - lastPosition; delta.y = 0;
            lastPosition = simulation.Actor.position;
            if (delta.magnitude > 1.5f) { delta = Vector3.zero; speed = 0; runningUntil = 0; } // session reset teleport, not a sprint
            speed = Mathf.Lerp(speed, delta.magnitude / dt, Mathf.Clamp01(dt * 10));

            // A bigger body takes longer strides, so the clips play slower for the same ground speed.
            float relative = speed / Scale;
            animator.SetFloat(Speed, relative);
            animator.SetFloat(LocoRate, Mathf.Clamp(relative / Mathf.Clamp(relative, WalkClipSpeed, RunClipSpeed), .5f, 1.6f));
            if (relative > (JogClipSpeed + RunClipSpeed) / 2) runningUntil = Time.time + .5f;
            if (relative < .2f && Time.time < runningUntil) { animator.SetTrigger(RunStop); runningUntil = 0; }
            animator.SetInteger(Pose, (int)CurrentPose(simulation));
        }

        // What a standing worker is doing, read from the simulation state and the Story hold stage.
        static StandingPose CurrentPose(MiningSimulation s)
        {
            bool live = s.State == SessionState.Running || s.State == SessionState.Blocked;
            if (!live) return StandingPose.Idle; // menu, pause and success keep a calm idle
            if (s.HazardContacts > 0) return StandingPose.Injured; // touched a closed area this session
            if (s.State == SessionState.Blocked) return StandingPose.LookAround; // route closed: searching for a way out
            if (s.Elapsed < 4) return StandingPose.Radio; // briefing
            if (!s.StoryHolding) return StandingPose.Idle;
            switch (s.StoryStep)
            {
                case StoryStage.Tremor:
                case StoryStage.Checking: return StandingPose.LookAround; // stopped by the tremor, checking the tunnel
                case StoryStage.Confirmed:
                case StoryStage.Mapping: return StandingPose.Radio; // reporting and receiving the new route
                default: return StandingPose.Idle;
            }
        }

        bool TryAttach()
        {
            if (simulation == null) simulation = FindFirstObjectByType<MiningSimulation>();
            var visual = simulation != null && simulation.Actor != null ? simulation.Actor.Find("PPE worker model") : null;
            var worker = visual != null ? Attach(visual, "Rigged worker") : null;
            if (worker == null) return false;
            animator = worker.GetComponent<Animator>();
            lastPosition = simulation.Actor.position;
            return animator != null;
        }
    }
}
