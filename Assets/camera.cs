
using UnityEngine;


public class CameraOrbit : MonoBehaviour
{
    public Transform pivotTarget;
    public float rotateSpeed = 4f;
    public float zoomSpeed = 2f;
    public float minDistance = 0.5f;
    public float maxDistance = 8f;

    private Transform cam;
    private float distance = 2.5f;
    private float yaw;
    private float pitch = 10f;

    void Start()
    {
        if (pivotTarget != null)
            transform.position = pivotTarget.position;

        cam = GetComponentInChildren<Camera>().transform;

        Vector3 offset = cam.localPosition;
        distance = offset.magnitude;

        yaw = 0f;
        pitch = 0f;

        UpdateCamera();
    }

    void LateUpdate()
    {
        if (Input.GetMouseButton(1))
        {
            yaw += Input.GetAxis("Mouse X") * rotateSpeed;
            pitch -= Input.GetAxis("Mouse Y") * rotateSpeed;
            pitch = Mathf.Clamp(pitch, -70f, 70f);
        }

        distance -= Input.mouseScrollDelta.y * zoomSpeed;
        distance = Mathf.Clamp(distance, minDistance, maxDistance);

        UpdateCamera();
    }

    void UpdateCamera()
    {
        Quaternion orbitRotation =
            Quaternion.Euler(pitch, yaw, 0f);

        transform.rotation = orbitRotation;

        cam.localPosition = new Vector3(0f, 0f, -distance);
        cam.localRotation = Quaternion.identity;
    }
}
