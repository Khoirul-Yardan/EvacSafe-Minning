using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using Debug = UnityEngine.Debug;

namespace SafeMining
{
    public enum MiningMode { Story, FirstPerson }
    public enum SessionState { Menu, Running, Paused, Success, Blocked }

    public class MiningSimulation : MonoBehaviour
    {
        [Header("Denah prosedural (ubah sebelum Play; X/Y = grid X/Z)")]
        public List<MineCorridor> corridors = MineLayout.DefaultCorridors();
        [Header("Longsor dinamis")]
        public HazardScenarioMode scenarioMode = HazardScenarioMode.Random;
        public bool randomSeedOnLaunch = true;
        public int scenarioSeed = 17023;
        [Range(1, 12)] public int randomEventCount = 3;
        [Min(4)] public float firstWarningSeconds = 6;
        [Min(2)] public float secondsBetweenEvents = 6;
        [Min(2)] public float warningDurationSeconds = 4;
        [Min(0)] public float warningRiskPenalty = 24;
        public int ActiveSeed { get; private set; }
        public HazardScenarioMode ActiveScenario { get; private set; }
        public List<Vector2Int> HazardSites { get; private set; } = new List<Vector2Int>();
        public List<ScheduledRockfall> Schedule { get; private set; } = new List<ScheduledRockfall>();
        public string LastDetectorAlert { get; private set; } = "Detektor aktif - kondisi normal";
        public MiningLandslideDetector[] Detectors { get; private set; }
        public MiningMode Mode { get; private set; }
        public SessionState State { get; private set; } = SessionState.Menu;
        public bool Adaptive { get; private set; } = true;
        public HashSet<Vector2Int> Cells { get; private set; }
        public readonly HashSet<Vector2Int> Blocked = new HashSet<Vector2Int>();
        public int[] HazardLevels { get; private set; } = new int[0]; // 0 normal, 1 warning, 2 closed
        public List<Vector2Int> Route { get; private set; } = new List<Vector2Int>();
        public int TargetExit { get; private set; } = -1;
        public float Elapsed { get; private set; }
        public float Travelled { get; private set; }
        public float Exposure { get; private set; }
        public int Reroutes { get; private set; }
        public float ResponseMs { get; private set; }
        public float MaxResponseMs { get; private set; }
        public int HazardContacts { get; private set; }
        public string Dialogue { get; private set; }
        public string ExportStatus { get; private set; }
        public Transform Actor { get; private set; }
        public Camera ViewCamera { get; private set; }
        public string Phase { get; private set; } = "Persiapan";
        public bool GlassesEnabled { get; private set; } = true;
        public float RouteDistance
        {
            get
            {
                if (Route.Count == 0) return 0;
                float distance = Vector3.Distance(Flat(Actor.position), MineLayout.World(Route[0]));
                for (int i = 1; i < Route.Count; i++) distance += Vector3.Distance(MineLayout.World(Route[i - 1]), MineLayout.World(Route[i]));
                return distance;
            }
        }

        CharacterController body;
        Transform workerVisual, leftLeg, rightLeg, leftArm, rightArm;
        GameObject[] rubble;
        bool[] warningSent, collapseSent;
        float activeWarningPenalty;
        readonly List<GameObject> arrows = new List<GameObject>();
        Material arrowMaterial;
        AudioSource radioAlarm;
        MiningHUD hud;
        MiningTelemetry telemetry;
        float pitch, gravityVelocity, nextPlan;
        bool lastContact;
        Vector2Int lastCell;
        Vector3 previousPosition;
        readonly List<string> eventRows = new List<string>();

