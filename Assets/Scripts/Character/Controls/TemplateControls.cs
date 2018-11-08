using UnityEngine;

// This script requires thoses components and will be added if they aren't already there
//[RequireComponent(typeof(RBCharacterMovement))]

public class TemplateControls : MonoBehaviour
{
    [Header("Controls")]
    [SerializeField]
    private bool m_useKeyboard;

    private Inputs noControlInputs;

	// // Use to detect on triggers states
    //private bool m_wasLeftTriggerInputDown = false;
    //private bool m_wasRightTriggerInputDown = false;

    public bool ControlsEnabled { get; private set; }
    
    private void Awake()
    {
        noControlInputs = new Inputs();

        ControlsEnabled = true;

        // TODO: Get components here

		// TODO: Log error message for components that might not be found
        /*if (!m_cameraControl)
        {
            Debug.LogError("No FPSCameraMovement found on childrens");
        }*/
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
					// TODO: Update scripts with inputs
                    /*UpdateCamera(inputs, inputs.lockOn);
                    UpdateMovement(inputs, inputs.lockOn);*/
                }
                else
                {
					// TODO: Update scripts using the noControlInputs
                    /*UpdateCamera(noControlInputs, noControlInputs.lockOn);
                    UpdateMovement(noControlInputs, noControlInputs.lockOn);*/
                }
            }
            else
            {
				// TODO: Update scripts using the noControlInputs
                /*UpdateCamera(noControlInputs, noControlInputs.lockOn);
                UpdateMovement(noControlInputs, noControlInputs.lockOn);*/
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
            /*inputs.vertical = Input.GetAxisRaw("Vertical");
            inputs.horizontal = Input.GetAxisRaw("Horizontal");
            inputs.running = Input.GetButton("Run");
            inputs.jump = Input.GetButtonDown("Jump");
            inputs.xAxis = Input.GetAxis("Mouse X");
            inputs.yAxis = Input.GetAxis("Mouse Y");

            inputs.lockOn = Input.GetButton("Lock");

            inputs.previousPower = Input.GetKeyDown("z");
            inputs.nextPower = Input.GetKeyDown("c");*/
			
			// Use to detect on triggers states
            /*inputs.leftTriggerDown = Input.GetKeyDown("q");
            inputs.rightTriggerDown = Input.GetKeyDown("e");

            inputs.leftTriggerUp = Input.GetKeyUp("q");
            inputs.rightTriggerUp = Input.GetKeyUp("e");

            inputs.holdLeftTrigger = Input.GetKey("q");
            inputs.holdRightTrigger = Input.GetKey("e");*/

            //inputs.cancelPower = Input.GetKey("x");
        }
	    else
	    {
            // Inputs grom the
            /*inputs.vertical = Input.GetAxisRaw("Controller Vertical");
            inputs.horizontal = Input.GetAxisRaw("Controller Horizontal");
            inputs.running = Input.GetButton("Controller Run");
            inputs.jump = Input.GetButtonDown("Controller Jump");
            inputs.xAxis = Input.GetAxis("Right Stick X");
            inputs.yAxis = Input.GetAxis("Right Stick Y");

            inputs.lockOn = Input.GetButton("Controller Lock");

            inputs.previousPower = Input.GetButtonDown("Left Bumper");
            inputs.nextPower = Input.GetButtonDown("Right Bumper");*/
			
			// Use to detect on triggers states
            /*inputs.leftTriggerDown = !m_wasLeftTriggerInputDown && Input.GetAxis("Left Trigger") == 1.0f;
            inputs.rightTriggerDown = !m_wasRightTriggerInputDown && Input.GetAxis("Right Trigger") == 1.0f;
            
            inputs.leftTriggerUp = m_wasLeftTriggerInputDown && Input.GetAxis("Left Trigger") != 1.0f;
            inputs.rightTriggerUp = m_wasRightTriggerInputDown && Input.GetAxis("Right Trigger") != 1.0f;
            
            inputs.holdLeftTrigger = m_wasLeftTriggerInputDown = Input.GetAxis("Left Trigger") == 1.0f;
            inputs.holdRightTrigger = m_wasRightTriggerInputDown = Input.GetAxis("Right Trigger") == 1.0f;*/
			
            //inputs.cancelPower = Input.GetButtonDown("Fire2");
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
	
	// TODO: Create an update method for each script called
    /*private void UpdateMovement(Inputs inputs, bool lockOn)
    {
        m_movementScript.MoveCharacter(inputs, lockOn ? m_cameraControl.TargetLockedOn : null);
    }*/
    
    public void EnableControl(bool enable)
    {
        ControlsEnabled = enable;
    }
}
