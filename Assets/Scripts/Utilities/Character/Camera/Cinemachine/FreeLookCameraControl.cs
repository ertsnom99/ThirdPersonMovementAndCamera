using Cinemachine;
using System;
using UnityEngine;

public interface IFreeLookCameraControlSubscriber
{
    void NotifyJustSubscribed(FreeLookCameraControl freeLookCameraControlScript);
    void NotifyLockOnTargetChanged(Transform target);
}

public enum FreelookRotationType
{
    RotateOnYAxis,
    RotateOnXYAxis
}

// This script requires thoses components and will be added if they aren't already there
[RequireComponent(typeof(CinemachineFreeLook))]

/** Controls the movement of a CinemachineFreeLook camera.
 * Two rotation type can be used:
 * - RotateOnYAxis (should use "Lock To Target With World Up" for the orbit binding mode)
 * - RotateOnXYAxis (should use "Lock To Target On Assign" for the orbit binding mode AND doesn't allow lock on)
 */
public class FreeLookCameraControl : MonoSubscribable<IFreeLookCameraControlSubscriber>
{
    [Header("Movement")]
    [SerializeField]
    private float m_initXAxis = .0f;
    [SerializeField]
    private float m_initYAxis = 0.5f;
    [SerializeField]
    private float m_XRotationSpeed = 0.05f;
    [SerializeField]
    private float m_YRotationSpeed = 0.05f;

    [Header("For raycast")]
    [SerializeField]
    private GameObject[] m_ignoredGameObjectByRaycast;
    [SerializeField]
    private Camera m_controledCamera;

    public Camera ControledCamera
    {
        get { return m_controledCamera; }
        private set { m_controledCamera = value; }
    }

    [Header("Froze")]
    [SerializeField]
    private bool m_frozenRotation = false;

    public bool FrozenRotation
    {
        get { return m_frozenRotation; }
        private set { m_frozenRotation = value; }
    }

    [Header("Lock on")]
    [SerializeField]
    private float m_maxLockOnDistance = 5.0f;
    [SerializeField]
    private LayerMask m_lockOnDetectionLayer;
    [SerializeField]
    private float m_lookAtTargetMoveSpeed = 0.3f;
    private Vector3 m_initialLookAtPositionOffsetRelatifToParent;

    public Transform TargetLockedOn { get; private set; }

    [Header("Fade")]
    [SerializeField]
    private bool m_useFade = false;
    [SerializeField]
    private float m_minDistanceForOpaque = 4.6f;
    [SerializeField]
    private float m_minDistanceForTransparent = 2.0f;
    [SerializeField]
    private Renderer[] m_renderersToFade;

    private CinemachineFreeLook m_freeLookScript;
    private Transform m_cameraLookAtTarget;

    private void Awake()
    {
        m_freeLookScript = GetComponent<CinemachineFreeLook>();

        m_freeLookScript.m_XAxis.Value = m_initXAxis;
        m_freeLookScript.m_YAxis.Value = m_initYAxis;

        m_freeLookScript.m_XAxis.m_InputAxisName = "";
        m_freeLookScript.m_YAxis.m_InputAxisName = "";

        m_cameraLookAtTarget = m_freeLookScript.LookAt;
        m_initialLookAtPositionOffsetRelatifToParent = m_cameraLookAtTarget.position - m_cameraLookAtTarget.parent.position;
    }

    public void RotateCamera(Inputs inputs, FreelookRotationType rotationType, Transform[] possibleTargets = null)
    {
        // If the camera isn't frozen
        if (!FrozenRotation)
        {
            // Try to find a target
            if (possibleTargets != null)
            {
                FindClosestTargetInSight(possibleTargets);
            }
            else if (TargetLockedOn != null)
            {
                TargetLockedOn = null;

                foreach (IFreeLookCameraControlSubscriber subscriber in m_subscribers)
                {
                    subscriber.NotifyLockOnTargetChanged(null);
                }
            }
            
            // If a target was found
            if (rotationType == FreelookRotationType.RotateOnYAxis && TargetLockedOn != null)
            {
                RotateWithTarget(inputs, rotationType);
            }
            else
            {
                if (TargetLockedOn != null)
                {
                    TargetLockedOn = null;
                }

                RotateWithInputs(inputs, rotationType);
            }

            if (m_useFade)
            {
                UpdateDistanceFade();
            }
        }
    }