        void Awake()
        {
            try { Cells = MineLayout.CreateCells(corridors); }
            catch (ArgumentException e)
            {
                Debug.LogError("Denah tambang tidak valid: " + e.Message, this);
                enabled = false; return;
            }
            var environment = new GameObject("Environment Layer | Mine").transform; environment.SetParent(transform, false);
            MineLayout.Build(environment, Cells);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.22f, .24f, .27f);
            RenderSettings.fog = true; RenderSettings.fogColor = new Color(.045f, .055f, .06f);
            RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = .017f;
            HazardSites = MiningHazardScenario.DetectorSites(Cells);
            BuildActor(); BuildHazards();
            if (randomSeedOnLaunch) scenarioSeed = Guid.NewGuid().GetHashCode();
            hud = gameObject.AddComponent<MiningHUD>(); hud.Simulation = this;
            telemetry = GetComponent<MiningTelemetry>() ?? gameObject.AddComponent<MiningTelemetry>(); telemetry.Simulation = this;
            arrowMaterial = MineLayout.Material("AR navigation green", new Color(.03f, 1f, .58f), true);
            for (int i = 0; i < 28; i++)
            {
                var arrow = new GameObject("AR route chevron"); arrow.transform.SetParent(transform, false);
                for (int side = -1; side <= 1; side += 2)
                {
                    var bar = MineLayout.Box(arrow.transform, "Chevron", new Vector3(side * .18f, 0, -.1f), new Vector3(.1f, .024f, .54f), arrowMaterial, false);
                    bar.transform.localRotation = Quaternion.Euler(0, side * -42, 0);
                }
                arrow.SetActive(false); arrows.Add(arrow);
            }
            Dialogue = "Pilih navigasi adaptif atau statis, lalu mulai Mode Cerita.";
            UpdateCamera(true);
        }

        void BuildActor(bool documentation = false)
        {
            Actor = new GameObject("Worker | Story + FPP").transform; Actor.SetParent(transform, false);
            Actor.gameObject.layer = 2; // Exclude the worker's own capsule from camera obstruction probes.
            Actor.position = MineLayout.World(MineLayout.Spawn) + Vector3.up * .05f;
            body = Actor.gameObject.AddComponent<CharacterController>(); body.height = 1.8f; body.center = Vector3.up * .9f;
            body.radius = .3f; body.stepOffset = .25f; body.skinWidth = .035f;
            workerVisual = new GameObject("PPE worker model").transform; workerVisual.SetParent(Actor, false);
            var orange = MineLayout.Material("Safety orange", new Color(.95f, .32f, .045f));
            var dark = MineLayout.Material("Boots and gloves", new Color(.06f, .07f, .075f));
            var yellow = MineLayout.Material("Helmet", new Color(1f, .72f, .06f));
            var skin = MineLayout.Material("Skin", new Color(.57f, .34f, .21f));
            var silver = MineLayout.Material("Reflective stripes", new Color(.75f, .84f, .8f));
            var glass = MineLayout.Material("Safety glasses", new Color(.015f, .38f, .48f));
            RoundedPart(workerVisual, "Suit torso", PrimitiveType.Capsule, new Vector3(0, 1.15f, 0), new Vector3(.49f, .32f, .31f), orange);
            MineLayout.Box(workerVisual, "Reflective belt", new Vector3(0, .98f, 0), new Vector3(.51f, .075f, .31f), silver, false);
            for (int side = -1; side <= 1; side += 2)
                MineLayout.Box(workerVisual, "Reflective shoulder stripe", new Vector3(side * .16f, 1.24f, .151f), new Vector3(.045f, .3f, .025f), silver, false);
            for (int side = -1; side <= 1; side += 2)
            {
                var leg = new GameObject("Leg pivot").transform; leg.SetParent(workerVisual, false); leg.localPosition = new Vector3(side * .14f, .9f, 0);
                RoundedPart(leg, "Work trousers", PrimitiveType.Capsule, new Vector3(0, -.33f, 0), new Vector3(.22f, .36f, .25f), orange);
                MineLayout.Box(leg, "Reflector", new Vector3(0, -.5f, 0), new Vector3(.23f, .06f, .26f), silver, false);
                RoundedPart(leg, "Safety boot", PrimitiveType.Capsule, new Vector3(0, -.77f, .055f), new Vector3(.24f, .105f, .38f), dark);
                var arm = new GameObject("Arm pivot").transform; arm.SetParent(workerVisual, false); arm.localPosition = new Vector3(side * .34f, 1.38f, 0);
                RoundedPart(arm, "Sleeve", PrimitiveType.Capsule, new Vector3(0, -.2f, 0), new Vector3(.18f, .24f, .2f), orange);
                RoundedPart(arm, "Glove", PrimitiveType.Sphere, new Vector3(0, -.45f, 0), new Vector3(.17f, .2f, .19f), dark);
                if (side < 0) { leftLeg = leg; leftArm = arm; } else { rightLeg = leg; rightArm = arm; }
            }
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere); head.name = "Head"; head.transform.SetParent(workerVisual, false);
            head.transform.localPosition = Vector3.up * 1.62f; head.transform.localScale = new Vector3(.32f, .36f, .31f);
            head.GetComponent<Renderer>().sharedMaterial = skin; head.GetComponent<Collider>().enabled = false;
            RoundedPart(workerVisual, "Hard hat", PrimitiveType.Sphere, new Vector3(0, 1.76f, 0), new Vector3(.39f, .27f, .38f), yellow);
            RoundedPart(workerVisual, "Helmet brim", PrimitiveType.Sphere, new Vector3(0, 1.73f, .035f), new Vector3(.45f, .055f, .48f), yellow);
            RoundedPart(workerVisual, "Helmet lamp", PrimitiveType.Sphere, new Vector3(0, 1.8f, .19f), new Vector3(.09f, .09f, .07f), silver);
            MineLayout.Box(workerVisual, "AR safety glasses", new Vector3(0, 1.64f, .154f), new Vector3(.32f, .105f, .04f), glass, false);
            MineLayout.Box(workerVisual, "Rescue backpack", new Vector3(0, 1.17f, -.24f), new Vector3(.37f, .47f, .22f), dark, false);
            var cameraGO = new GameObject("Simulation camera", typeof(Camera), typeof(AudioListener)); cameraGO.transform.SetParent(transform, false);
            ViewCamera = cameraGO.GetComponent<Camera>(); ViewCamera.nearClipPlane = .06f; ViewCamera.farClipPlane = 180; ViewCamera.fieldOfView = 72;
            ViewCamera.clearFlags = CameraClearFlags.SolidColor; ViewCamera.backgroundColor = RenderSettings.fogColor;
            var light = cameraGO.AddComponent<Light>(); light.type = LightType.Spot; light.range = 24; light.spotAngle = 88;
            light.intensity = 5; light.color = new Color(1f, .91f, .72f); light.shadows = LightShadows.Soft;
            if (documentation) return;
            radioAlarm = cameraGO.AddComponent<AudioSource>(); radioAlarm.playOnAwake = false; radioAlarm.volume = .15f;
            const int sampleRate = 22050;
            var samples = new float[sampleRate / 2];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / sampleRate;
                samples[i] = Mathf.Sin(t * Mathf.PI * 2 * (t < .25f ? 660 : 880)) * Mathf.Sin(t * Mathf.PI * 2);
            }
            radioAlarm.clip = AudioClip.Create("Local evacuation alert", samples.Length, 1, sampleRate, false); radioAlarm.clip.SetData(samples, 0);
        }

        static void RoundedPart(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material; go.GetComponent<Collider>().enabled = false;
        }

