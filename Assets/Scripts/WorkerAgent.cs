using UnityEngine;
using UnityEngine.AI;
using System;

[RequireComponent(typeof(NavMeshAgent))]
public class WorkerAgent : MonoBehaviour
{
    [Header("Navigation")]
    [SerializeField] private Transform currentExitPoint;
    [SerializeField] private float arrivalThreshold = 2.0f;
    [SerializeField] private float agentSpeed = 4.0f;
    [SerializeField] private bool showPathDebug = false;

    [Header("Status")]
    public bool isEvacuating = false;
    public bool hasArrived = false;
    public float remainingDistance = 0f;

    public Transform CurrentExitPoint => currentExitPoint;
    public NavMeshAgent Agent { get; private set; }

    public event Action<Transform> OnDestinationChanged;
    public event Action OnArrivedAtExit;

    private LineRenderer lineRenderer;

    private void Awake()
    {
        Agent = GetComponent<NavMeshAgent>();
        if (Agent != null)
        {
            Agent.speed = agentSpeed;
            Agent.acceleration = 12f;
            Agent.angularSpeed = 360f;
            Agent.stoppingDistance = arrivalThreshold;
            Agent.autoRepath = true;
        }

        SetupPathVisualizer();
    }

    private void SetupPathVisualizer()
    {
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        }
        lineRenderer.startWidth = 0.25f;
        lineRenderer.endWidth = 0.25f;
        lineRenderer.useWorldSpace = true;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = new Color(0.2f, 0.8f, 1f, 0.7f);
        lineRenderer.endColor = new Color(0.1f, 1f, 0.5f, 0.7f);
        lineRenderer.enabled = false;
    }

    private void Update()
    {
        if (Agent == null || !Agent.enabled || !Agent.isOnNavMesh) return;

        if (isEvacuating && !hasArrived)
        {
            remainingDistance = Agent.remainingDistance;

            if (!Agent.pathPending && Agent.remainingDistance <= arrivalThreshold)
            {
                if (Agent.hasPath || Agent.velocity.sqrMagnitude < 0.1f)
                {
                    hasArrived = true;
                    isEvacuating = false;
                    Agent.isStopped = true;
                    Debug.Log($"<color=green>[WorkerAgent]</color> BERHASIL EVAKUASI! Telah sampai di exit: {(currentExitPoint != null ? currentExitPoint.name : "Target")}");
                    OnArrivedAtExit?.Invoke();
                }
            }

            UpdatePathVisualizer();
        }
    }

    private void UpdatePathVisualizer()
    {
        if (!showPathDebug || lineRenderer == null || Agent == null || !Agent.hasPath)
        {
            if (lineRenderer != null) lineRenderer.enabled = false;
            return;
        }

        Vector3[] corners = Agent.path.corners;
        if (corners.Length > 1)
        {
            lineRenderer.enabled = true;
            lineRenderer.positionCount = corners.Length;
            for (int i = 0; i < corners.Length; i++)
            {
                lineRenderer.SetPosition(i, corners[i] + Vector3.up * 0.2f);
            }
        }
        else
        {
            lineRenderer.enabled = false;
        }
    }

    public void SetDestination(Transform exitTarget)
    {
        if (exitTarget == null) return;
        currentExitPoint = exitTarget;
        SetDestination(exitTarget.position);
        OnDestinationChanged?.Invoke(exitTarget);
    }

    public void SetDestination(Vector3 targetPosition)
    {
        if (Agent == null || !Agent.enabled) return;

        if (!Agent.isOnNavMesh)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 5.0f, NavMesh.AllAreas))
            {
                Agent.Warp(hit.position);
            }
        }

        if (Agent.isOnNavMesh)
        {
            Agent.isStopped = false;
            Agent.SetDestination(targetPosition);
            isEvacuating = true;
            hasArrived = false;
        }
    }

    public void ResetAgent(Vector3 startPos)
    {
        if (Agent != null && Agent.isOnNavMesh)
        {
            Agent.isStopped = true;
            Agent.ResetPath();
        }
        transform.position = startPos;
        hasArrived = false;
        isEvacuating = false;
        if (lineRenderer != null) lineRenderer.enabled = false;
    }
}
