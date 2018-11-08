using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public interface IStaminaSubscriber
{
    void NotifyJustSubscribed(CharacterControllerMovement characterMovementScript);
    void NotifyStaminaExhaustion(GameObject character, bool exhausted);
    void NotifyStaminaConsomption(GameObject character, bool start);
    void NotifyStaminaChange(GameObject character, float stamina);
}

public enum MovementType
{
    Strafing,
    MoveToward
}

// This script requires thoses components and will be added if they aren't already there
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]

/** Controls the movement of a CharacterController.
 * Two movement type can be used:
 * - Strafing
 * - MoveToward (always walk forward and turns around to go in the given direction (relatif to world space). Doesn't allow lock on)
 */
public class CharacterControllerMovement : MonoSubscribable<IStaminaSubscriber>
{
    protected CharacterController m_characterController;
    protected Animator m_animator;

    private Vector3 m_localVelocity = Vector3.zero;
    private Vector3 m_globalSlipeVelocity = Vector3.zero;
    private Vector3 m_globalVelocity = Vector3.zero;

    private Vector2 m_currentMovementInput;
    private Dictionary<string, float> m_currentModifiers;

    public bool UsesRootMotion { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsExhausted { get; private set; }
    public bool IsSliping { get; private set; }
    public bool IsAirborne { get; private set; }
    public bool IsStaggered { get; private set; }

    [Header("Movement")]
    [SerializeField]
    private float m_yRotationSpeed = 115.0f;
    [SerializeField]
    private float m_lockOnRotationSpeed = 10.0f;
    private float m_rotationExecuted = 0f;

    [SerializeField]
    private float m_maxXWalkSpeed = 5.0f;
    [SerializeField]
    private float m_maxZWalkSpeed = 5.0f;

    [SerializeField]
    private float m_weightMultiplier = 2.8f;
    [SerializeField]
    private float m_maxFallingSpeed = 100.0f;

    [SerializeField]
    private float m_acceleration = 20.0f;
    [SerializeField]
    private float m_decceleration = 32.0f;

    [Header("Running")]
    [SerializeField]
    private bool m_runningAllowed = true;
    [SerializeField]
    private float m_minMovInputForRun = 0.5f;

    public float Stamina { get; private set; }

    [SerializeField]
    private float m_maxStamina = 100.0f;

    public float MaxStamina
    {
        get { return m_maxStamina; }
        private set
        {
            if (value < 0) value = 0;
            m_maxStamina = value;
        }
    }

    [SerializeField]
    private float m_staminaConsuption = 20;
    [SerializeField]
    private float m_staminaRegeneration = 5;
    [SerializeField]
    private float m_minStaminaToEndExhaustion = 25;

    public bool IsStaminaFrozen { get; private set; }

    [Header("Jumping")]
    [SerializeField]
    private bool m_jumpAllowed = false;

    public bool JumpAllowed
    {
        get { return m_jumpAllowed; }
        set { m_jumpAllowed = value; }
    }

    [SerializeField]
    private float m_jumpStrength = 10.6f;

    [Header("Airborne")]
    [SerializeField]
    private bool m_controlWhileAirborne = true;

    [Header("Landing")]
    [SerializeField]
    private float m_minLandingVelocityForMethodAndStagger = 25.0f;
	
    [SerializeField]
    private bool m_useMethodCallOnLanding = false;

    public bool UseMethodCallOnLanding
    {
        get { return m_useMethodCallOnLanding; }
        private set { m_useMethodCallOnLanding = value; }
    }
	
    [SerializeField]
    private string m_methodCalledOnLanding = "";

    [SerializeField]
    private bool m_useStaggeredLanding = false;

    public bool UseStaggeredLanding
    {
        get { return m_useStaggeredLanding; }
        private set { m_useStaggeredLanding = value; }
    }
	
    [SerializeField]
    private float m_staggeredLandingDuration = 1.0f;

    private IEnumerator m_staggeredCoroutine;

    [Header("Sliping")]
    [SerializeField]
    private bool m_preventSlipeOfSmallStep = true;

    public bool PreventSlipeOfSmallStep
    {
        get { return m_preventSlipeOfSmallStep; }
        private set { m_preventSlipeOfSmallStep = value; }
    }

    // When detecting collision with the controller to calculate a sliping Vector3, more than one collision
    // for the same movement might occur. This variable will tell when a certain collision indicated
    // that the character should not be sliping.
    private bool m_doesntNeedToSlip = false;
    // While keep track of slipe velocity during eahc frame
    private Vector3 m_currentSlipeVelocity;
    // When the character check if he must slipe, he might be on the edge of something and NOT in a slope.
    // To check if it's the case, a raycast is used. If the ray hit something, it consider that the character
    // is in a slop.
    // The raycast origin isn't the same has the hit point. It is a little bit off. It's place at
    // hit normal * preventSlipOfSmallStepRaycastOffset
    [SerializeField]
    private float m_preventSlipOfSmallStepRaycastOffset = 0.01f;
    // The raycast distance. It should always be at least bigger than preventSlipOfSmallStepRaycastOffset.
    [SerializeField]
    private float m_preventSlipOfSmallStepRaycastDistance = 0.05f;
    // An angle, calculated when detecting a slope, exciding this value will prevent
    // the player from being treated as in a slope
    [SerializeField]
    private float m_maxConsideredSlipAngle = 89.0f;
    [SerializeField]
    private float m_slidingAcceleration = 10.0f;
    private float m_lastHitDistance = 0.0f;

    [Header("Adjust velocity with normal")]
    [SerializeField]
    private bool m_adjustVelocityWhenHittingWallOrCeilling = true;

    public bool AdjustVelocityWhenHittingWallOrCeilling
    {
        get { return m_adjustVelocityWhenHittingWallOrCeilling; }
        private set { m_adjustVelocityWhenHittingWallOrCeilling = value; }
    }

    // Modifiers are a percentage of how much of the movement must be executed, NOT how much ISN'T executed
    [Header("Modifiers")]
    [SerializeField]
    private float m_sideStepModifier = 1.0f;
    [SerializeField]
    private float m_backwardModifier = 1.0f;
    [SerializeField]
    private float m_airborneModifier = 0.2f;
    [SerializeField]
    private float m_runModifier = 2.4f;
    private float m_globalModifier = 1.0f;

    [Header("Animation")]
    [SerializeField]
    private bool m_updateAnimatorVariables = false;

    public bool UpdateAnimatorVariables
    {
        get { return m_updateAnimatorVariables; }
        private set { m_updateAnimatorVariables = value; }
    }

    [Header("Debug")]
    [SerializeField]
    private bool m_debugPreventSlipOfSmallStep = false;
    [SerializeField]
    private bool m_debugAdjustVelocityWhenHittingWallOrCeilling = false;
    [SerializeField]
    private bool m_debugHoppingDownSlopeCorrection = false;
    [SerializeField]
    private bool m_debugMovement = false;

    protected int m_ZVelocityParamHashId = Animator.StringToHash(ZVelocityParamNameString);
    protected int m_XVelocityParamHashId = Animator.StringToHash(XVelocityParamNameString);
    protected int m_YVelocityParamHashId = Animator.StringToHash(YVelocityParamNameString);
    protected int m_lastAirborneYVelocityParamHashId = Animator.StringToHash(LastAirborneYVelocityParamNameString);
    protected int m_slopAngleParamHashId = Animator.StringToHash(SlopeAngleParamNameString);
    protected int m_rotationParamHashId = Animator.StringToHash(RotationParamNameString);
    protected int m_isRunningParamHashId = Animator.StringToHash(IsRunningParamNameString);
    protected int m_isSlipingParamHashId = Animator.StringToHash(IsSlipingParamNameString);
    protected int m_isAirborneParamHashId = Animator.StringToHash(IsAirborneYVelocityParamNameString);
    protected int m_isStaggeredOnLandingParamHashId = Animator.StringToHash(IsStaggeredOnLandingParamNameString);
    protected int m_isMovingParamHashId = Animator.StringToHash(IsMovingParamNameString);
    protected int m_isRotatingParamHashId = Animator.StringToHash(IsRotatingParamNameString);
    
    public const string ZVelocityParamNameString = "ZVelocity";
    public const string XVelocityParamNameString = "XVelocity";
    public const string YVelocityParamNameString = "YVelocity";
    public const string LastAirborneYVelocityParamNameString = "LastAirborneYVelocity";
    public const string SlopeAngleParamNameString = "SlopeAngle";
    public const string RotationParamNameString = "Rotation";
    public const string IsRunningParamNameString = "IsRunning";
    public const string IsSlipingParamNameString = "IsSliping";
    public const string IsAirborneYVelocityParamNameString = "IsAirborne";
    public const string IsStaggeredOnLandingParamNameString = "IsStaggeredOnLanding";
    public const string IsMovingParamNameString = "IsMoving";
    public const string IsRotatingParamNameString = "IsRotating";

	private bool m_addedVelocity;

	protected virtual void Awake()
    {
        m_characterController = GetComponent<CharacterController>();
        m_animator = GetComponent<Animator>();

        IsRunning = false;
        IsExhausted = false;
        IsSliping = false;
        IsAirborne = false;
        IsStaggered = false;

        Stamina = MaxStamina;

        UseRootMotion(false);
    }

    public void FreezeStamina(bool froze)
    {
        IsStaminaFrozen = froze;
    }

    public void UpdateMovement(Inputs inputs, MovementType movementType, Transform lockOnTarget = null)
    {
        // Update rotation
        switch(movementType)
        {
            case MovementType.Strafing:
                if (!IsStaggered)
                {
                    if (lockOnTarget != null)
                    {
                        LookAtTarget(lockOnTarget);
                    }
                    else
                    {
                        RotateWithInput(inputs);
                    }
                }

                break;
            case MovementType.MoveToward:
                if (!IsStaggered)
                {
                    RotateToward(inputs);
                }

                break;
            default:
                Debug.LogError("Unpredicted MovementType: " + movementType);
                break;
        }

        // Update movement
        MoveCharacter(inputs, movementType);
    }

    private void LookAtTarget(Transform target)
    {
        Quaternion previousRotation = transform.rotation;

        Vector3 characterToTarget = target.position - transform.position;
        characterToTarget.y = 0;

        Quaternion rotation = Quaternion.LookRotation(characterToTarget);
        transform.rotation = Quaternion.Slerp(transform.rotation, rotation, Time.deltaTime * m_lockOnRotationSpeed * m_globalModifier);

        m_rotationExecuted = CalculateSignedAngle(previousRotation, transform.rotation);
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

    private void RotateWithInput(Inputs inputs)
    {
        Quaternion previousRotation = transform.rotation;
        float rotY = inputs.xAxis * m_yRotationSpeed * m_globalModifier * Time.deltaTime;

        // Rotate around the global Y axis
        transform.rotation *= Quaternion.AngleAxis(rotY, Vector3.up);
        m_rotationExecuted = CalculateSignedAngle(previousRotation, transform.rotation);
    }

    private void RotateToward(Inputs inputs)
    {
        Vector3 targetDirection = new Vector3(inputs.horizontal, .0f, inputs.vertical).normalized;
        
        if (targetDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection, transform.up);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, m_yRotationSpeed * Time.deltaTime);
        }
    }

