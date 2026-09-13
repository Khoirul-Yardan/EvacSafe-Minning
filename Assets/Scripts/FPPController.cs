using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(CharacterController))]
public class FPPController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 3.5f;
    public float runSpeed = 6.0f;
    public float gravity = 18f;
    public float jumpHeight = 1.0f;

    [Header("Look Settings")]
    public float mouseSensitivity = 2.0f;
    public float minVerticalAngle = -75f;
    public float maxVerticalAngle = 75f;
    public Transform cameraHolder;
    public DangerZone[] dangerZones;
    public Image dangerOverlay;
    public float dangerFeedbackDistance = 8f;
    public float dangerDamageDelay = 0.35f;
    public float deathDuration = 1.8f;
    public Text missionText;
    public Transform[] checkpointPoints;
    public float checkpointRadius = 2.5f;
    public string lastCheckpointName { get; private set; }
    public float checkpointAlertUntil { get; private set; }

    private CharacterController controller;
    private float verticalRotation = 0f;
    private Vector3 moveVelocity;
    private bool isGrounded;
    private Vector3 respawnPosition;
    private Quaternion respawnRotation;
    private float dangerEnteredAt = -1f;
    private bool isDead;
    private Text dangerMessage;

    public bool isControlEnabled = true;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (dangerZones == null || dangerZones.Length == 0)
        {
            dangerZones = FindObjectsByType<DangerZone>(FindObjectsSortMode.None);
        }
        if (cameraHolder == null && transform.childCount > 0)
        {
            cameraHolder = transform.Find("FPP_Camera") ?? transform.GetChild(0);
        }
    }

    private void Start()
    {
        respawnPosition = transform.position;
        respawnRotation = transform.rotation;

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 10f, NavMesh.AllAreas))
        {
            controller.enabled = false;
            // CharacterController.transform is its capsule centre, not its feet.
            transform.position = hit.position + Vector3.up * (controller.height * 0.5f);
            controller.enabled = true;
            respawnPosition = transform.position;
        }
        else
        {
            Debug.LogWarning($"[FPPController] Spawn PlayerFPP di luar NavMesh: {transform.position}");
        }

        EnsureDangerMessage();
    }

    private void Update()
    {
        if (!isControlEnabled) return;

        if (isDead) return;

        HandleMouseLook();
        HandleMovement();
        UpdateDangerFeedback();
        CheckDangerCollision();
        CheckCheckpointProgress();
        PreventLeavingMap();
    }

    private void HandleMouseLook()
    {
        if (Mouse.current == null) return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        float mouseX = mouseDelta.x * mouseSensitivity * 0.02f;
        float mouseY = mouseDelta.y * mouseSensitivity * 0.02f;

        // Rotate body horizontally
        transform.Rotate(Vector3.up * mouseX);

        // Rotate camera vertically with clamp
        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, minVerticalAngle, maxVerticalAngle);

        if (cameraHolder != null)
        {
            cameraHolder.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
        }
    }

    private void HandleMovement()
    {
        isGrounded = controller.isGrounded;
        if (isGrounded && moveVelocity.y < 0)
        {
            moveVelocity.y = -2f;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        float moveX = 0f;
        if (keyboard.aKey.isPressed) moveX -= 1f;
        if (keyboard.dKey.isPressed) moveX += 1f;

        float moveZ = 0f;
        if (keyboard.sKey.isPressed) moveZ -= 1f;
        if (keyboard.wKey.isPressed) moveZ += 1f;

        bool isRunning = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        float speed = isRunning ? runSpeed : walkSpeed;

        Vector3 move = transform.right * moveX + transform.forward * moveZ;
        controller.Move(move * speed * Time.deltaTime);

        // Jump (optional)
        if (keyboard.spaceKey.wasPressedThisFrame && isGrounded)
        {
            moveVelocity.y = Mathf.Sqrt(jumpHeight * 2f * gravity);
        }

        // Apply gravity
        moveVelocity.y -= gravity * Time.deltaTime;
        controller.Move(moveVelocity * Time.deltaTime);
    }

    public void SetControlEnabled(bool enabled)
    {
        isControlEnabled = enabled;
        if (enabled)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void UpdateDangerFeedback()
    {
        if (dangerOverlay == null || dangerZones == null) return;

        float strongestDanger = 0f;
        foreach (DangerZone zone in dangerZones)
        {
            if (zone == null || !zone.isActive) continue;
            float distance = Vector3.Distance(transform.position, zone.ZoneBounds.ClosestPoint(transform.position));
            strongestDanger = Mathf.Max(strongestDanger, 1f - Mathf.Clamp01(distance / dangerFeedbackDistance));
        }

        CanvasGroup overlayGroup = dangerOverlay.GetComponent<CanvasGroup>();
        if (overlayGroup != null) overlayGroup.alpha = strongestDanger * 0.28f;
        else { Color color = dangerOverlay.color; color.a = strongestDanger * 0.28f; dangerOverlay.color = color; }
    }

    private void CheckDangerCollision()
    {
        DangerZone enteredZone = null;
        foreach (DangerZone zone in dangerZones)
        {
            if (zone != null && zone.isActive && zone.ZoneBounds.Intersects(controller.bounds))
            {
                enteredZone = zone;
                break;
            }
        }

        if (enteredZone == null)
        {
            dangerEnteredAt = -1f;
            return;
        }

        if (dangerEnteredAt < 0f) dangerEnteredAt = Time.time;
        if (Time.time - dangerEnteredAt >= dangerDamageDelay)
        {
            Debug.LogWarning($"[FPPController] Pemain masuk {zoneName(enteredZone)}. Death sequence dimulai.");
            StartCoroutine(DieAndRespawn(enteredZone));
        }
    }

    private string zoneName(DangerZone zone)
    {
        return zone != null ? zone.zoneName : "DangerZone";
    }

    public void SetCheckpointPoints(Transform[] points)
    {
        checkpointPoints = points;
    }

    private void CheckCheckpointProgress()
    {
        if (checkpointPoints == null) return;
        foreach (Transform checkpoint in checkpointPoints)
        {
            if (checkpoint == null || checkpoint.name == lastCheckpointName) continue;
            if (Vector3.Distance(transform.position, checkpoint.position) <= checkpointRadius)
            {
                respawnPosition = checkpoint.position;
                lastCheckpointName = checkpoint.name;
                checkpointAlertUntil = Time.time + 3f;
            }
        }
    }

    private void PreventLeavingMap()
    {
        if (transform.position.y < -10f || !NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
        {
            RespawnAt(respawnPosition);
        }
    }

    private void RespawnAt(Vector3 position)
    {
        if (!NavMesh.SamplePosition(position, out NavMeshHit hit, 10f, NavMesh.AllAreas)) return;
        controller.enabled = false;
        transform.position = hit.position + Vector3.up * (controller.height * 0.5f);
        controller.enabled = true;
        moveVelocity = Vector3.zero;
    }

    private IEnumerator DieAndRespawn(DangerZone zone)
    {
        isDead = true;
        SetDangerMessage($"BAHAYA: {zone.zoneName}\nAnda terluka, kembali ke titik aman...");
        if (dangerOverlay != null)
        {
            CanvasGroup overlayGroup = dangerOverlay.GetComponent<CanvasGroup>();
            if (overlayGroup != null) overlayGroup.alpha = 0.55f;
            else { Color color = Color.red; color.a = 0.55f; dangerOverlay.color = color; }
        }

        yield return new WaitForSeconds(deathDuration);

        Vector3 safePosition = FindSafeRespawnPosition();

        if (NavMesh.SamplePosition(safePosition, out NavMeshHit hit, 20f, NavMesh.AllAreas)) safePosition = hit.position;
        RespawnAt(safePosition);
        transform.rotation = respawnRotation;
        moveVelocity = Vector3.zero;
        dangerEnteredAt = -1f;
        isDead = false;
        SetDangerMessage(string.Empty);
        if (dangerOverlay != null)
        {
            CanvasGroup overlayGroup = dangerOverlay.GetComponent<CanvasGroup>();
            if (overlayGroup != null) overlayGroup.alpha = 0f;
            else { Color color = dangerOverlay.color; color.a = 0f; dangerOverlay.color = color; }
        }
    }

    private Vector3 FindSafeRespawnPosition()
    {
        Vector3[] candidates =
        {
            respawnPosition,
            respawnPosition + Vector3.right * 4f,
            respawnPosition - Vector3.right * 4f,
            respawnPosition + Vector3.forward * 4f,
            respawnPosition - Vector3.forward * 4f,
            respawnPosition + new Vector3(6f, 0f, 6f),
            respawnPosition + new Vector3(-6f, 0f, -6f)
        };

        foreach (Vector3 candidate in candidates)
        {
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas)) continue;
            bool blocked = false;
            foreach (DangerZone zone in dangerZones)
            {
                if (zone != null && zone.isActive && zone.ZoneBounds.Contains(hit.position))
                {
                    blocked = true;
                    break;
                }
            }
            if (!blocked) return hit.position;
        }

        return respawnPosition;
    }

    private void EnsureDangerMessage()
    {
        if (dangerOverlay == null || dangerMessage != null) return;
        dangerMessage = dangerOverlay.GetComponentInChildren<Text>();
        if (dangerMessage == null)
        {
            GameObject messageObject = new GameObject("DangerMessage");
            messageObject.transform.SetParent(dangerOverlay.transform, false);
            RectTransform rect = messageObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.15f, 0.35f);
            rect.anchorMax = new Vector2(0.85f, 0.65f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            dangerMessage = messageObject.AddComponent<Text>();
            dangerMessage.alignment = TextAnchor.MiddleCenter;
            dangerMessage.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            dangerMessage.fontSize = 24;
            dangerMessage.fontStyle = FontStyle.Bold;
            dangerMessage.color = Color.white;
            dangerMessage.raycastTarget = false;
        }
        dangerMessage.text = string.Empty;
    }

    private void SetDangerMessage(string message)
    {
        if (dangerMessage != null) dangerMessage.text = message;
    }
}
