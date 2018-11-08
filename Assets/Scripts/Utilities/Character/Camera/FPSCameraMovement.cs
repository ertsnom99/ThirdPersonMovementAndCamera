using System;
using UnityEngine;

// This script requires thoses components and will be added if they aren't already there
[RequireComponent(typeof(Camera))]

public class FPSCameraMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField]
    protected float m_rotationSpeed = 115.0f;
    [SerializeField]
    protected float m_fromAngle = 0.0f;
    [SerializeField]
    protected float m_toAngle = 0.0f;
    [SerializeField]
    private int m_angleCorrection = 2;

    private Vector3 m_zeroDegreeForward;

    [Header("Lock on")]
    [SerializeField]
    private float m_maxLockOnDistance = 1.0f;
    [SerializeField]
    private LayerMask m_lockOnDetectionLayer;
    [SerializeField]
    protected float m_lockOnSpeed = 400.0f;

    public Transform TargetLockedOn { get; private set; }

    [Header("Froze")]
    [SerializeField]
    private bool m_frozenRotation = false;

    public bool FrozenRotation
    {
        get { return m_frozenRotation; }
        protected set { m_frozenRotation = value; }
    }

    [Header("Debug")]
    [SerializeField]
    protected bool m_debug = false;

    protected Camera m_camera;

    private void Awake()
    {
        TargetLockedOn = null;

        m_camera = GetComponent<Camera>();
    }

    private void Start()
    {
        // The "0 degree forward" must stored again in case that the parent rotated
        m_zeroDegreeForward = transform.parent.forward;

        // Adjust rotation if out of range
        float angle = GetAngle();
        LimitRotation(angle, angle);
    }

    public void RotateCamera(Inputs inputs, Transform[] possibleTargets = null)
    {
        // The "0 degree forward" must stored again in case that the parent rotated
        m_zeroDegreeForward = transform.parent.forward;

        // If the camera isn't frozen
        if (!FrozenRotation)
        {
            float previousAngle = GetAngle();
            float rotDoneAngle = 0.0f;

            // Try to find a target
            TargetLockedOn = possibleTargets != null ? FindClosestTargetInSight(possibleTargets) : null;

            // If a target was found
            if (TargetLockedOn)
            {
                // Rotate the camera
                rotDoneAngle = RotateWithTarget(TargetLockedOn);
            }
            else
            {
                if (TargetLockedOn)
                {
                    TargetLockedOn = null;
                }

                // Rotate the camera
                rotDoneAngle = RotateWithInputs(inputs);
            }

            // Limit the rotation
            if (m_fromAngle != m_toAngle)
            {
                LimitRotation(previousAngle, rotDoneAngle);
            }
        }

        // Draw lines if in debug mode
        if (m_debug)
        {
            DebugCamera();
        }
    }

    private Transform FindClosestTargetInSight(Transform[] lockableTarget)
    {
        Transform bestTarget = null;
        float bestDistance = Mathf.Infinity;

        Vector3 targetViewportPos;
        RaycastHit hit;
        Vector3 targetVectorToCenter;

        // Check all lockable target
        foreach (Transform target in lockableTarget)
        {
            // Viewport coordonates of the target
            targetViewportPos = m_camera.WorldToViewportPoint(target.position);

            // Consider only targets in front of the camera
            if (Mathf.Clamp01(targetViewportPos.x) == targetViewportPos.x && Mathf.Clamp01(targetViewportPos.y) == targetViewportPos.y && targetViewportPos.z > 0.0f)
            {
                // Check if the target can be seen and isn't out of range of the lock on
                Physics.Raycast(transform.position, target.transform.position - transform.position, out hit, Mathf.Infinity, m_lockOnDetectionLayer);

                if (hit.collider != null && Array.IndexOf(lockableTarget, hit.collider.transform) != -1)
                {
                    // If this target is already locked on
                    if (target == TargetLockedOn)
                    {
                        // No need to check any target, still locked on this target
                        return target;
                    }
                    // If the target is in range of lock on
                    else if (hit.distance <= m_maxLockOnDistance)
                    {
                        // Find how close the target is to the center of the view
                        // Vector representing the distance of the target from the center
                        targetVectorToCenter = new Vector3(targetViewportPos.x - 0.5f, targetViewportPos.y - 0.5f, targetViewportPos.z);
                        float targetDistanceToCenter = targetVectorToCenter.magnitude;

                        // Check if this is a better target
                        if (targetDistanceToCenter < bestDistance)
                        {
                            bestTarget = target;
                            bestDistance = targetDistanceToCenter;
                        }
                    }
                }
            }
        }

        return bestTarget;
    }

    private float RotateWithInputs(Inputs inputs)
    {
        float rotX = inputs.yAxis * m_rotationSpeed * Time.deltaTime;

        transform.rotation *= Quaternion.AngleAxis(rotX, Vector3.right);

        return rotX;
    }

    private float RotateWithTarget(Transform target)
    {
        Vector3 targetDirection = (target.position - transform.position).normalized;

        Quaternion lookRotation = Quaternion.LookRotation(targetDirection);

        float angle = GetAngle();

        float rotation = Mathf.MoveTowardsAngle(angle, lookRotation.eulerAngles.x, m_lockOnSpeed * Time.deltaTime);
        transform.localRotation = Quaternion.Euler(rotation, 0.0f, 0.0f);

        return CalculateRotationDone(angle, rotation);
    }

    private float CalculateRotationDone(float previousAngle, float currentAngle)
    {
        // Calculate if the rotation was clockwise or counterclockwise
        float angleDif = currentAngle - previousAngle;

        if (angleDif < 0.0f)
        {
            angleDif += 360.0f;
        }
        // If no rotation was done, immediately return 0 degrees
        else if (angleDif == 0.0f)
        {
            return 0.0f;
        }

        float direction = -1.0f;
        if (angleDif > 0.0f && angleDif < 180.0f) direction = 1.0f;

        float rotation = 0.0f;

        // Calculate how much rotation was done
        if (direction == 1.0f)
        {
            if (previousAngle <= currentAngle)
            {
                rotation = direction * (currentAngle - previousAngle);
            }
            else
            {
                rotation = direction * (360.0f - previousAngle + currentAngle);
            }
        }
        else
        {
            if (previousAngle <= currentAngle)
            {
                rotation = direction * (360.0f - currentAngle + previousAngle);
            }
            else
            {
                rotation = direction * (previousAngle - currentAngle);
            }
        }

        return rotation;
    }

    // Returns the angle relative to the "0 foward axis"
    private float GetAngle()
    {
        float rot = Vector3.Angle(transform.forward, m_zeroDegreeForward);
        rot = Mathf.Round(rot * Mathf.Pow(10.0f, m_angleCorrection)) / Mathf.Pow(10.0f, m_angleCorrection);

        float rotDir;

        if (transform.parent != null)
        {
            rotDir = Mathf.Sign(transform.parent.InverseTransformDirection(transform.forward).y);
        }
        else
        {
            rotDir = Mathf.Sign(transform.forward.y);
        }

        if (rotDir == 1 && rot != 0) rot = 360 - rot;

        return rot;
    }

    private void LimitRotation(float previousAngle, float rotDoneAngle)
    {
        float angle = GetAngle();

        // Check if the camera rotation is in the "dead zone"
        if (m_fromAngle < m_toAngle && angle < m_toAngle && angle > m_fromAngle)
        {
            if (rotDoneAngle >= 0.0f)
            {
                float adjustRot = m_fromAngle - angle;
                transform.rotation *= Quaternion.AngleAxis(adjustRot, Vector3.right);
            }
            else
            {
                float adjustRot = m_toAngle - angle;
                transform.rotation *= Quaternion.AngleAxis(adjustRot, Vector3.right);
            }
        }
        else if (m_fromAngle > m_toAngle && !(angle >= m_toAngle && angle <= m_fromAngle))
        {
            if (rotDoneAngle >= 0.0f)
            {
                float adjustRot = m_fromAngle - angle;
                transform.rotation *= Quaternion.AngleAxis(adjustRot, Vector3.right);
            }
            else
            {
                float adjustRot = m_toAngle - angle;
                transform.rotation *= Quaternion.AngleAxis(adjustRot, Vector3.right);
            }
        }
        else
        {
            // Check if the camera went over the limit
            float maxRot = MaxRotationAuthorized(previousAngle, rotDoneAngle);

            if (Mathf.Abs(rotDoneAngle) > maxRot)
            {
                if (rotDoneAngle > 0.0f)
                {
                    float adjustRot = m_fromAngle - angle;
                    transform.rotation *= Quaternion.AngleAxis(adjustRot, Vector3.right);
                }
                else if (rotDoneAngle < 0.0f)
                {
                    float adjustRot = m_toAngle - angle;
                    transform.rotation *= Quaternion.AngleAxis(adjustRot, Vector3.right);
                }

                return;
            }
        }
    }

    private float MaxRotationAuthorized(float previousAngle, float lastRotAngle)
    {
        float maxRot = previousAngle;

        if (m_fromAngle > m_toAngle)
        {
            if (lastRotAngle > 0.0f)
            {
                if (previousAngle > m_fromAngle)
                {
                    maxRot = (360.0f - previousAngle) + m_fromAngle;
                }
                else
                {
                    maxRot = m_fromAngle - previousAngle;
                }
            }
            else if (lastRotAngle < 0.0f)
            {
                if (previousAngle > m_fromAngle || (previousAngle <= m_fromAngle && previousAngle >= m_toAngle))
                {
                    maxRot = previousAngle - m_toAngle;
                }
                else
                {
                    maxRot = (360.0f - m_toAngle) + previousAngle;
                }
            }
        }
        else if (m_fromAngle < m_toAngle)
        {
            if (lastRotAngle > 0.0f)
            {
                if (previousAngle <= m_fromAngle)
                {
                    maxRot = m_fromAngle - previousAngle;
                }
                else
                {
                    maxRot = (360.0f - previousAngle) + m_fromAngle;
                }
            }
            else if (lastRotAngle < 0.0f)
            {
                if (previousAngle >= m_toAngle)
                {
                    maxRot = previousAngle - m_toAngle;
                }
                else
                {
                    maxRot = 360.0f - m_toAngle + previousAngle;
                }
            }
        }

        return maxRot;
    }

    private void DebugCamera()
    {
        Debug.DrawLine(transform.position, transform.position + m_zeroDegreeForward, Color.blue);
        Debug.DrawLine(transform.position, transform.position + transform.forward, Color.green);

        if (TargetLockedOn)
        {
            Debug.DrawLine(transform.position, TargetLockedOn.position, Color.red);
        }
    }
}
