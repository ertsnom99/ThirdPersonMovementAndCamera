using UnityEngine;
using UnityEngine.AI;

// This script requires thoses components and will be added if they aren't already there
[RequireComponent(typeof(NavMeshAgent))]

// This is a modified version of this script:
// https://docs.unity3d.com/Manual/nav-CouplingAnimationAndNavigation.html
public class AgentMovement : MonoBehaviour
{
    private NavMeshAgent m_agent;
    private Animator m_animator;
    
    private Vector2 m_agentVelocity = Vector2.zero;

    private Quaternion m_previousRotation;

    public bool AgentMovementEnabled { get; private set; }

    private Vector3 m_destination;
    
    [Space(20)]
    [Header("Animation")]
    [SerializeField]
    private float m_minVelocityToAnimMove = 0.01f;
    // How much of the agent radius the remaining distance must be bigger than to animate the move
    // At 0.5f, the character will stop it's move animation when the remaining distance is lesser than half it's agent radius.
    [SerializeField]
    private float m_minAgentRadiusDistanceToAnimMove = 0.45f;
    [SerializeField]
    private float m_minRotationToAnim = 0.05f;

    [Header("Look At Destination")]
    [SerializeField]
    private float m_minDistanceToLookAtDestination = 0.5f;
    [SerializeField]
    private float m_lookAtAngularSpeed = 5.0f;
    private GameObject m_lookAtTarget;

    [Header("Speed")]
    [SerializeField]
    private float m_walkSpeed = 12.0f;
    [SerializeField]
    private float m_runSpeed = 16.0f;

    [Header("Angular Speed")]
    [SerializeField]
    private float m_angularWalkSpeed = 160.0f;
    [SerializeField]
    private float m_angularRunSpeed = 210.0f;

    private float m_unmodifiedSpeed;
    private float m_unmodifiedAngularSpeed;

    protected int m_isMovingParamHashId = Animator.StringToHash(IsMovingParamNameString);
    protected int m_XVelocityParamHashId = Animator.StringToHash(XVelocityParamNameString);
    protected int m_ZVelocityParamHashId = Animator.StringToHash(ZVelocityParamNameString);
    protected int m_isRunningParamHashId = Animator.StringToHash(IsRunningParamNameString);
    protected int m_isRotatingParamHashId = Animator.StringToHash(IsRotatingParamNameString);
    protected int m_rotationParamHashId = Animator.StringToHash(RotationParamNameString);
    
    public const string IsMovingParamNameString = "IsMoving";
    public const string XVelocityParamNameString = "XVelocity";
    public const string ZVelocityParamNameString = "ZVelocity";
    public const string IsRunningParamNameString = "IsRunning";
    public const string IsRotatingParamNameString = "IsRotating";
    public const string RotationParamNameString = "Rotation";

    private void Awake()
    {
        m_agent = GetComponent<NavMeshAgent>();
        m_animator = GetComponent<Animator>();

        m_unmodifiedSpeed = m_agent.speed;
        m_unmodifiedAngularSpeed = m_agent.angularSpeed;

        Run(false);

        // Don’t update position automatically, we will do it ourself, or we wouldn't be
        // able to calculate teh delta position in the update
        m_agent.updatePosition = false;

        AgentMovementEnabled = true;
    }

    private void Start()
    {
        m_previousRotation = transform.rotation;
    }

    private void Update()
    {
        if(AgentMovementEnabled)
        { 
            Vector3 worldDeltaPosition = m_agent.nextPosition - transform.position;

            // Map 'worldDeltaPosition' to local space (convert global to local)
            // By using Dot product, we calculate the pourcentage of overlape of the
            // worldDeltaPosition over each unit directional vector (P.S.: transform.right
            // isn't necessary (1, 0, 0) since it's the right direction of the player
            // in global space), since dot(a, b) = |a| x |b| x cos(o)
            float dx = Vector3.Dot(transform.right, worldDeltaPosition);
            float dy = Vector3.Dot(transform.forward, worldDeltaPosition);
            Vector2 deltaPosition = new Vector2(dx, dy);

            // Update velocity if time advances
            if (Time.deltaTime > 1e-5f)
            {
                // delta movement/ delta time = speed (velocity)  
                m_agentVelocity = deltaPosition / Time.deltaTime;

                if (m_unmodifiedSpeed != m_runSpeed)
                {
                    // makes the velocity relative to the max speed (1 = full walk speed)
                    m_agentVelocity = m_agentVelocity / m_agent.speed;

                    // Cap the agent velocity (smoothing might create vlaues over 1.0f)
                    m_agentVelocity.x = Mathf.Clamp(m_agentVelocity.x, -1.0f, 1.0f);
                    m_agentVelocity.y = Mathf.Clamp(m_agentVelocity.y, -1.0f, 1.0f);
                }
                else
                {
                    // Cap the agent velocity (for how much walking + how much running)
                    m_agentVelocity.x = Mathf.Clamp(m_agentVelocity.x, -m_walkSpeed, m_walkSpeed) / m_walkSpeed + (Mathf.Sign(m_agentVelocity.x) * Mathf.Clamp(Mathf.Abs(m_agentVelocity.x) - m_walkSpeed, 0.0f, m_runSpeed - m_walkSpeed)) / (m_runSpeed - m_walkSpeed);
                    m_agentVelocity.y = Mathf.Clamp(m_agentVelocity.y, -m_walkSpeed, m_walkSpeed) / m_walkSpeed + (Mathf.Sign(m_agentVelocity.y) * Mathf.Clamp(Mathf.Abs(m_agentVelocity.y) - m_walkSpeed, 0.0f, m_runSpeed - m_walkSpeed)) / (m_runSpeed - m_walkSpeed);
                }
            }


            //Calculate rotation
            float rotation = CalculateSignedAngle(m_previousRotation, transform.rotation);
            m_previousRotation = transform.rotation;

            bool shouldMove = m_agentVelocity.magnitude >= m_minVelocityToAnimMove && m_agent.remainingDistance > m_agent.radius * m_minAgentRadiusDistanceToAnimMove;
            bool shouldRotate = Mathf.Abs(rotation) > m_minRotationToAnim;

            // Update animation parameters
            m_animator.SetBool(m_isMovingParamHashId, shouldMove);
            m_animator.SetFloat(m_XVelocityParamHashId, m_agentVelocity.x);
            m_animator.SetFloat(m_ZVelocityParamHashId, m_agentVelocity.y);
            m_animator.SetBool(m_isRunningParamHashId, m_unmodifiedSpeed == m_runSpeed);
            m_animator.SetBool(m_isRotatingParamHashId, shouldRotate);
            m_animator.SetFloat(m_rotationParamHashId, rotation);
        }
    }
    