    private Transform FindClosestTargetInSight(Transform[] lockableTarget)
    {
        Transform bestTarget = null;
        float bestDistance = Mathf.Infinity;

        Vector3 targetViewportPos;
        float initialLookAtTargetToCamDistance = (m_cameraLookAtTarget.parent.position + m_initialLookAtPositionOffsetRelatifToParent - m_controledCamera.transform.position).magnitude;

        RaycastHit hit;
        Vector3 targetVectorToCenter;

        // Check all lockable target
        foreach (Transform target in lockableTarget)
        {
            // Viewport coordonates of the target
            targetViewportPos = m_controledCamera.WorldToViewportPoint(target.position);

            // Consider only targets in front of the camera
            if (Mathf.Clamp01(targetViewportPos.x) == targetViewportPos.x && Mathf.Clamp01(targetViewportPos.y) == targetViewportPos.y && targetViewportPos.z > initialLookAtTargetToCamDistance)
            {
                // Check if the target can be seen and isn't out of range of the lock on
                Raycast(target.transform.position - m_controledCamera.transform.position, out hit, Mathf.Infinity, m_lockOnDetectionLayer, m_ignoredGameObjectByRaycast);

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

        // Warn subscribers if the target changed
        if (bestTarget != TargetLockedOn)
        {
            foreach (IFreeLookCameraControlSubscriber subscriber in m_subscribers)
            {
                subscriber.NotifyLockOnTargetChanged(bestTarget);
            }
        }

        TargetLockedOn = bestTarget;

        return bestTarget;
    }

    private void RotateWithTarget(Inputs inputs, FreelookRotationType rotationType)
    {
        // Use trigonometry to find new look at target position
        Vector3 cameraToTarget = TargetLockedOn.position - m_controledCamera.transform.position;
        Vector3 cameraToInitialLookAtTarget = m_cameraLookAtTarget.parent.position + m_initialLookAtPositionOffsetRelatifToParent - m_controledCamera.transform.position;
        float distanceCameraToTarget = cameraToInitialLookAtTarget.magnitude / Mathf.Cos(Vector3.Angle(cameraToTarget, cameraToInitialLookAtTarget) * Mathf.Deg2Rad);
        Vector3 lookAtPosition = m_controledCamera.transform.position + cameraToTarget.normalized * distanceCameraToTarget;

        // Move the look at target towards the target
        m_freeLookScript.LookAt.position = Vector3.Lerp(m_freeLookScript.LookAt.position, lookAtPosition, m_lookAtTargetMoveSpeed * Time.deltaTime);

        UpdateAxis(inputs, rotationType);
    }

    private void RotateWithInputs(Inputs inputs, FreelookRotationType rotationType)
    {
        // Move the look at target towards the follow target
        if (m_freeLookScript.LookAt.position != m_cameraLookAtTarget.parent.position + m_initialLookAtPositionOffsetRelatifToParent)
        {
            m_freeLookScript.LookAt.position = Vector3.Lerp(m_freeLookScript.LookAt.position, m_cameraLookAtTarget.parent.position + m_initialLookAtPositionOffsetRelatifToParent, m_lookAtTargetMoveSpeed * Time.deltaTime);
        }

        UpdateAxis(inputs, rotationType);
    }

    private void UpdateAxis(Inputs inputs, FreelookRotationType rotationType)
    {
        if (rotationType == FreelookRotationType.RotateOnXYAxis)
        {
            m_freeLookScript.m_XAxis.Value += -inputs.xAxis * m_XRotationSpeed * Time.deltaTime;
        }

        m_freeLookScript.m_YAxis.Value = Mathf.Clamp01(m_freeLookScript.m_YAxis.Value + -inputs.yAxis * m_YRotationSpeed * Time.deltaTime);
    }

    private void UpdateDistanceFade()
    {
        // Calulate transparency
        float distanceToTarget = (m_cameraLookAtTarget.position - m_controledCamera.transform.position).magnitude;
        float transparency = Mathf.InverseLerp(m_minDistanceForTransparent, m_minDistanceForOpaque, distanceToTarget);

        // Apply transparency
        foreach (Renderer renderer in m_renderersToFade)
        {
            foreach (Material material in renderer.materials)
            {
                material.color = new Color(material.color.r, material.color.g, material.color.b, transparency);
            }
        }
    }

    // A raycast that shoots from the camera and output the first RaycastHit that isn't behind the follow target or that hitted an ignored GameObject
    public bool Raycast(Vector3 direction, out RaycastHit hitInfo, float range, int detectionLayer, GameObject[] ignoredGameObjects)
    {
        Vector3 hitViewportPos;
        //float lookAtTargetDistanceToCam = (m_freeLookScript.Follow.position - m_controledCamera.transform.position).magnitude;
        float initialLookAtTargetToCamDistance = (m_cameraLookAtTarget.parent.position + m_initialLookAtPositionOffsetRelatifToParent - m_controledCamera.transform.position).magnitude;

        // Cast a ray the check which player is selected
        RaycastHit[] hits = Physics.RaycastAll(m_controledCamera.transform.position, direction, range, detectionLayer);
        Array.Sort(hits, delegate (RaycastHit a, RaycastHit b) { return a.distance.CompareTo(b.distance); });

        RaycastHit validHit = new RaycastHit();
        int i = 0;

        // TODO: ignore collider behind the follow target
        while (validHit.collider == null && i < hits.Length)
        {
            // Viewport coordonates of the target
            hitViewportPos = m_controledCamera.WorldToViewportPoint(hits[i].point);

            if (hitViewportPos.z > initialLookAtTargetToCamDistance && Array.IndexOf(ignoredGameObjects, hits[i].collider.gameObject) == -1)
            {
                validHit = hits[i];
            }

            i++;
        }

        hitInfo = validHit;

        return validHit.collider != null;
    }

    public Transform GetFollowTarget()
    {
        return m_freeLookScript.Follow;
    }

    public Transform GetLookAtTarget()
    {
        return m_freeLookScript.LookAt;
    }

    // Override the Subscribe method
    public override void Subscribe(IFreeLookCameraControlSubscriber subscriber)
    {
        base.Subscribe(subscriber);
        subscriber.NotifyJustSubscribed(this);
    }
}