    private void MoveCharacter(Inputs inputs, MovementType movementType)
    {
        // The horizontal and vertical inputs are combine together in a Vector2 and adjusted if necessary
		m_currentMovementInput = new Vector2(inputs.horizontal, inputs.vertical);
		if (m_currentMovementInput.magnitude > 1) m_currentMovementInput.Normalize();

		UpdateRunningState(CanRun() && inputs.running);
		UpdateStamina();

		if (!IsStaggered)
        {
            CalculateCurrentModifiers();

			CalculateLocalXVelocity(movementType);
            CalculateLocalYVelocity(CanJump() && inputs.jump);
			CalculateLocalZVelocity(movementType);
        }
        else
        {
            CalculateLocalYVelocity(false);
        }

        Vector3 movement = CalculateGlobalMovement();

        m_currentSlipeVelocity = Vector3.zero;
        m_doesntNeedToSlip = false;

        // The sliping velocity while be calculated immediatly after the character moves
        // OnControllerColliderHit() is called by controller.move()
        MoveCharacterWithVector3(movement * Time.deltaTime);

        // The sliping velocity calculated for this frame is added after all the OnControllerColliderHit() are called
        ApplyCurrentSlipingVelocity();

        // If the character is falling after his movement, it might be because he is on a slope and it's Y velocity is the small compared to it's X and Z velocity.
        // In that case, the character must try to fix this hoping down slopes effect
        // We both test !IsAirborne and !controller.isGrounded, because !IsAirborne will check if the character was in contact with the ground at the previous frame
        // and !controller.isGrounded check if the character is grounded during this frame
        if (!IsAirborne && !m_characterController.isGrounded && movement.y < 0) FixHopingDownSlopeEffect(movement * Time.deltaTime);
		
		if (UseMethodCallOnLanding) CallMethodOnLanding();
        if (UseStaggeredLanding) UpdateStaggeredLanding();

        // The global velocity might need to be transfered to the local velocity if the character just landed or the opposite if
        // the character just left the ground
        TransferVelocityOnLandingAndLeavingGround();
        
        UpdateSlipingState();
        UpdateAirborneState();

        if (UpdateAnimatorVariables) SetAnimatorParams();

        if (m_debugMovement) DrawVector3AtCharacterPos(movement, Color.green, false, true);
    }