    private float CalculateSignedAngle(Quaternion rotationA, Quaternion rotationB)
    {
        // get a "forward vector" for each rotation
        Vector3 forwardA = rotationA * Vector3.forward;
        Vector3 forwardB = rotationB * Vector3.forward;

        // get a numeric angle for each vector, on the X-Z plane (relative to world forward)
        float angleA = Mathf.Atan2(forwardA.x, forwardA.z) * Mathf.Rad2Deg;
        float angleB = Mathf.Atan2(forwardB.x, forwardB.z) * Mathf.Rad2Deg;

        // get the signed difference in these angles
        return Mathf.DeltaAngle(angleA, angleB);
    }

    private void OnAnimatorMove()
    {
        if (AgentMovementEnabled)
        {
            // Update position to agent position
            transform.position = m_agent.nextPosition;

            // When the agent gets close enough to it's destination, make shure it ends up looking at it's destination
            if (m_lookAtTarget != null && m_agent.remainingDistance <= m_minDistanceToLookAtDestination)
            {
                Vector3 characterToTarget = m_lookAtTarget.transform.position - transform.position;
                characterToTarget.y = 0;

                if (characterToTarget != Vector3.zero)
                {
                    Quaternion rotation = Quaternion.LookRotation(characterToTarget);
                    transform.rotation = Quaternion.Slerp(transform.rotation, rotation, Time.deltaTime * m_lookAtAngularSpeed);
                }
            }
        }

        /*// Check if the agent reached it's destination
        if (agent.remainingDistance < agent.stoppingDistance)
        {
            //......
        }*/
    }

    public void SetLookAtTarget(GameObject target)
    {
        m_lookAtTarget = target;
    }

    public void EnableAgentMovement(bool enable)
    {
        if (!enable) ClearDestination();

        // Replace the NavMeshAgent where the character is (using m_agent.nextPosition instead might not work
        // if the position is to far from the current NavMeshAgent position)
        m_agent.Warp(transform.position);
        // Stop/Start the agent movement along its current path
        m_agent.isStopped = !enable;
        
        AgentMovementEnabled = enable;
    }

    public void SetDestination(Vector3 destination)
    {
        m_destination = destination;
        m_agent.SetDestination(m_destination);
    }

    public void ClearDestination()
    {
        m_destination = transform.position;
        m_agent.destination = m_destination;
    }
    
    public void Run(bool run)
    {
		// Calculate any speed modifier that already was applied
        float currentModifier = m_agent.speed / m_unmodifiedSpeed;

        if (run)
        {
            m_unmodifiedSpeed = m_runSpeed;
            m_unmodifiedAngularSpeed = m_angularRunSpeed;
        }
        else
        {
            m_unmodifiedSpeed = m_walkSpeed;
            m_unmodifiedAngularSpeed = m_angularWalkSpeed;
        }

        m_agent.speed = m_unmodifiedSpeed * currentModifier;
        m_agent.angularSpeed = m_unmodifiedAngularSpeed * currentModifier;
    }

    public void ApplySpeedModifier(float speedModifier)
    {
        m_agent.speed = m_unmodifiedSpeed * speedModifier;
        m_agent.angularSpeed = m_unmodifiedAngularSpeed * speedModifier;
    }

    public bool Warp(Vector3 warpPosition)
    {
        return m_agent.Warp(warpPosition);
    }
}
