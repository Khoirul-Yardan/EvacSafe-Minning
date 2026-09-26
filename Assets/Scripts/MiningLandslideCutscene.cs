using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace SafeMining
{
    // Read-only view of applied world state. Never drives the edge, planner, actor, or main camera.
    public sealed class MiningLandslideCutscene : MonoBehaviour
    {
        public const float ShotSeconds = 5f;
        public int ActiveDeviceIndex { get; private set; } = -1;
        public int PendingCount => pending.Count;
        public bool IsVisible => panel != null && panel.activeSelf;
        public Camera FeedCamera { get; private set; }
        public RenderTexture FeedTexture { get; private set; }
        readonly Queue<int> pending = new Queue<int>();
        MiningSimulation simulation;
        GameObject panel;
        RawImage image;
        Text title, status;
        bool[] seen = new bool[0];
        int revision = -1;
        float shotStarted;

        public void Initialize(MiningSimulation owner, GameObject view, RawImage feed, Text heading, Text caption)
        {
            simulation = owner; panel = view; image = feed; title = heading; status = caption;
            panel.SetActive(false);
        }

        void LateUpdate()
        {
            if (simulation == null || panel == null) return;
            if (revision != simulation.SessionRevision)
            {
                revision = simulation.SessionRevision;
                seen = new bool[simulation.HazardLevels.Length];
                ClearShots();
            }
            if (simulation.State == SessionState.Menu || simulation.State == SessionState.Success || simulation.State == SessionState.Blocked)
            {
                ClearShots(); SetVisible(false); return;
            }
            if (simulation.State == SessionState.Paused) { SetVisible(false); return; }

            // Applied level 2 only: decisions awaiting MQTT receipt cannot trigger the view.
            // One shot per device per session also ignores repeated QoS snapshots.
            for (int i = 0; i < seen.Length; i++)
                if (!seen[i] && simulation.HazardLevels[i] == 2)
                { seen[i] = true; pending.Enqueue(i); }
            if (ActiveDeviceIndex >= 0 && simulation.Elapsed - shotStarted >= ShotSeconds)
                ActiveDeviceIndex = -1;
            if (ActiveDeviceIndex < 0 && pending.Count > 0) StartShot(pending.Dequeue());
            SetVisible(ActiveDeviceIndex >= 0);
            if (ActiveDeviceIndex >= 0)
                status.text = "KAMERA LOKASI / LIVE" + (pending.Count > 0 ? "  |  " + pending.Count + " lokasi menunggu" : "  |  LORONG TERTUTUP");
        }

        void StartShot(int index)
        {
            EnsureCamera();
            ActiveDeviceIndex = index; shotStarted = simulation.Elapsed;
            var site = simulation.HazardSites[index];
            Vector3 center = MineLayout.World(site);
            Vector2Int approach = Vector2Int.up;
            foreach (var direction in MineLayout.Directions)
                if (simulation.Cells.Contains(site + direction)) { approach = direction; break; }
            Vector3 offset = new Vector3(approach.x, 0, approach.y) * (MineLayout.CellSize * .9f);
            FeedCamera.transform.position = center + offset + Vector3.up * 2.2f;
            Vector3 detector = simulation.Detectors[index].transform.position;
            FeedCamera.transform.LookAt(Vector3.Lerp(center + Vector3.up * 1.6f, detector, .35f));
            title.text = "LONGSOR / D" + (index + 1).ToString("00") + "  |  GRID " + site.x + ":" + site.y;
        }

        void EnsureCamera()
        {
            if (FeedCamera != null) return;
            FeedTexture = new RenderTexture(640, 360, 24) { name = "Landslide inset 640x360" };
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null) FeedTexture.Create();
            image.texture = FeedTexture;
            var cameraObject = new GameObject("Landslide inset camera");
            cameraObject.transform.SetParent(transform, false);
            FeedCamera = cameraObject.AddComponent<Camera>();
            FeedCamera.enabled = false; FeedCamera.targetTexture = FeedTexture;
            FeedCamera.fieldOfView = 64; FeedCamera.aspect = 16f / 9f;
            FeedCamera.nearClipPlane = .1f; FeedCamera.farClipPlane = 65;
            FeedCamera.clearFlags = CameraClearFlags.SolidColor;
            FeedCamera.backgroundColor = new Color(.035f, .041f, .046f);
            FeedCamera.cullingMask = simulation.ViewCamera.cullingMask & ~(1 << LayerMask.NameToLayer("UI"));
            FeedCamera.depth = simulation.ViewCamera.depth - 1;
            FeedCamera.allowHDR = false; FeedCamera.allowMSAA = false;
            var data = FeedCamera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false; data.renderShadows = true;
            data.requiresColorOption = CameraOverrideOption.Off;
            data.requiresDepthOption = CameraOverrideOption.Off;
        }

        void ClearShots() { pending.Clear(); ActiveDeviceIndex = -1; }
        void SetVisible(bool visible)
        {
            panel.SetActive(visible);
            if (FeedCamera != null)
                FeedCamera.enabled = visible && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;
        }
        void OnDisable() { if (panel != null) SetVisible(false); }
        void OnDestroy()
        {
            if (image != null) image.texture = null;
            if (FeedCamera != null) { FeedCamera.targetTexture = null; Destroy(FeedCamera.gameObject); }
            if (FeedTexture != null) { FeedTexture.Release(); Destroy(FeedTexture); }
        }
    }
}