    private void UpdateRunningState(bool running)
    {
        if (!IsRunning && running && !IsStaminaFrozen)
        {
            IsRunning = true;

            foreach (IStaminaSubscriber subscriber in m_subscribers)
            {
                subscriber.NotifyStaminaConsomption(gameObject, IsRunning);
            }
        }
        else if (IsRunning && (!running || IsStaminaFrozen))
        {
            IsRunning = false;

            foreach (IStaminaSubscriber subscriber in m_subscribers)
            {
                subscriber.NotifyStaminaConsomption(gameObject, IsRunning);
            }
        }
    }

    private bool CanRun()
    {
        return (m_runningAllowed
                && m_characterController.isGrounded
                && !IsStaggered
				&& Stamina > 0 && !IsExhausted
                && m_currentMovementInput.magnitude >= m_minMovInputForRun);
    }

    private void UpdateStamina()
    {
        if (!IsStaminaFrozen)
        {
            if (IsRunning)
            {
                Stamina -= m_staminaConsuption * Time.deltaTime;

                if (Stamina <= 0)
                {
                    Stamina = 0;
                    IsExhausted = true;
                    StartCoroutine(EndExhaustion());

                    foreach (IStaminaSubscriber subscriber in m_subscribers)
                    {
                        subscriber.NotifyStaminaExhaustion(gameObject, IsExhausted);
                    }
                }

                foreach (IStaminaSubscriber subscriber in m_subscribers)
                {
                    subscriber.NotifyStaminaChange(gameObject, Stamina);
                }
            }
            else
            {
                Stamina += m_staminaRegeneration * Time.deltaTime;

                if (Stamina > MaxStamina) Stamina = MaxStamina;

                foreach (IStaminaSubscriber subscriber in m_subscribers)
                {
                    subscriber.NotifyStaminaChange(gameObject, Stamina);
                }
            }
        }
    }

