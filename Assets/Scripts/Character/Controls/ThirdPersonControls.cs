using UnityEngine;

// This script requires thoses components and will be added if they aren't already there
[RequireComponent(typeof(CharacterControllerMovement))]

public class ThirdPersonControls : MonoBehaviour
{
    [Header("Controls")]
    [SerializeField]
    private bool m_useKeyboard;

    private Inputs noControlInputs;

    public bool ControlsEnabled { get; private set; }

    private FreeLookCameraControl m_cameraControl;
    private CharacterControllerMovement m_movement;

    private void Awake()
    {
        noControlInputs = new Inputs();

        ControlsEnabled = true;

        m_cameraControl = GetComponentInChildren<FreeLookCameraControl>();
        m_movement = GetComponent<CharacterControllerMovement>();

        if (!m_cameraControl)
        {
            Debug.LogError("No FreeLookCameraControl found in childrens");
        }
    }

    private void Update()
    {
        // Only update when time isn't stop
        if (Time.deltaTime > 0.0f)
        {
            // Get the inputs used during this frame
            Inputs inputs = FetchInputs();

            if (ControlsCharacter())
            {
                // Movement and camera update
                if (!OnPreventMovementControlCheck())
                {
					UpdateCamera(inputs, false);
                    UpdateMovement(inputs, false);
                }
                else
                {
					UpdateCamera(noControlInputs, false);
                    UpdateMovement(noControlInputs, false);
                }
            }
            else
            {
				UpdateCamera(noControlInputs, false);
                UpdateMovement(noControlInputs, false);
            }
        }
    }

	// TODO: Get inputs
    private Inputs FetchInputs()
    {
        Inputs inputs = new Inputs();
        
	    if (m_useKeyboard)
	    {
            // Inputs from the keyboard
            inputs.vertical = Input.GetAxisRaw("Vertical");
            inputs.horizontal = Input.GetAxisRaw("Horizontal");
            inputs.running = Input.GetButton("Run");
            inputs.jump = Input.GetButtonDown("Jump");
            inputs.xAxis = Input.GetAxis("Mouse X");
            inputs.yAxis = Input.GetAxis("Mouse Y");
        }
	    else
	    {
            // Inputs grom the
            inputs.vertical = Input.GetAxisRaw("Controller Vertical");
            inputs.horizontal = Input.GetAxisRaw("Controller Horizontal");
            inputs.running = Input.GetButton("Controller Run");
            inputs.jump = Input.GetButtonDown("Controller Jump");
            inputs.xAxis = Input.GetAxis("Right Stick X");
            inputs.yAxis = Input.GetAxis("Right Stick Y");
        }

        return inputs;
	}

    public void SetKeyboardUse(bool useKeyboard)
    {
        m_useKeyboard = useKeyboard;
    }

    private bool ControlsCharacter()
    {
        return ControlsEnabled;
    }

    private bool OnPreventMovementControlCheck()
    {
        return false;
    }

    private void UpdateCamera(Inputs inputs, bool lockOn)
    {
        m_cameraControl.RotateCamera(inputs, FreelookRotationType.RotateOnXYAxis);
    }

    private void UpdateMovement(Inputs inputs, bool lockOn)
    {
        m_movement.UpdateMovement(ConvertMovementInputsRelatifToView(inputs), MovementType.MoveToward);
    }
    
    private Inputs ConvertMovementInputsRelatifToView(Inputs inputs)
    {
        Vector3 originalInput = new Vector3(inputs.horizontal, .0f, inputs.vertical);
        Vector3 convertedInput = m_cameraControl.ControledCamera.transform.TransformDirection(originalInput).normalized;
        convertedInput = new Vector3(convertedInput.x, .0f, convertedInput.z) * originalInput.magnitude;

        inputs.horizontal = convertedInput.x;
        inputs.vertical = convertedInput.z;

        return inputs;
    }

    public void EnableControl(bool enable)
    {
        ControlsEnabled = enable;
    }
}
