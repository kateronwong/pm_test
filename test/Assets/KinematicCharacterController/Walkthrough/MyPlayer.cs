using KinematicCharacterController.Examples;
using System.Linq;
using UnityEngine;

public class MyPlayer : MonoBehaviour
{
    //camera var
    public ExampleCharacterCamera OrbitCamera;
    public Transform CameraFollowPoint;
    public MyCharacterController Character;
    private Vector3 _lookInputVector = Vector3.zero;

    //character var
    private const string MouseXInput = "Mouse X";
    private const string MouseYInput = "Mouse Y";
    private const string MouseScrollInput = "Mouse ScrollWheel";
    private const string HorizontalInput = "Horizontal";
    private const string VerticalInput = "Vertical";

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;

        // Tell camera to follow transform
        OrbitCamera.SetFollowTransform(CameraFollowPoint);

        // Ignore the character's collider(s) for camera obstruction checks
        OrbitCamera.IgnoredColliders = Character.GetComponentsInChildren<Collider>().ToList();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Cursor.lockState = CursorLockMode.Locked;
        }

        HandleCharacterInput();
    }

    private void LateUpdate()
    {
        HandleCameraInput();
    }

    private void HandleCameraInput()
    {
        // Create the look input vector for the camera
        float mouseLookAxisUp = Input.GetAxisRaw("Mouse Y");
        float mouseLookAxisRight = Input.GetAxisRaw("Mouse X");
        _lookInputVector = new Vector3(mouseLookAxisRight, mouseLookAxisUp, 0f);

        // Prevent moving the camera while the cursor isn't locked
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            _lookInputVector = Vector3.zero;
        }

        // Input for zooming the camera (disabled in WebGL because it can cause problems)
        float scrollInput = -Input.GetAxis("Mouse ScrollWheel");

        // Apply inputs to the camera
        OrbitCamera.UpdateWithInput(Time.deltaTime, scrollInput, _lookInputVector);

        // Handle toggling zoom level
        if (Input.GetMouseButtonDown(1))
        {
            OrbitCamera.TargetDistance = (OrbitCamera.TargetDistance == 0f) ? OrbitCamera.DefaultDistance : 0f;
        }
    }

    private void HandleCharacterInput()
    {
        PlayerCharacterInputs characterInputs = new PlayerCharacterInputs();

        // Build the CharacterInputs struct
        characterInputs.MoveAxisForward = Input.GetAxisRaw(VerticalInput);
        characterInputs.MoveAxisRight = Input.GetAxisRaw(HorizontalInput);
        characterInputs.CameraRotation = OrbitCamera.Transform.rotation;
        characterInputs.JumpDown = Input.GetKeyDown(KeyCode.Space);

        // Apply inputs to character
        Character.SetInputs(ref characterInputs);

        // Apply impulse
        if (Input.GetKeyDown(KeyCode.Q))
        {
            Character.Motor.ForceUnground(0.1f);
            Character.AddVelocity(Vector3.one * 10f);
        }

    }
}
