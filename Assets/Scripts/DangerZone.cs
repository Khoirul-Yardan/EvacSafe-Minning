using UnityEngine;
using UnityEngine.Events;
using UnityEngine.AI;

[RequireComponent(typeof(BoxCollider))]
public class DangerZone : MonoBehaviour
{
    [Header("Danger Zone Settings")]
    public string zoneName = "Zona Bahaya";
    [SerializeField] private bool _isActive = false;

    [Header("Visual References")]
    [SerializeField] private Renderer visualRenderer;
    [SerializeField] private Light warningLight;
    [SerializeField] private Color activeColor = new Color(1f, 0.1f, 0.1f, 0.65f);
    [SerializeField] private Color inactiveColor = new Color(0.1f, 0.9f, 0.2f, 0.15f);

    [Header("Events")]
    public UnityEvent<DangerZone, bool> OnZoneStatusChanged;
    public event System.Action<DangerZone, bool> OnStatusChangedAction;

    private BoxCollider boxCollider;
    private NavMeshObstacle navObstacle;
    private MaterialPropertyBlock propBlock;

    public bool isActive
    {
        get => _isActive;
        set => SetActive(value);
    }

    public Bounds ZoneBounds
    {
        get
        {
            if (boxCollider == null) boxCollider = GetComponent<BoxCollider>();
            return boxCollider.bounds;
        }
    }

    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider>();
        boxCollider.isTrigger = true;

        navObstacle = GetComponent<NavMeshObstacle>();
        if (navObstacle == null)
        {
            navObstacle = gameObject.AddComponent<NavMeshObstacle>();
        }
        navObstacle.carving = true;
        navObstacle.carveOnlyStationary = false;
        navObstacle.size = boxCollider.size;
        navObstacle.center = boxCollider.center;
        navObstacle.enabled = _isActive;

        propBlock = new MaterialPropertyBlock();

        if (visualRenderer == null)
        {
            visualRenderer = GetComponentInChildren<MeshRenderer>();
        }

        if (warningLight == null)
        {
            warningLight = GetComponentInChildren<Light>();
        }

        UpdateVisuals();
    }

    private void Start()
    {
        UpdateVisuals();
    }

    private void OnValidate()
    {
        if (visualRenderer == null) visualRenderer = GetComponentInChildren<MeshRenderer>();
        UpdateVisuals();
    }

    public void SetActive(bool active)
    {
        if (_isActive == active) return;
        _isActive = active;

        if (navObstacle != null)
        {
            navObstacle.enabled = _isActive;
        }

        UpdateVisuals();

        if (_isActive)
        {
            Debug.Log($"<color=red>[DangerZone]</color> {zoneName} -> AKTIF (BAHAYA)");
        }
        OnZoneStatusChanged?.Invoke(this, _isActive);
        OnStatusChangedAction?.Invoke(this, _isActive);
    }

    public void ToggleActive()
    {
        SetActive(!_isActive);
    }

    public void UpdateVisuals()
    {
        if (visualRenderer != null)
        {
            if (propBlock == null) propBlock = new MaterialPropertyBlock();
            visualRenderer.GetPropertyBlock(propBlock);
            Color targetColor = _isActive ? activeColor : inactiveColor;
            propBlock.SetColor("_BaseColor", targetColor);
            propBlock.SetColor("_Color", targetColor);
            if (_isActive)
            {
                propBlock.SetColor("_EmissionColor", activeColor * 1.5f);
            }
            else
            {
                propBlock.SetColor("_EmissionColor", Color.black);
            }
            visualRenderer.SetPropertyBlock(propBlock);
            visualRenderer.enabled = _isActive;
        }

        if (warningLight != null)
        {
            warningLight.enabled = _isActive;
            warningLight.color = Color.red;
            warningLight.intensity = _isActive ? 2.5f : 0f;
        }
    }

    public bool IntersectsPath(NavMeshPath path)
    {
        if (path == null || path.corners.Length < 2) return false;
        Bounds bounds = ZoneBounds;
        // Expand slightly to catch path passing very close
        bounds.Expand(0.8f);

        for (int i = 0; i < path.corners.Length - 1; i++)
        {
            Vector3 start = path.corners[i];
            Vector3 end = path.corners[i + 1];

            if (bounds.Contains(start) || bounds.Contains(end))
                return true;

            if (LineIntersectsBounds(bounds, start, end))
                return true;
        }
        return false;
    }

    public void ConfigureVisuals(Renderer renderer, Color active, Color inactive)
    {
        visualRenderer = renderer;
        activeColor = active;
        inactiveColor = inactive;
        UpdateVisuals();
    }

    private bool LineIntersectsBounds(Bounds b, Vector3 p1, Vector3 p2)
    {
        Vector3 dir = p2 - p1;
        float len = dir.magnitude;
        if (len < 0.001f) return false;
        Ray ray = new Ray(p1, dir.normalized);
        if (b.IntersectRay(ray, out float enterDist))
        {
            return enterDist <= len;
        }
        return false;
    }

    private void OnDrawGizmos()
    {
        if (!_isActive) return;
        Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
        BoxCollider col = GetComponent<BoxCollider>();
        if (col != null)
        {
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(col.center, col.size);
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(col.center, col.size);
            Gizmos.matrix = oldMatrix;
        }
    }
}