    // Remove the exhausted state after a certain amoung of seconds
    private IEnumerator EndExhaustion()
    {
        while (Stamina < m_minStaminaToEndExhaustion)
        {
            yield return 0;
        }

        IsExhausted = false;

        foreach (IStaminaSubscriber subscriber in m_subscribers)
        {
            subscriber.NotifyStaminaExhaustion(gameObject, IsExhausted);
        }
    }

    private void CalculateCurrentModifiers()
    {
        Dictionary<string, float> modifiers = new Dictionary<string, float>();

        modifiers.Add("backwardModifier", Mathf.Sign(m_currentMovementInput.y) == -1 ? m_backwardModifier : 1);
        modifiers.Add("sideStepModifier", m_currentMovementInput.x != 0 ? m_sideStepModifier : 1);
        modifiers.Add("airborneModifier", !m_characterController.isGrounded ? m_airborneModifier : 1);
        modifiers.Add("runningModifier", IsRunning ? m_runModifier : 1);

        m_currentModifiers = modifiers;
    }

	private void CalculateLocalXVelocity(MovementType movementType)
    {
        float previousVelocityX = m_localVelocity.x;
        float XUsed = .0f;

        // Adjust values used for velocity calculation
        switch (movementType)
        {
            case MovementType.Strafing:
                XUsed = m_currentMovementInput.x;
                break;
            case MovementType.MoveToward:
                XUsed = .0f;
                break;
            default:
                Debug.LogError("Unpredicted MovementType: " + movementType);
                break;
        }

        float currentMaxVelocityX = m_maxXWalkSpeed * XUsed * m_currentModifiers["sideStepModifier"] * m_currentModifiers["runningModifier"] * m_globalModifier;

        // If the character moves without having achived is maximum horizontal velocity
        if (XUsed != 0 && previousVelocityX != currentMaxVelocityX && (!IsAirborne || m_controlWhileAirborne))
        {
            m_localVelocity.x += Mathf.Abs(XUsed) * m_acceleration * Mathf.Sign(currentMaxVelocityX - m_localVelocity.x) * m_currentModifiers["sideStepModifier"] * m_currentModifiers["airborneModifier"] * m_currentModifiers["runningModifier"] * Time.deltaTime;

			if (VelocityExceedMax(previousVelocityX, m_localVelocity.x, currentMaxVelocityX))
            {
                m_localVelocity.x = currentMaxVelocityX;
            }
        }
        // If the character doesn't want to move, but didn't loose all is horizontal velocity
        else if (XUsed == 0 && m_characterController.isGrounded && previousVelocityX != 0)
        {
            m_localVelocity.x -= m_decceleration * Mathf.Sign(previousVelocityX) * m_currentModifiers["sideStepModifier"] * m_currentModifiers["airborneModifier"] * m_currentModifiers["runningModifier"] * Time.deltaTime;

            if (Mathf.Sign(previousVelocityX) * m_localVelocity.x < 0)
            {
                m_localVelocity.x = 0;
            }
        }
    }

    private void CalculateLocalYVelocity(bool jump)
    {
		if (!m_addedVelocity)
		{
			if (m_characterController.isGrounded)
			{
				m_localVelocity.y = 0;
			}

			if (jump)
			{
				// Apply the jumping mouvement
				m_localVelocity.y = m_jumpStrength;
			}

			// Increments the effect of the gravity on the character fall
			m_localVelocity.y += Physics.gravity.y * m_weightMultiplier * Time.deltaTime;
		}
		else
		{
			m_addedVelocity = false;
		}
    }

    private bool CanJump()
    {
        return (JumpAllowed
                && m_characterController.isGrounded
                && m_globalSlipeVelocity == Vector3.zero);
    }

