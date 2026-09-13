using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target Tracking")]
    public Transform target;
    public Vector3 offset = new Vector3(0f, 25f, -15f);
    public float smoothSpeed = 5f;
    public bool lookAtTarget = true;

    private void Start()
    {
        if (target == null)
        {
            WorkerAgent agent = FindFirstObjectByType<WorkerAgent>();
            if (agent != null) target = agent.transform;
        }

        if (target != null)
        {
            transform.position = target.position + offset;
            if (lookAtTarget)
            {
                transform.LookAt(target.position + Vector3.up * 1.5f);
            }
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            WorkerAgent agent = FindFirstObjectByType<WorkerAgent>();
            if (agent != null) target = agent.transform;
            return;
        }

        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

        if (lookAtTarget)
        {
            Quaternion targetRotation = Quaternion.LookRotation((target.position + Vector3.up * 1.0f) - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, smoothSpeed * Time.deltaTime);
        }
    }
}
