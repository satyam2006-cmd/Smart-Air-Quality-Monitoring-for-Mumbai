using UnityEngine;
using UnityEngine.InputSystem;

public class CameraMapNavigation : MonoBehaviour
{
    [Header("Camera")]
    public Camera mainCamera;

    [Header("Orbit Target")]
    public Vector3 orbitTarget = Vector3.zero;
    public float targetHeight = 0f;

    [Header("Orbit Settings")]
    public float orbitSensitivity = 0.15f;
    public float minVerticalAngle = 10f;
    public float maxVerticalAngle = 85f;

    [Header("Zoom Settings")]
    public float zoomSensitivity = 0.050f;
    public float minDistance = 5f;
    public float maxDistance = 300f;
    public float zoomSmoothTime = 0.1f;

    [Header("Pan Settings")]
    public float panSensitivity = 1f;

    private float yaw;
    private float pitch;

    private float currentDistance;
    private float targetDistance;
    private float zoomVelocity;

    private Vector2 previousMousePosition;

    private bool isOrbiting;
    private bool isPanning;

    private void Start()
    {
        if (mainCamera == null)
            mainCamera = GetComponent<Camera>();

        if (mainCamera == null)
        {
            Debug.LogError("Main Camera not assigned!");
            enabled = false;
            return;
        }

        orbitTarget.y = targetHeight;

        Vector3 direction =
            mainCamera.transform.position - orbitTarget;

        currentDistance = direction.magnitude;
        targetDistance = currentDistance;

        Vector3 normalizedDirection = direction.normalized;

        pitch = Mathf.Asin(normalizedDirection.y) * Mathf.Rad2Deg;

        yaw = Mathf.Atan2(
            -normalizedDirection.x,
            -normalizedDirection.z
        ) * Mathf.Rad2Deg;

        currentDistance = Mathf.Clamp(
            currentDistance, minDistance, maxDistance
        );

        targetDistance = currentDistance;

        UpdateCameraPosition();

        Debug.Log("3D MAP NAVIGATION INITIALIZED");
    }

    private void Update()
    {
        if (Mouse.current == null)
            return;

        HandleZoom();
        HandleOrbit();
        HandlePan();
    }

    private void LateUpdate()
    {
        UpdateSmoothZoom();
        UpdateCameraPosition();
    }

    private void HandleZoom()
    {
        float scroll = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) < 0.01f)
            return;

        // Exponential zoom prevents large jumps.
        float zoomFactor = Mathf.Exp(-scroll * zoomSensitivity);

        targetDistance *= zoomFactor;

        targetDistance = Mathf.Clamp(
            targetDistance,
            minDistance,
            maxDistance
        );
    }

    private void HandleOrbit()
    {
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            isOrbiting = true;
            previousMousePosition =
                Mouse.current.position.ReadValue();
        }

        if (Mouse.current.rightButton.wasReleasedThisFrame)
            isOrbiting = false;

        if (!isOrbiting || !Mouse.current.rightButton.isPressed)
            return;

        Vector2 currentMousePosition =
            Mouse.current.position.ReadValue();

        Vector2 mouseDelta =
            currentMousePosition - previousMousePosition;

        yaw += mouseDelta.x * orbitSensitivity;
        pitch -= mouseDelta.y * orbitSensitivity;

        pitch = Mathf.Clamp(
            pitch,
            minVerticalAngle,
            maxVerticalAngle
        );

        previousMousePosition = currentMousePosition;
    }

    private void HandlePan()
    {
        if (Mouse.current.middleButton.wasPressedThisFrame)
        {
            isPanning = true;
            previousMousePosition =
                Mouse.current.position.ReadValue();
        }

        if (Mouse.current.middleButton.wasReleasedThisFrame)
            isPanning = false;

        if (!isPanning || !Mouse.current.middleButton.isPressed)
            return;

        Vector2 currentMousePosition =
            Mouse.current.position.ReadValue();

        Vector2 mouseDelta =
            currentMousePosition - previousMousePosition;

        float worldUnitsPerPixel =
            (2f * currentDistance *
            Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad))
            / Screen.height;

        Vector3 cameraRight = mainCamera.transform.right;
        Vector3 cameraUp = mainCamera.transform.up;

        Vector3 movement =
            (-cameraRight * mouseDelta.x
            - cameraUp * mouseDelta.y)
            * worldUnitsPerPixel
            * panSensitivity;

        // Keep the target on the horizontal map plane.
        movement.y = 0f;

        orbitTarget += movement;

        previousMousePosition = currentMousePosition;
    }

    private void UpdateSmoothZoom()
    {
        currentDistance = Mathf.SmoothDamp(
            currentDistance,
            targetDistance,
            ref zoomVelocity,
            zoomSmoothTime
        );
    }

    private void UpdateCameraPosition()
    {
        Quaternion rotation =
            Quaternion.Euler(pitch, yaw, 0f);

        Vector3 offset =
            rotation * Vector3.back * currentDistance;

        mainCamera.transform.position =
            orbitTarget + offset;

        mainCamera.transform.rotation =
            Quaternion.LookRotation(
                orbitTarget - mainCamera.transform.position,
                Vector3.up
            );
    }
}