using UnityEngine;
using UnityEngine.AI;

// Runtime evidence for layout integrity; deliberately observational and never moves actors.
public class SafeMiningRuntimeDiagnostics : MonoBehaviour
{
    private float nextLog;

    private void Update()
    {
        if (Time.time < nextLog) return;
        nextLog = Time.time + 1f;
        Transform entrance = GameObject.Find("EntrancePoint")?.transform;
        GameObject player = GameObject.Find("PlayerFPP");
        WorkerAgent worker = FindFirstObjectByType<WorkerAgent>();
        Camera camera = Camera.main;
        bool playerOnNav = player != null && NavMesh.SamplePosition(player.transform.position, out _, 1.5f, NavMesh.AllAreas);
        float velocity = worker != null && worker.Agent != null ? worker.Agent.velocity.magnitude : -1f;
        Debug.Log($"[SafeMiningDiagnostics] t={Time.time:F1}; Entrance={entrance?.position}; Player={player?.transform.position}; PlayerOnNav={playerOnNav}; Worker={worker?.transform.position}; WorkerVelocity={velocity:F2}; Camera={camera?.transform.position}");
        if (player != null && !playerOnNav) Debug.LogError($"[SafeMiningDiagnostics] PlayerFPP outside NavMesh at {player.transform.position}");
    }
}
