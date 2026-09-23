using UnityEngine;

namespace SafeMining
{
    // A virtual sensor station: scenario state drives the reading, light and local alarm.
    [ExecuteAlways]
    public class MiningLandslideDetector : MonoBehaviour
    {
        public Renderer indicator;
        public Light beacon;
        public TextMesh display;
        [field: SerializeField] public int StationIndex { get; private set; }
        [field: SerializeField] public int Level { get; private set; }
        [field: SerializeField] public Vector2Int Cell { get; private set; }
        MiningSimulation simulation;
        AudioSource siren;
        MaterialPropertyBlock tint;
        Material labelMaterial;
        static AudioClip alarmClip;
        float nextBeep;

        public void Configure(MiningSimulation owner, int index, Vector2Int cell)
        {
            simulation = owner; StationIndex = index; Cell = cell;
            name = "Detector D" + (index + 1).ToString("00");
            PrepareLabel();
            if (Application.isPlaying)
            {
                siren = gameObject.AddComponent<AudioSource>(); siren.playOnAwake = false;
                siren.spatialBlend = 1; siren.minDistance = 3; siren.maxDistance = 30;
                siren.rolloffMode = AudioRolloffMode.Linear; siren.volume = .28f;
                if (alarmClip == null)
                {
                    const int rate = 22050;
                    var samples = new float[rate / 3];
                    for (int i = 0; i < samples.Length; i++)
                        samples[i] = Mathf.Sin(i * 2 * Mathf.PI * 850 / rate) * Mathf.Sin(i * Mathf.PI / samples.Length) * .5f;
                    alarmClip = AudioClip.Create("Detector warning siren", samples.Length, 1, rate, false);
                    alarmClip.SetData(samples, 0);
                }
                siren.clip = alarmClip;
            }
            SetLevel(0);
        }

        public void SetLevel(int level)
        {
            Level = Mathf.Clamp(level, 0, 2); nextBeep = 0;
            if (siren != null) siren.Stop();
            ApplyVisual(0);
        }

        void Update()
        {
            if (display != null && labelMaterial != null) labelMaterial.mainTexture = display.font.material.mainTexture;
            if (simulation == null) return;
            ApplyVisual(simulation.Elapsed);
            if (simulation.State != SessionState.Running || Level == 0)
            { if (siren != null && siren.isPlaying) siren.Stop(); return; }
            if (simulation.Elapsed >= nextBeep && siren != null)
            { siren.Play(); nextBeep = simulation.Elapsed + (Level == 1 ? 1.25f : .65f); }
        }

        void OnEnable() { PrepareLabel(); }
        void PrepareLabel()
        {
            if (display == null) return;
            display.characterSize = .022f;
            var shader = Resources.Load<Shader>("MiningWorldSign");
            if (shader == null) return;
            if (labelMaterial == null) labelMaterial = new Material(shader) { name = "Detector depth-tested text" };
            display.font.RequestCharactersInTexture(display.text, display.fontSize);
            labelMaterial.mainTexture = display.font.material.mainTexture;
            display.GetComponent<Renderer>().sharedMaterial = labelMaterial;
        }

        void ApplyVisual(float time)
        {
            var color = Level == 0 ? new Color(.12f, 1, .4f) : Level == 1 ? new Color(1, .62f, .02f) : new Color(1, .08f, .035f);
            float pulse = Level == 0 ? 1 : .45f + .55f * Mathf.Abs(Mathf.Sin(time * (Level == 1 ? 5 : 9)));
            if (indicator != null)
            {
                if (tint == null) tint = new MaterialPropertyBlock();
                tint.SetColor("_BaseColor", color); tint.SetColor("_EmissionColor", color * (2 * pulse));
                indicator.SetPropertyBlock(tint);
            }
            if (beacon != null) { beacon.color = color; beacon.intensity = (Level == 0 ? .6f : 3) * pulse; }
            if (display != null)
            {
                string status = Level == 0 ? "NORMAL" : Level == 1 ? "AWAS LONGSOR" : "JALUR TERTUTUP";
                string text = "DETEKTOR D" + (StationIndex + 1).ToString("00") + "\n" + status;
                if (display.text != text)
                {
                    display.text = text; display.font.RequestCharactersInTexture(text, display.fontSize);
                    if (labelMaterial != null) labelMaterial.mainTexture = display.font.material.mainTexture;
                }
                display.color = color;
            }
        }

        // Also used once by the editor to author the reusable prefab asset.
        public static GameObject CreateModel()
        {
            var root = new GameObject("LandslideDetector");
            var detector = root.AddComponent<MiningLandslideDetector>();
            var yellow = MineLayout.Material("Detector safety yellow", new Color(.95f, .62f, .035f));
            var dark = MineLayout.Material("Detector graphite", new Color(.035f, .05f, .065f));
            var light = MineLayout.Material("Detector signal", new Color(.12f, 1, .4f), true);
            MineLayout.Box(root.transform, "Protective enclosure", Vector3.zero, new Vector3(1.15f, .9f, .3f), yellow, false);
            MineLayout.Box(root.transform, "Display panel", new Vector3(0, .04f, -.17f), new Vector3(1.02f, .6f, .045f), dark, false);
            MineLayout.Box(root.transform, "Vibration probe", new Vector3(-.38f, -.61f, 0), new Vector3(.18f, .32f, .22f), dark, false);
            MineLayout.Box(root.transform, "Antenna", new Vector3(.4f, .64f, .02f), new Vector3(.035f, .43f, .035f), dark, false);
            var lamp = MineLayout.Box(root.transform, "Warning beacon", new Vector3(0, .64f, 0), new Vector3(.32f, .34f, .26f), light, false);
            detector.indicator = lamp.GetComponent<Renderer>(); detector.beacon = lamp.AddComponent<Light>(); detector.beacon.range = 11;
            for (int i = 0; i < 4; i++)
                MineLayout.Box(root.transform, "Siren grille", new Vector3(.33f, -.32f + i * .04f, -.175f), new Vector3(.29f, .016f, .025f), dark, false);
            MineLayout.Sign(root.transform, new Vector3(0, .08f, -.201f), "DETEKTOR\nNORMAL", new Color(.12f, 1, .4f));
            detector.display = root.GetComponentInChildren<TextMesh>(); detector.display.characterSize = .022f;
            return root;
        }
    }
}