    private void CalculateLocalZVelocity(MovementType movementType)
    {
        float previousVelocityZ = m_localVelocity.z;
        float ZUsed = .0f;

        // Adjust values used for velocity calculation
        switch (movementType)
        {
            case MovementType.Strafing:
                ZUsed = m_currentMovementInput.y;
                break;
            case MovementType.MoveToward:
                ZUsed = m_currentMovementInput.magnitude;
                break;
            default:
                Debug.LogError("Unpredicted MovementType: " + movementType);
                break;
        }

        float currentMaxVelocityZ = m_maxZWalkSpeed * ZUsed * m_currentModifiers["backwardModifier"] * m_currentModifiers["runningModifier"] * m_globalModifier;

        // If the character moves without having achived is maximum vertical velocity
        if (ZUsed != 0 && previousVelocityZ != currentMaxVelocityZ && (!IsAirborne || m_controlWhileAirborne))
        {
            m_localVelocity.z += Mathf.Abs(ZUsed) * m_acceleration * Mathf.Sign(currentMaxVelocityZ - m_localVelocity.z) * m_currentModifiers["backwardModifier"] * m_currentModifiers["airborneModifier"] * m_currentModifiers["runningModifier"] * Time.deltaTime;

            if (VelocityExceedMax(previousVelocityZ, m_localVelocity.z, currentMaxVelocityZ))
            {
                m_localVelocity.z = currentMaxVelocityZ;
            }
        }
        // If the character doesn't want to move, but didn't loose all is vertical velocity
        else if (ZUsed == 0 && m_characterController.isGrounded && previousVelocityZ != 0)
        {
            m_localVelocity.z -= m_decceleration * Mathf.Sign(previousVelocityZ) * m_currentModifiers["backwardModifier"] * m_currentModifiers["airborneModifier"] * m_currentModifiers["runningModifier"] * Time.deltaTime;

            if (Mathf.Sign(previousVelocityZ) * m_localVelocity.z < 0)
            {
                m_localVelocity.z = 0;
            }
        }
    }

    private bool VelocityExceedMax(float previousVelocity, float velocity, float maxVelocity)
    {
        return ((previousVelocity < maxVelocity && velocity > maxVelocity)
                || (previousVelocity > maxVelocity && velocity < maxVelocity));
    }

    // Add current local velocity to the current global velocity and return the result
    private Vector3 CalculateGlobalMovement()
    {
        Vector3 convertedLocalVelocity = transform.TransformDirection(m_localVelocity);

        if (!m_characterController.isGrounded)
        {
            m_globalVelocity += convertedLocalVelocity;
            m_localVelocity = convertedLocalVelocity = Vector3.zero;

            // Adjusts the global velocity if necessary
            Vector3 globalToLocalVelocity = transform.InverseTransformDirection(m_globalVelocity);

            float maxAirborneXVelocity = m_maxXWalkSpeed * m_sideStepModifier * m_runModifier * m_globalModifier;
            float maxAirborneZVelocity = m_maxZWalkSpeed * m_runModifier * m_globalModifier;
            if (Mathf.Sign(globalToLocalVelocity.z) == -1) maxAirborneZVelocity *= m_backwardModifier;

            if (Mathf.Abs(globalToLocalVelocity.x) > maxAirborneXVelocity)
            {
                globalToLocalVelocity.x = Mathf.Sign(globalToLocalVelocity.x) * maxAirborneXVelocity;
            }

            if (globalToLocalVelocity.y < -m_maxFallingSpeed)
            {
                globalToLocalVelocity.y = -m_maxFallingSpeed;
            }

            if (Mathf.Abs(globalToLocalVelocity.z) > maxAirborneZVelocity)
            {
                globalToLocalVelocity.z = Mathf.Sign(globalToLocalVelocity.z) * maxAirborneZVelocity;
            }

            m_globalVelocity = transform.TransformDirection(globalToLocalVelocity);
        }

        // Part of the velocity must be cancelled out if the character is sliping and trying to go agianst the sliping direction
        if (m_globalSlipeVelocity != Vector3.zero)
        {
            if (m_globalSlipeVelocity.x != 0
                && (Mathf.Sign(convertedLocalVelocity.x) != Mathf.Sign(m_globalSlipeVelocity.x)))
            {
                convertedLocalVelocity -= new Vector3(convertedLocalVelocity.x, 0, 0);
            }

            if (m_globalSlipeVelocity.z != 0
                && (Mathf.Sign(convertedLocalVelocity.z) != Mathf.Sign(m_globalSlipeVelocity.z)))
            {
                convertedLocalVelocity -= new Vector3(0, 0, convertedLocalVelocity.z);
            }

            // The local velocity must be change since the adjustement were made only on the converted velocity
            m_localVelocity = transform.InverseTransformDirection(convertedLocalVelocity);
        }

        // Return the final movement for this frame
        return m_globalVelocity + convertedLocalVelocity + m_globalSlipeVelocity;
    }

    // Move the character base on a Vector3. Vector3 movement is NOT mutiplied by the delta time.
    public void MoveCharacterWithVector3(Vector3 vector)
    {
        m_characterController.Move(vector);
    }