#if UNITY_EDITOR
        // Reuse the exact runtime model builders without starting a session or creating UI/input.
        public static void CreateDocumentationActors(Transform parent, HashSet<Vector2Int> cells)
        {
            var host = new GameObject("Temporary documentation builder"); host.SetActive(false);
            var builder = host.AddComponent<MiningSimulation>();
            builder.Cells = cells; builder.HazardSites = MiningHazardScenario.DetectorSites(cells);
            builder.BuildActor(true); builder.BuildHazards();
            while (host.transform.childCount > 0) host.transform.GetChild(0).SetParent(parent, true);
            DestroyImmediate(host);
        }
#endif

        void BuildHazards()
        {
            var material = MineLayout.Material("Fallen shale", new Color(.28f, .24f, .21f));
            rubble = new GameObject[HazardSites.Count]; HazardLevels = new int[HazardSites.Count];
            Detectors = new MiningLandslideDetector[HazardSites.Count];
            var prefab = Resources.Load<GameObject>("Mining/LandslideDetector");
            for (int i = 0; i < rubble.Length; i++)
            {
                var root = new GameObject("Landslide " + (i + 1)); root.transform.SetParent(transform, false); root.transform.position = MineLayout.World(HazardSites[i]);
                for (int j = 0; j < 22; j++)
                {
                    var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere); rock.transform.SetParent(root.transform, false);
                    float x = (j % 5 - 2) * 1.05f, z = (j / 5 - 2) * .75f;
                    rock.transform.localPosition = new Vector3(x, .35f + (1 - Mathf.Abs(x) / 3) * (j % 3) * .38f, z);
                    rock.transform.localScale = new Vector3(1.15f, .9f + j % 3 * .2f, 1.2f);
                    rock.transform.localRotation = Quaternion.Euler(j * 33, j * 17, j * 49); rock.GetComponent<Renderer>().sharedMaterial = material;
                    var shard = Instantiate(rock.GetComponent<MeshFilter>().sharedMesh);
                    Vector3[] vertices = shard.vertices;
                    for (int v = 0; v < vertices.Length; v++)
                        vertices[v] *= .76f + Mathf.PerlinNoise(vertices[v].x * 7 + 40 + j, vertices[v].y * 7 + vertices[v].z * 3 + 40) * .55f;
                    shard.vertices = vertices; shard.RecalculateNormals(); rock.GetComponent<MeshFilter>().sharedMesh = shard;
                }
                // The barrier spans the full tunnel; the trigger shell is also used for exposure feedback.
                var barrier = root.AddComponent<BoxCollider>(); barrier.center = new Vector3(0, 2, 0); barrier.size = new Vector3(6, 4, 5.8f);
                root.AddComponent<MiningRockfall>().Simulation = this; rubble[i] = root; root.SetActive(false);
                var device = prefab != null ? Instantiate(prefab) : MiningLandslideDetector.CreateModel();
                device.transform.SetParent(transform, false);
                Vector2Int wall = Vector2Int.right;
                foreach (var direction in MineLayout.Directions)
                    if (!Cells.Contains(HazardSites[i] + direction)) { wall = direction; break; }
                var outward = new Vector3(wall.x, 0, wall.y);
                device.transform.position = MineLayout.World(HazardSites[i]) + outward * 2.48f + Vector3.up * 2.4f;
                device.transform.rotation = Quaternion.LookRotation(outward);
                Detectors[i] = device.GetComponent<MiningLandslideDetector>();
                Detectors[i].Configure(Application.isPlaying ? this : null, i, HazardSites[i]);
            }
        }

        public void Begin(MiningMode mode, bool adaptive)
        {
            if (Actor == null) return;
            // Keep the legacy signature for scene/tool compatibility; research sessions use Story only.
            mode = MiningMode.Story;
            Mode = mode; Adaptive = adaptive; State = SessionState.Running;
            Elapsed = Travelled = Exposure = ResponseMs = MaxResponseMs = 0; Reroutes = HazardContacts = 0;
            pitch = 0; gravityVelocity = 0; nextPlan = 0; lastContact = false; GlassesEnabled = true;
            Blocked.Clear(); eventRows.Clear(); ExportStatus = "";
            ActiveSeed = scenarioSeed; ActiveScenario = scenarioMode; activeWarningPenalty = Mathf.Max(0, warningRiskPenalty);
            Schedule = MiningHazardScenario.Create(Cells, HazardSites, ActiveScenario, ActiveSeed,
                randomEventCount, firstWarningSeconds, secondsBetweenEvents, warningDurationSeconds);
            warningSent = new bool[Schedule.Count]; collapseSent = new bool[Schedule.Count];
            LastDetectorAlert = "Detektor aktif - kondisi normal";
            for (int i = 0; i < HazardLevels.Length; i++) { HazardLevels[i] = 0; rubble[i].SetActive(false); Detectors[i].SetLevel(0); }
            body.enabled = false; Actor.position = MineLayout.World(MineLayout.Spawn) + Vector3.up * .04f; Actor.rotation = Quaternion.identity; body.enabled = true;
            previousPosition = Actor.position; lastCell = MineLayout.Cell(Actor.position);
            workerVisual.gameObject.SetActive(mode == MiningMode.Story);
            Phase = "01 / Briefing";
            Dialogue = "Tim: Kita berada di galeri produksi. Amati detektor kuning di dinding. Lampu kuning berarti waspada; merah berarti jalur tertutup.";
            Plan(false); LogEvent("start", mode + "/" + (adaptive ? "adaptive" : "static")); SetCursor(); UpdateCamera(true);
        }

        public void TogglePause()
        {
            if (State == SessionState.Running) State = SessionState.Paused;
            else if (State == SessionState.Paused) State = SessionState.Running;
            SetCursor();
        }
        public void Menu() { State = SessionState.Menu; SetCursor(); }
        void SetCursor() { bool capture = State == SessionState.Running && Mode == MiningMode.FirstPerson; Cursor.lockState = capture ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !capture; }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame) TogglePause();
                if (keyboard.rKey.wasPressedThisFrame && State != SessionState.Menu) Begin(Mode, Adaptive);
            }
            if (State != SessionState.Running) { UpdateCamera(false); UpdateArrows(); return; }
            float dt = Mathf.Min(Time.deltaTime, .05f);
            Elapsed += dt;
            RunTimeline();
            if (State != SessionState.Running) { UpdateCamera(false); UpdateArrows(); return; }
            if (Mode == MiningMode.Story) MoveStory(dt); else MovePlayer(dt);
            if (body.isGrounded) gravityVelocity = -2; else gravityVelocity -= 20 * dt;
            body.Move(Vector3.up * (gravityVelocity * dt));
            Travelled += Vector3.Distance(Flat(previousPosition), Flat(Actor.position)); previousPosition = Actor.position;
            CheckExposure(dt);
            Vector2Int cell = MineLayout.Cell(Actor.position);
            if (Adaptive && Mode == MiningMode.FirstPerson && (cell != lastCell || Elapsed >= nextPlan))
            { Plan(false); nextPlan = Elapsed + .5f; lastCell = cell; }
            TrimRoute();
            for (int i = 0; i < MineLayout.Exits.Length; i++)
                if (Vector3.Distance(Flat(Actor.position), MineLayout.World(MineLayout.Exits[i])) < 1.8f)
                {
                    TargetExit = i; State = SessionState.Success; Phase = "05 / Evakuasi selesai";
                    Dialogue = "Tim: Kita sudah berada di zona aman. Lakukan pendataan anggota dan tunggu arahan petugas.";
                    LogEvent("success", "refuge_" + (i + 1)); SetCursor(); break;
                }
            UpdateCamera(false); UpdateArrows();
            if (Actor.position.y < -3) { State = SessionState.Blocked; Dialogue = "Posisi di luar map. Tekan R untuk mengulang."; SetCursor(); }
        }

        public void NewRandomScenario()
        {
            if (State != SessionState.Menu) return;
            scenarioMode = HazardScenarioMode.Random; scenarioSeed = Guid.NewGuid().GetHashCode();
        }

        void RunTimeline()
        {
            for (int i = 0; i < Schedule.Count && State == SessionState.Running; i++)
            {
                var item = Schedule[i];
                if (!warningSent[i] && Elapsed >= item.warningTime)
                { warningSent[i] = true; SetHazard(item.detectorIndex, 1); }
                if (!collapseSent[i] && State == SessionState.Running && Elapsed >= item.collapseTime)
                { collapseSent[i] = true; SetHazard(item.detectorIndex, 2); }
            }
        }

        public void SetHazard(int index, int level)
        {
            if (index < 0 || index >= HazardLevels.Length || level < 0 || level > 2 || HazardLevels[index] == level) return;
            HazardLevels[index] = level;
            if (level == 2) Blocked.Add(HazardSites[index]); else Blocked.Remove(HazardSites[index]);
            rubble[index].SetActive(level == 2); Detectors[index].SetLevel(level);
            string station = "D" + (index + 1).ToString("00");
            LastDetectorAlert = station + " | " + (level == 0 ? "NORMAL" : level == 1 ? "AWAS LONGSOR - lampu kuning" : "JALUR TERTUTUP - lampu merah") +
                " | grid " + HazardSites[index].x + ":" + HazardSites[index].y;
            Phase = level == 2 ? "Longsor / " + station : level == 1 ? "Peringatan / " + station : "Pembaruan / " + station;
            Dialogue = "Detektor " + station + (level == 1 ? ": getaran meningkat. Periksa lampu dan sirene di lorong." :
                level == 2 ? ": longsor menutup lorong. " + (Adaptive ? "Mencari rute menuju zona aman." : "Baseline tetap memakai rute awal.") : ": kondisi lokasi diperbarui menjadi normal.");
            if (radioAlarm != null) radioAlarm.Play();
            LogEvent("hazard", index + ":" + level);
            if (Adaptive) Plan(true);
            if (level == 2 && MineLayout.Cell(Actor.position) == HazardSites[index])
            {
                HazardContacts++; State = SessionState.Blocked; Phase = "Terpapar longsor";
                Dialogue = "Longsor terjadi di posisi Anda. Sesi dihentikan. Ulangi latihan dan hindari lorong yang sudah diberi peringatan.";
                LogEvent("blocked", "landslide_at_player"); SetCursor();
            }
        }

        void Plan(bool hazardChange)
        {
            var watch = Stopwatch.StartNew();
            var start = MineLayout.Cell(Actor.position);
            var risk = new Dictionary<Vector2Int, float>();
            if (Adaptive) for (int i = 0; i < HazardSites.Count; i++) if (HazardLevels[i] == 1) risk[HazardSites[i]] = activeWarningPenalty;
            var newPath = MineLayout.FindRiskAwarePath(Cells, start, Adaptive ? Blocked : new HashSet<Vector2Int>(), risk, out int exit);
            // Ignore consumed route prefixes when comparing the remaining decisions.
            int oldIndex = Route.Count > 0 && Route[0] == start ? 1 : 0;
            int newIndex = newPath.Count > 0 && newPath[0] == start ? 1 : 0;
            bool changed = TargetExit != exit || Route.Count - oldIndex != newPath.Count - newIndex;
            if (!changed) for (int i = 0; i < Route.Count - oldIndex; i++)
                if (Route[i + oldIndex] != newPath[i + newIndex]) { changed = true; break; }
            if (hazardChange && changed) Reroutes++;
            Route = newPath; TargetExit = exit;
            if (Route.Count > 1)
            {
                Vector3 heading = (MineLayout.World(Route[1]) - MineLayout.World(Route[0])).normalized;
                Vector3 offset = Flat(Actor.position) - MineLayout.World(Route[0]);
                float progress = Vector3.Dot(offset, heading);
                if (progress > 0 && (offset - heading * progress).magnitude < 1.5f) Route.RemoveAt(0);
            }
            // Start at the current cell centre; skipping it can cut diagonally through a junction wall.
            TrimRoute(); watch.Stop(); ResponseMs = (float)watch.Elapsed.TotalMilliseconds;
            if (hazardChange) { MaxResponseMs = Mathf.Max(MaxResponseMs, ResponseMs); LogEvent("route_update", "exit_" + exit); }
            if (Route.Count == 0)
            {
                State = SessionState.Blocked; Phase = "Tidak ada rute aman";
                Dialogue = "Tidak ada rute aman dari posisi pekerja. Sesi dihentikan dan dicatat sebagai terhalang.";
                LogEvent("blocked", "no_safe_route"); SetCursor();
            }
        }

        void TrimRoute()
        {
            while (Route.Count > 1 && Vector3.Distance(Flat(Actor.position), MineLayout.World(Route[0])) < .4f) Route.RemoveAt(0);
        }

        void MoveStory(float dt)
        {
            if (Elapsed < 4 || Route.Count == 0) return;
            Vector3 delta = MineLayout.World(Route[0]) - Flat(Actor.position);
            if (Blocked.Contains(Route[0]) && delta.magnitude < 4.2f)
            {
                State = SessionState.Blocked; Phase = "Baseline terhalang";
                Dialogue = "Rute statis terhalang longsor. Sesi berhenti tanpa menerobos bahaya. Bandingkan dengan navigasi adaptif pada skenario yang sama.";
                LogEvent("blocked", "static_route"); SetCursor(); return;
            }
            if (delta.sqrMagnitude > .01f)
            {
                Actor.rotation = Quaternion.Slerp(Actor.rotation, Quaternion.LookRotation(delta), dt * 8);
                body.Move(delta.normalized * Mathf.Min(delta.magnitude, 2.8f * dt));
                float swing = Mathf.Sin(Elapsed * 8) * 25;
                leftLeg.localRotation = Quaternion.Euler(swing, 0, 0); rightLeg.localRotation = Quaternion.Euler(-swing, 0, 0);
                leftArm.localRotation = Quaternion.Euler(-swing * .7f, 0, 0); rightArm.localRotation = Quaternion.Euler(swing * .7f, 0, 0);
            }
        }

        void MovePlayer(float dt)
        {
            Vector2 look = Mouse.current != null && Cursor.lockState == CursorLockMode.Locked ? Mouse.current.delta.ReadValue() * .09f : Vector2.zero;
            if (Gamepad.current != null) look += Gamepad.current.rightStick.ReadValue() * (110 * dt);
            Actor.Rotate(0, look.x, 0); pitch = Mathf.Clamp(pitch - look.y, -70, 70);
            Vector2 input = Gamepad.current != null ? Gamepad.current.leftStick.ReadValue() : Vector2.zero;
            var k = Keyboard.current;
            if (k != null) { input.x += (k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0); input.y += (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0); }
            input = Vector2.ClampMagnitude(input, 1);
            float speed = k != null && k.leftShiftKey.isPressed ? 4.8f : 3.1f;
            body.Move((Actor.right * input.x + Actor.forward * input.y) * (speed * dt));
        }

        void CheckExposure(float dt)
        {
            bool near = false;
            for (int i = 0; i < HazardSites.Count; i++)
                if (HazardLevels[i] == 2 && Vector3.Distance(Flat(Actor.position), MineLayout.World(HazardSites[i])) < 4.5f) near = true;
            if (near) { Exposure += dt; if (!lastContact) { HazardContacts++; LogEvent("hazard_contact", "warning_perimeter"); } }
            lastContact = near;
        }
        public float NearestHazardDistance()
        {
            float d = float.PositiveInfinity;
            for (int i = 0; i < HazardSites.Count; i++) if (HazardLevels[i] > 0) d = Mathf.Min(d, Vector3.Distance(Flat(Actor.position), MineLayout.World(HazardSites[i])));
            return d;
        }

        void UpdateCamera(bool snap)
        {
            if (ViewCamera == null) return;
            Vector3 eye = Actor.position + Vector3.up * 1.65f;
            if (Mode == MiningMode.FirstPerson && State != SessionState.Menu)
            { ViewCamera.transform.SetPositionAndRotation(eye, Actor.rotation * Quaternion.Euler(pitch, 0, 0)); return; }
            Vector3 desired = Actor.position - Actor.forward * 3.1f + Vector3.up * 2.65f;
            Vector3 direction = desired - eye;
            if (Physics.SphereCast(eye, .16f, direction.normalized, out RaycastHit hit, direction.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                desired = eye + direction.normalized * Mathf.Max(.25f, hit.distance - .15f);
            ViewCamera.transform.position = snap ? desired : Vector3.Lerp(ViewCamera.transform.position, desired, Time.deltaTime * 10);
            ViewCamera.transform.LookAt(eye + Actor.forward * 3);
        }

        void UpdateArrows()
        {
            foreach (var arrow in arrows) arrow.SetActive(false);
            if (State != SessionState.Running || (Mode == MiningMode.FirstPerson && !GlassesEnabled) || Route.Count == 0) return;
            Vector3 from = Flat(Actor.position); int count = 0;
            foreach (var cell in Route)
            {
                if (Blocked.Contains(cell)) break; // Never label a known landslide as safe, even in baseline.
                Vector3 to = MineLayout.World(cell), delta = to - from; float length = delta.magnitude;
                // A manual baseline detour must not produce an AR line through a rock wall.
                bool obstructed = length > .1f && Physics.Raycast(from + Vector3.up, delta.normalized, length, ~0, QueryTriggerInteraction.Ignore);
                if (length > .1f && !obstructed)
                    for (float d = 1.3f; d < length && count < arrows.Count; d += 2)
                    {
                        var arrow = arrows[count++]; arrow.SetActive(true); arrow.transform.position = from + delta.normalized * d + Vector3.up * .09f;
                        arrow.transform.rotation = Quaternion.LookRotation(delta);
                    }
                from = to; if (count >= arrows.Count) break;
            }
        }

        public string DirectionHint()
        {
            if (Route.Count == 0) return "TIDAK ADA RUTE AMAN";
            if (Blocked.Contains(Route[0])) return "RUTE TERHALANG - BERHENTI";
            Vector3 d = MineLayout.World(Route[0]) - Flat(Actor.position);
            float angle = Vector3.SignedAngle(Actor.forward, d, Vector3.up);
            return Mathf.Abs(angle) > 140 ? "PUTAR BALIK" : Mathf.Abs(angle) < 25 ? "LURUS" : angle > 0 ? "BELOK KANAN" : "BELOK KIRI";
        }
        static Vector3 Flat(Vector3 p) => new Vector3(p.x, 0, p.z);
        void LogEvent(string type, string detail)
        {
            eventRows.Add(string.Join(",", Elapsed.ToString("F3", CultureInfo.InvariantCulture), type, detail, Reroutes.ToString(), ResponseMs.ToString("F4", CultureInfo.InvariantCulture)));
        }
        [Serializable] class ExperimentConfig
        {
            public int seed;
            public string scenario, planner, unityVersion;
            public float warningRiskPenalty;
            public float cellSize = MineLayout.CellSize, workerSpeed = 2.8f, exposureRadius = 4.5f;
            public Vector2Int spawn = MineLayout.Spawn;
            public Vector2Int[] exits = MineLayout.Exits;
            public Vector2Int[] detectorSites;
            public ScheduledRockfall[] schedule;
        }
        public void Export()
        {
            try
            {
                string directory = Path.Combine(Application.persistentDataPath, "Evaluasi"); Directory.CreateDirectory(directory);
                string prefix = Path.Combine(directory, DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_" + Mode + "_" + (Adaptive ? "adaptive" : "static"));
                var layout = new List<Vector2Int>(Cells);
                layout.Sort((a, b) => a.x == b.x ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
                var layoutRows = new List<string> { "cell_x,cell_z" };
                foreach (var cell in layout) layoutRows.Add(cell.x + "," + cell.y);
                File.WriteAllText(prefix + "_layout.csv", string.Join("\n", layoutRows));
                File.WriteAllText(prefix + "_events.csv", "simulation_time_s,event,detail,reroutes,planning_ms\n" + string.Join("\n", eventRows));
                File.WriteAllText(prefix + "_summary.csv", "mode,navigation,outcome,elapsed_s,distance_m,exposure_s,hazard_contacts,reroutes,max_planning_ms,scenario,seed\n" +
                    string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3:F3},{4:F3},{5:F3},{6},{7},{8:F4},{9},{10}\n", Mode, Adaptive ? "adaptive" : "static", State, Elapsed, Travelled, Exposure, HazardContacts, Reroutes, MaxResponseMs, ActiveScenario, ActiveSeed));
                var scheduleRows = new List<string> { "detector_index,cell_x,cell_z,warning_s,collapse_s" };
                foreach (var item in Schedule)
                    scheduleRows.Add(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3:F3},{4:F3}", item.detectorIndex,
                        HazardSites[item.detectorIndex].x, HazardSites[item.detectorIndex].y, item.warningTime, item.collapseTime));
                File.WriteAllText(prefix + "_scenario.csv", string.Join("\n", scheduleRows));
                File.WriteAllText(prefix + "_config.json", JsonUtility.ToJson(new ExperimentConfig {
                    seed = ActiveSeed, scenario = ActiveScenario.ToString(), warningRiskPenalty = activeWarningPenalty,
                    detectorSites = HazardSites.ToArray(), schedule = Schedule.ToArray(),
                    unityVersion = Application.unityVersion, planner = "Dijkstra_distance_plus_warning_penalty_v1"
                }, true));
                ExportStatus = "CSV tersimpan: " + directory; Debug.Log(ExportStatus);
            }
            catch (Exception e) { ExportStatus = "Ekspor gagal: " + e.Message; Debug.LogWarning(ExportStatus); }
        }
        void OnDestroy() { if (Application.isPlaying) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; } }
    }
}
