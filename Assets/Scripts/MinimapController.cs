using UnityEngine;
using UnityEngine.UI;

public class MinimapController : MonoBehaviour
{
    [Header("Target Tracking")]
    public Transform activeTarget; // WorkerAgent or PlayerFPP
    public Transform[] exitPoints;
    public DangerZone[] dangerZones;

    [Header("UI Marker References")]
    public RectTransform playerMarker;
    public RectTransform[] exitMarkers;
    public RectTransform[] dangerMarkers;
    public Transform[] checkpointPoints;
    public RectTransform[] checkpointMarkers;

    [Header("Minimap Bounds Setup")]
    public Vector2 worldCenter = new Vector2(0f, -45f);
    public Vector2 worldSize = new Vector2(150f, 150f);
    public float minimapUIWidth = 180f;
    public float minimapUIHeight = 180f;
    public Camera minimapCamera;
    public bool followActiveTarget = true;
    public float followHeight = 80f;
    public float followOrthographicSize = 32f;

    public void SetWorldBounds(Bounds bounds)
    {
        worldCenter = new Vector2(bounds.center.x, bounds.center.z);
        worldSize = new Vector2(Mathf.Max(bounds.size.x, 1f), Mathf.Max(bounds.size.z, 1f));
    }

    private void Update()
    {
        FollowActiveTarget();
        UpdatePlayerMarker();
        UpdateExitMarkers();
        UpdateDangerMarkers();
        UpdateCheckpointMarkers();
    }

    private void FollowActiveTarget()
    {
        if (!followActiveTarget || activeTarget == null) return;
        if (minimapCamera == null) minimapCamera = GameObject.Find("MinimapCamera")?.GetComponent<Camera>();
        if (minimapCamera == null) return;

        Vector3 position = minimapCamera.transform.position;
        position.x = activeTarget.position.x;
        position.y = followHeight;
        position.z = activeTarget.position.z;
        minimapCamera.transform.position = position;
        minimapCamera.orthographic = true;
        minimapCamera.orthographicSize = followOrthographicSize;
    }

    public void SetActiveTarget(Transform target)
    {
        activeTarget = target;
    }

    public void SetCheckpointPoints(Transform[] points)
    {
        checkpointPoints = points;
        checkpointMarkers = new RectTransform[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            GameObject markerObject = new GameObject($"CheckpointMarker_{i + 1}");
            markerObject.transform.SetParent(transform, false);
            RectTransform marker = markerObject.AddComponent<RectTransform>();
            marker.sizeDelta = new Vector2(11f, 11f);
            Image image = markerObject.AddComponent<Image>();
            image.color = Color.yellow;
            checkpointMarkers[i] = marker;
        }
    }

    private void UpdatePlayerMarker()
    {
        if (activeTarget == null || playerMarker == null) return;

        Vector2 uiPos = WorldToMinimapPosition(activeTarget.position);
        if (followActiveTarget && minimapCamera != null)
        {
            playerMarker.anchoredPosition = Vector2.zero;
        }
        else
        {
            playerMarker.anchoredPosition = uiPos;
        }

        // Rotate marker according to target rotation (top-down view)
        float yaw = activeTarget.eulerAngles.y;
        playerMarker.localRotation = Quaternion.Euler(0f, 0f, -yaw);
    }

    private void UpdateExitMarkers()
    {
        if (exitPoints == null || exitMarkers == null) return;

        for (int i = 0; i < exitPoints.Length && i < exitMarkers.Length; i++)
        {
            if (exitPoints[i] != null && exitMarkers[i] != null)
            {
                exitMarkers[i].gameObject.SetActive(exitPoints[i].gameObject.activeInHierarchy);
                if (!exitPoints[i].gameObject.activeInHierarchy) continue;
                exitMarkers[i].anchoredPosition = WorldToMinimapPosition(exitPoints[i].position);
            }
        }
    }

    private void UpdateDangerMarkers()
    {
        if (dangerZones == null || dangerMarkers == null) return;

        for (int i = 0; i < dangerZones.Length && i < dangerMarkers.Length; i++)
        {
            if (dangerZones[i] != null && dangerMarkers[i] != null)
            {
                dangerMarkers[i].anchoredPosition = WorldToMinimapPosition(dangerZones[i].transform.position);

                // Update marker color based on danger zone status
                Image img = dangerMarkers[i].GetComponent<Image>();
                if (img != null)
                {
                    img.color = dangerZones[i].isActive ? new Color(1f, 0.2f, 0.2f, 0.9f) : new Color(0.4f, 0.4f, 0.4f, 0.3f);
                }
            }
        }
    }

    private void UpdateCheckpointMarkers()
    {
        if (checkpointPoints == null || checkpointMarkers == null) return;
        for (int i = 0; i < checkpointPoints.Length && i < checkpointMarkers.Length; i++)
        {
            if (checkpointPoints[i] == null || checkpointMarkers[i] == null) continue;
            checkpointMarkers[i].anchoredPosition = WorldToMinimapPosition(checkpointPoints[i].position);
        }
    }

    public Vector2 WorldToMinimapPosition(Vector3 worldPos)
    {
        if (followActiveTarget && minimapCamera != null)
        {
            float halfHeight = minimapCamera.orthographicSize;
            float halfWidth = halfHeight * minimapCamera.aspect;
            float localX = (worldPos.x - minimapCamera.transform.position.x) / halfWidth;
            float localY = (worldPos.z - minimapCamera.transform.position.z) / halfHeight;
            return new Vector2(localX * minimapUIWidth * 0.5f, localY * minimapUIHeight * 0.5f);
        }

        // Normalize X and Z into [0, 1] relative to world center and size
        float normX = (worldPos.x - (worldCenter.x - worldSize.x * 0.5f)) / worldSize.x;
        float normZ = (worldPos.z - (worldCenter.y - worldSize.y * 0.5f)) / worldSize.y;

        // Map to UI local coordinates [-width/2, +width/2]
        float uiX = (normX - 0.5f) * minimapUIWidth;
        float uiY = (normZ - 0.5f) * minimapUIHeight;

        return new Vector2(uiX, uiY);
    }
}