    protected virtual void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!m_doesntNeedToSlip) CalculateSlipingVelocity(hit);

        // Calculate the distance between the base position of the character and the hit position
        CalculateHitDistance(hit.point);

        // Some of the velocity must be cancelled out when the character hit a surface
        if (AdjustVelocityWhenHittingWallOrCeilling) AdjustVelocityWithNormal(hit.normal);
    }

    private void CalculateSlipingVelocity(ControllerColliderHit hit)
    {
        // Calculate the slope angle
        float slopeAngle = (float)System.Math.Round(Vector3.Angle(Vector3.up, hit.normal), 2);
        float slopeLimit = (float)System.Math.Round(m_characterController.slopeLimit, 2);

        bool smallStepPreventedSlip = false;

        // If the character is not suppose to slipe of small step, a ray will be cast to check if the character
        // is on a slope or an edge, since he might be on the edge of a 3D model (for exemple, a stair).
        // This will prevent him from sliping down edges that he might be allow to climb because of his
        // step offset.
        if (PreventSlipeOfSmallStep)
        {
            Vector3 raycastOrigin = hit.point + new Vector3(hit.normal.x, 0, hit.normal.z) * m_preventSlipOfSmallStepRaycastOffset;
            float rayDistance = Mathf.Tan(slopeAngle * Mathf.Deg2Rad) * m_preventSlipOfSmallStepRaycastDistance;

            smallStepPreventedSlip = !Physics.Raycast(raycastOrigin, Vector3.down, rayDistance);

            if (m_debugPreventSlipOfSmallStep) Debug.DrawLine(raycastOrigin, raycastOrigin + Vector3.down * rayDistance, Color.yellow);
        }

        if (!smallStepPreventedSlip && slopeLimit < slopeAngle && slopeAngle < m_maxConsideredSlipAngle)
        {
            // Normal vector projected to the gorund (Y = 0)
            Vector3 groundVector = -(new Vector3(hit.normal.x, 0, hit.normal.z));
            // Rotate the groundVector of 90 degrees around the Y axis
            Vector3 rotationAxis = Quaternion.AngleAxis(90, Vector3.up) * groundVector;
            // Take the groundVector and rotate it by the slope angle and create slipe velocity
            Vector3 calculatedSlipVelocity = -(Quaternion.AngleAxis(-slopeAngle, rotationAxis) * groundVector) * m_slidingAcceleration * Time.deltaTime;

            // Add the new sliping velocity, but always average the result when adding to a previously existing sliping velocity
            if (m_currentSlipeVelocity == Vector3.zero)
            {
                m_currentSlipeVelocity = calculatedSlipVelocity;
            }
            else
            {
                m_currentSlipeVelocity = (m_currentSlipeVelocity + calculatedSlipVelocity) / 2;
            }
        }
        // As soon as a hit indicate the character doens't need to slipe, indicate to stop all calculation and remove all sliping velocity
        else
        {
            m_doesntNeedToSlip = true;
            m_currentSlipeVelocity = Vector3.zero;
        }
    }

    private void CalculateHitDistance(Vector3 hitPos)
    {
        m_lastHitDistance = Vector3.Distance(new Vector3(transform.position.x, transform.position.y - m_characterController.skinWidth, transform.position.z), new Vector3(hitPos.x, transform.position.y - m_characterController.skinWidth, hitPos.z));
    }

    private void AdjustVelocityWithNormal(Vector3 normal)
    {
        // The y component of the normal might by extremely small. It is rounded to prevent
        // some problems that can cause this situation.
        float normalY = (float)System.Math.Round(normal.y, 2);
        
        // Cancel out the Y velocity if the character hit the ceiling
        if (normalY < 0 && (m_localVelocity.y > 0 || m_globalVelocity.y > 0))
		{
			m_localVelocity.y = m_globalVelocity.y = 0;
		}	
        
        // If the character hit vertical surface 
        if (normalY <= 0)
        {
            Vector3 convertedVelocity;

            if (m_globalVelocity != Vector3.zero)
            {
                convertedVelocity = m_globalVelocity;
            }
            else
            {
                convertedVelocity = transform.TransformDirection(m_localVelocity);
            }

            // Project the convertedVelocity to a Vector3 parallel to the wall (or ceilling) 
            Vector3 flattenNormal = new Vector3(normal.x, 0, normal.z);
            Vector3 vectorProjectedTo = Quaternion.AngleAxis(90, Vector3.up) * flattenNormal * Mathf.Sign(Vector3.Cross(normal, convertedVelocity).y);
            Vector3 correctedVelocity = Vector3.Project(convertedVelocity, vectorProjectedTo);
            
            // The velocity must be updated to the newly corrected velocity
            if (m_globalVelocity != Vector3.zero)
            {
                m_globalVelocity.x = correctedVelocity.x;
                m_globalVelocity.z = correctedVelocity.z;
            }
            else
            {
                Vector3 globalCorrectedVelocity = transform.InverseTransformDirection(correctedVelocity);
                m_localVelocity.x = globalCorrectedVelocity.x;
                m_localVelocity.z = globalCorrectedVelocity.z;
            }
            
            if (m_debugAdjustVelocityWhenHittingWallOrCeilling)
            {
                Debug.DrawLine(transform.position, transform.position + convertedVelocity, Color.red);
                Debug.DrawLine(transform.position, transform.position + normal, Color.blue);
                Debug.DrawLine(transform.position, transform.position + vectorProjectedTo, Color.black);
                Debug.DrawLine(transform.position, transform.position + correctedVelocity, Color.yellow);
            }
        }
    }

    private void ApplyCurrentSlipingVelocity()
    {
        if (m_currentSlipeVelocity != Vector3.zero)
        {
            m_globalSlipeVelocity += m_currentSlipeVelocity;

            if (m_globalSlipeVelocity.magnitude > m_maxFallingSpeed)
            {
                m_globalSlipeVelocity = m_globalSlipeVelocity.normalized * m_maxFallingSpeed;
            }
        }
        else
        {
            // globalSlipeVelocity is transfered to the globalVelocity if the charatcer falls after sliping
            if (m_globalSlipeVelocity != Vector3.zero && !m_characterController.isGrounded)
            {
                m_globalVelocity += m_globalSlipeVelocity;
            }

            m_globalSlipeVelocity = Vector3.zero;
        }
    }

    private void CallMethodOnLanding()
    {
        if (IsLandingFromToHigh() && m_methodCalledOnLanding != "")
        {
            gameObject.SendMessage(m_methodCalledOnLanding);   
        }
    }

    private void UpdateStaggeredLanding()
    {
        if (IsLandingFromToHigh())
        {
            if (m_globalSlipeVelocity == Vector3.zero)
            {
                StartStaggered(m_staggeredLandingDuration);
            }
        }
        else if (!m_characterController.isGrounded && IsStaggered)
        {
            StopStaggered();
        }
    }
	
	private bool IsLandingFromToHigh()
	{
	    return (m_characterController.isGrounded
	            && m_globalVelocity != Vector3.zero
	            && m_globalVelocity.y <= -m_minLandingVelocityForMethodAndStagger);
	}

    private void TransferVelocityOnLandingAndLeavingGround()
    {
        if (m_characterController.isGrounded && m_globalVelocity != Vector3.zero)
        {
            m_localVelocity += transform.InverseTransformDirection(m_globalVelocity);
            m_globalVelocity = Vector3.zero;
        }
        else if (!m_characterController.isGrounded && m_localVelocity != Vector3.zero)
        {
            m_globalVelocity += transform.TransformDirection(m_localVelocity);
            m_localVelocity = Vector3.zero;
        }
    }

    public void StartStaggered(float staggeredDuration)
    {
        m_localVelocity = m_globalVelocity = Vector3.zero;

        IsStaggered = true;
        if (UpdateAnimatorVariables) SetAnimatorParams();

        m_staggeredCoroutine = EndStaggered(staggeredDuration);
        StartCoroutine(m_staggeredCoroutine);
    }

    private IEnumerator EndStaggered(float staggerDuration)
    {
        yield return new WaitForSeconds(staggerDuration);

        m_staggeredCoroutine = null;

        IsStaggered = false;
        if (UpdateAnimatorVariables) SetAnimatorParams();
    }

    public void StopStaggered()
    {
        if (m_staggeredCoroutine != null)
        { 
            StopCoroutine(m_staggeredCoroutine);

            m_staggeredCoroutine = null;

            IsStaggered = false;
            if (UpdateAnimatorVariables) SetAnimatorParams();
        }
    }

    private void FixHopingDownSlopeEffect(Vector3 lastMovement)
    {
        Vector3 raycastOrigin = new Vector3(transform.position.x, transform.position.y, transform.position.z);
        float flattenLastMovementMagnitude = new Vector3(lastMovement.x, 0, lastMovement.z).magnitude;
        float minYVelocityInSlope = (Mathf.Tan(m_characterController.slopeLimit * Mathf.Deg2Rad) * flattenLastMovementMagnitude + m_characterController.skinWidth - lastMovement.y) + m_lastHitDistance - lastMovement.y;
        
        // Correct hoping down slopes effect if necessary
        if (Physics.Raycast(raycastOrigin, Vector3.down, minYVelocityInSlope))
        {
            m_characterController.Move(new Vector3(0, -minYVelocityInSlope, 0));
        }

        if (m_debugHoppingDownSlopeCorrection) Debug.DrawLine(raycastOrigin, raycastOrigin - new Vector3(0, minYVelocityInSlope, 0), Color.red);
    }
    
    private void UpdateSlipingState()
    {
        IsSliping = m_globalSlipeVelocity != Vector3.zero;
    }

    private void UpdateAirborneState()
    {
        IsAirborne = !m_characterController.isGrounded && m_globalSlipeVelocity == Vector3.zero;
    }
    
    private void SetAnimatorParams()
    {
        Vector3 totalLocalVelocity = m_localVelocity + transform.InverseTransformDirection(m_globalVelocity) + transform.InverseTransformDirection(m_globalSlipeVelocity);

        float maxXSpeed = m_maxXWalkSpeed * m_sideStepModifier;
        float maxZSpeed = Mathf.Sign(totalLocalVelocity.z) == 1 ? m_maxZWalkSpeed : m_maxZWalkSpeed * m_backwardModifier;

        float XVelocity = Mathf.Abs(totalLocalVelocity.x) / maxXSpeed;
        float ZVelocity = Mathf.Abs(totalLocalVelocity.z) / maxZSpeed;

        if (XVelocity > 1) XVelocity = 1 + (Mathf.Abs(totalLocalVelocity.x) - maxXSpeed)/(maxXSpeed * m_runModifier - maxXSpeed);
        if (ZVelocity > 1) ZVelocity = 1 + (Mathf.Abs(totalLocalVelocity.z) - maxZSpeed)/(maxZSpeed * m_runModifier - maxZSpeed);

        XVelocity *= Mathf.Sign(totalLocalVelocity.x);
        ZVelocity *= Mathf.Sign(totalLocalVelocity.z);

        m_animator.SetFloat(m_XVelocityParamHashId, XVelocity);
        m_animator.SetFloat(m_ZVelocityParamHashId, ZVelocity);
        m_animator.SetFloat(m_YVelocityParamHashId, totalLocalVelocity.y);

        if (!m_characterController.isGrounded) m_animator.SetFloat(m_lastAirborneYVelocityParamHashId, totalLocalVelocity.y);
        m_animator.SetFloat(m_rotationParamHashId, m_rotationExecuted);
        m_animator.SetBool(m_isRotatingParamHashId, m_rotationExecuted != 0.0f);

        float slopeAngle = 0;
        if (m_globalSlipeVelocity != Vector3.zero) slopeAngle = Vector3.Angle(m_globalSlipeVelocity, new Vector3(m_globalSlipeVelocity.x, 0, m_globalSlipeVelocity.z));
        m_animator.SetFloat(m_slopAngleParamHashId, slopeAngle);
        
        m_animator.SetBool(m_isRunningParamHashId, IsRunning);
        m_animator.SetBool(m_isSlipingParamHashId, m_globalSlipeVelocity != Vector3.zero);
        m_animator.SetBool(m_isAirborneParamHashId, !m_characterController.isGrounded);
        m_animator.SetBool(m_isMovingParamHashId, IsMoving());
        m_animator.SetBool(m_isStaggeredOnLandingParamHashId, IsStaggered);
    }

    private bool IsMoving()
    {
        Vector3 totalLocalVelocity = m_localVelocity + transform.InverseTransformDirection(m_globalVelocity) + transform.InverseTransformDirection(m_globalSlipeVelocity);

        return (totalLocalVelocity.x != 0.0f
                || (totalLocalVelocity.y != 0.0f && !m_characterController.isGrounded)
                || totalLocalVelocity.z != 0.0f);
    }

    private void DrawVector3AtCharacterPos(Vector3 vector3, Color color, bool ignoreXaxis = false, bool ignoreYaxis = false, bool ignoreZaxis = false)
    {
        Vector3 finalVector = vector3;

        if (ignoreXaxis) finalVector.x = 0;
        if (ignoreYaxis) finalVector.y = 0;
        if (ignoreZaxis) finalVector.z = 0;

        Debug.DrawLine(transform.position, transform.position + finalVector, color);
    }
    
    public void AddLocalVelocity(Vector3 addedVelocity)
    {
        m_localVelocity = m_localVelocity + addedVelocity;
		m_addedVelocity = true;
    }

    public void AddGlobalVelocity(Vector3 addedVelocity)
    {
		m_localVelocity = m_localVelocity + transform.InverseTransformDirection(addedVelocity);
		m_addedVelocity = true;
    }

    public void NullifyVelocity()
    {
        m_localVelocity = m_globalVelocity = m_globalSlipeVelocity = Vector3.zero;
        if (UpdateAnimatorVariables) SetAnimatorParams();
    }

    public void UseRootMotion(bool use)
    {
        m_animator.applyRootMotion = UsesRootMotion = use;
    }

    public void SetGlobalModifier(float modifier)
    {
        m_globalModifier = modifier;
    }

    // Override the Subscribe method
    public override void Subscribe(IStaminaSubscriber subscriber)
    {
        base.Subscribe(subscriber);
        subscriber.NotifyJustSubscribed(this);
    }
}
