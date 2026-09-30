using System.Collections.Generic;
using UnityEngine;

namespace SafeMining
{
    // Presentation only: these cues never set detector levels or close a corridor.
    public sealed class MiningStoryCues : MonoBehaviour
    {
        MiningSimulation simulation;
        readonly List<Transform> pipes = new List<Transform>();
        readonly List<Vector3> pipeRest = new List<Vector3>();
        GameObject[] cracks;
        Transform[] dust;
        float[] cueUntil;
        AudioSource rumble;
        AudioClip rumbleClip;
        Material crackMaterial, dustMaterial;
        GameObject nearbyCrack;
        Transform nearbyDust;

        public void Initialize(MiningSimulation owner)
        {
            simulation = owner;
            foreach (Transform item in owner.GetComponentsInChildren<Transform>())
                if (item.name == "Ventilation pipe" || item.name == "Pipe flange")
                { pipes.Add(item); pipeRest.Add(item.localPosition); }
            int count = owner.HazardSites.Count;
            cracks = new GameObject[count]; dust = new Transform[count]; cueUntil = new float[count];
            crackMaterial = MineLayout.Material("Story fissures", new Color(.025f, .018f, .012f));
            dustMaterial = MineLayout.Material("Story falling grit", new Color(.48f, .40f, .29f));
            for (int i = 0; i < count; i++)
            {
                var cell = owner.HazardSites[i];
                Vector2Int wall = Vector2Int.right;
                foreach (var direction in MineLayout.Directions)
                    if (!owner.Cells.Contains(cell + direction)) { wall = direction; break; }
                var outward = new Vector3(wall.x, 0, wall.y);
                var root = new GameObject("Story cracks D" + (i + 1).ToString("00"));
                root.transform.SetParent(transform, false);
                root.transform.position = MineLayout.World(cell) + outward * 2.65f + Vector3.up * 2.4f;
                root.transform.rotation = Quaternion.LookRotation(-outward);
                for (int segment = 0; segment < 7; segment++)
                {
                    var fissure = MineLayout.Box(root.transform, "Fissure", new Vector3((segment % 2) * .14f - .07f, segment * .18f - .55f, 0),
                        new Vector3(.045f, .3f, .035f), crackMaterial, false);
                    fissure.transform.localRotation = Quaternion.Euler(0, 0, segment % 2 == 0 ? 32 : -38);
                }
                cracks[i] = root; root.SetActive(false);
                var grit = new GameObject("Falling dust D" + (i + 1).ToString("00")).transform;
                grit.SetParent(transform, false); grit.position = MineLayout.World(cell);
                for (int n = 0; n < 10; n++)
                    MineLayout.Box(grit, "Grit", Vector3.zero, Vector3.one * (.025f + n % 3 * .015f), dustMaterial, false);
                dust[i] = grit; grit.gameObject.SetActive(false);
            }
            rumble = gameObject.AddComponent<AudioSource>(); rumble.playOnAwake = false; rumble.loop = true; rumble.volume = 0;
            const int rate = 22050;
            var samples = new float[rate];
            for (int n = 0; n < samples.Length; n++)
            {
                float t = (float)n / rate;
                float rattle = Mathf.Pow(Mathf.Max(0, Mathf.Sin(2 * Mathf.PI * 13 * t)), 12);
                samples[n] = .35f * Mathf.Sin(2 * Mathf.PI * 57 * t) + .15f * Mathf.Sin(2 * Mathf.PI * 83 * t)
                    + rattle * .22f * Mathf.Sin(2 * Mathf.PI * 730 * t);
            }
            rumbleClip = AudioClip.Create("Tunnel rumble and pipe rattle", rate, 1, rate, false);
            rumbleClip.SetData(samples, 0); rumble.clip = rumbleClip;
        }

        public void Observe(int index)
        {
            if (index < 0 || index >= cracks.Length) return;
            cueUntil[index] = simulation.Elapsed + .6f;
            cracks[index].SetActive(true);
        }

        public void ShowNearbySigns()
        {
            // A visible environmental clue near the worker, not a new detector or a closure.
            if (cracks.Length == 0) return;
            if (nearbyCrack == null)
            {
                nearbyCrack = Instantiate(cracks[0], transform); nearbyCrack.name = "Nearby environmental fissure";
                nearbyDust = Instantiate(dust[0], transform); nearbyDust.name = "Nearby falling grit";
            }
            var cell = MineLayout.Cell(simulation.Actor.position);
            var wall = Vector2Int.right;
            foreach (var direction in MineLayout.Directions)
                if (!simulation.Cells.Contains(cell + direction)) { wall = direction; break; }
            var outward = new Vector3(wall.x, 0, wall.y);
            nearbyCrack.transform.position = MineLayout.World(cell) + outward * 2.65f + Vector3.up * 2.4f;
            nearbyCrack.transform.rotation = Quaternion.LookRotation(-outward);
            nearbyCrack.SetActive(true);
            nearbyDust.position = simulation.Actor.position + simulation.Actor.forward * 1.5f;
        }

        public void ResetCues()
        {
            if (cracks == null) return;
            for (int i = 0; i < cracks.Length; i++)
            { cracks[i].SetActive(false); dust[i].gameObject.SetActive(false); cueUntil[i] = -1; }
            for (int i = 0; i < pipes.Count; i++) pipes[i].localPosition = pipeRest[i];
            rumble.Stop();
            if (nearbyCrack != null) nearbyCrack.SetActive(false);
            if (nearbyDust != null) nearbyDust.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (simulation == null) return;
            bool running = simulation.State == SessionState.Running && simulation.Mode == MiningMode.Story;
            float tremor = running ? simulation.StoryTremor : 0;
            if (tremor > 0) { if (!rumble.isPlaying) rumble.Play(); rumble.volume = tremor * .24f; }
            else if (rumble.isPlaying) rumble.Stop();
            if (nearbyDust != null)
            {
                nearbyDust.gameObject.SetActive(tremor > 0);
                if (tremor > 0) AnimateDust(nearbyDust);
            }
            for (int i = 0; i < pipes.Count; i++)
                pipes[i].localPosition = pipeRest[i] + Vector3.up * (Mathf.Sin(simulation.Elapsed * 43) * .035f * tremor);
            for (int i = 0; i < dust.Length; i++)
            {
                bool active = running && simulation.Elapsed < cueUntil[i];
                dust[i].gameObject.SetActive(active);
                if (!active) continue;
                AnimateDust(dust[i]);
            }
        }

        void AnimateDust(Transform root)
        {
            for (int n = 0; n < root.childCount; n++)
                root.GetChild(n).localPosition = new Vector3(Mathf.Sin(n * 7) * 2.1f,
                    3.9f - Mathf.Repeat(simulation.Elapsed * 1.8f + n * .37f, 3.6f), Mathf.Cos(n * 11) * 1.6f);
        }

        void OnDisable() { if (rumble != null) rumble.Stop(); }
        void OnDestroy()
        {
            if (rumbleClip != null) Destroy(rumbleClip);
            if (crackMaterial != null) Destroy(crackMaterial);
            if (dustMaterial != null) Destroy(dustMaterial);
        }
    }
}
