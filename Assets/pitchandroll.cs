
using UnityEngine;

public class HandOrientationController : MonoBehaviour
{
    public InputReader inputReader;

    public Transform pitchJoint;

    public float pitchScale = 1f;
    public float smoothSpeed = 8f;
    public float pitchDirection = -1f;

    Quaternion pitchStart;

    void Start()
    {
        if (pitchJoint != null)
            pitchStart = pitchJoint.localRotation;
    }

    void Update()
    {
        if (inputReader == null ||
            !inputReader.mpuCalibrated)
            return;

        float p = (inputReader.pitch -
            inputReader.neutralPitch) *
            pitchDirection * pitchScale;

        if (pitchJoint != null)
        {
            Quaternion target =
                pitchStart * Quaternion.Euler(p, 0f, 0f);

            pitchJoint.localRotation =
                Quaternion.Slerp(
                    pitchJoint.localRotation,
                    target,
                    smoothSpeed * Time.deltaTime);
        }
    }
}
