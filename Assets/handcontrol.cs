
using UnityEngine;

public class HandController : MonoBehaviour
{
    [System.Serializable]
    public class Finger
    {
        public string fingerName;

        [Range(0f, 1f)]
        public float bendInput;

        public Transform joint1;
        public Transform joint2;
        public Transform joint3;
        public float joint1Max = 75f;
        public float joint2Max = 90f;
        public float joint3Max = 45f;
        [HideInInspector] public Quaternion start1;
        [HideInInspector] public Quaternion start2;
        [HideInInspector] public Quaternion start3;
    }

    public Finger[] fingers = new Finger[5];
    public float movementSpeed = 360f;

    void Start()
    {
        foreach (Finger finger in fingers)
        {
            if (finger.joint1 != null)
                finger.start1 = finger.joint1.localRotation;

            if (finger.joint2 != null)
                finger.start2 = finger.joint2.localRotation;

            if (finger.joint3 != null)
                finger.start3 = finger.joint3.localRotation;
        }
    }

    void Update()
    {
        foreach (Finger finger in fingers)
        {
            BendJoint(finger.joint1, finger.start1,
                      finger.bendInput * finger.joint1Max);

            BendJoint(finger.joint2, finger.start2,
                      finger.bendInput * finger.joint2Max);

            BendJoint(finger.joint3, finger.start3,
                      finger.bendInput * finger.joint3Max);
        }
    }

    void BendJoint(Transform joint, Quaternion startRotation,
                   float angle)
    {
        if (joint == null)
            return;

        Quaternion target =
            startRotation * Quaternion.Euler(angle, 0f, 0f);

        joint.localRotation = Quaternion.RotateTowards(
            joint.localRotation,
            target,
            movementSpeed * Time.deltaTime
        );
    }
}
