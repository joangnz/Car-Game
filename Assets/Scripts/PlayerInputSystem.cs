using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputSystem : MonoBehaviour
{
    // Components and Children
    private Transform car, camPivot;
    private Rigidbody rb;
    private Camera cam;
    [SerializeField] private Transform FLWheel, FRWheel, RLWheel, RRWheel;

    // Actions
    private InputAction accelerateAction, decelerateAction, steerAction, jumpAction, lookAction, switchCamAction;

    // Action Inputs
    private bool accelerateInput, decelerateInput, jumpInput;
    private Vector2 steerInput = Vector2.zero, lookInput = Vector2.zero;

    // Attributes
    private bool grounded, troubled;

    // Serialized Parameters
    [SerializeField] private float curSpeed = 0f;
    [SerializeField] private float maxSpeed = 90;
    [SerializeField] private float maxNegSpeed = -20f;
    [SerializeField] private float minAbsSpeed = 0.15f;
    [SerializeField] private float airResistance = 0.995f;
    [SerializeField] private float controllerSensitivity = 5f;
    [SerializeField] private float sensitivityMultiplier = 5f;
    [SerializeField] private float maxSteerYaw = 80f;
    [SerializeField] private float maxWheelYaw = 30f;
    [SerializeField] private float groundedDistance = 0.5f;

    [SerializeField] private float troubledUpDistance = 1.5f;
    [SerializeField] private float troubledSideDistance = 1.2f;
    [SerializeField] private float jumpForce = 10000f;

    // Camera Parameters
    private readonly float maxCamYaw = 70f;
    private readonly float maxCamPitch = 30f;
    private readonly float minCamPitch = -10f;
    private readonly float defaultYaw = 0f;
    private readonly float defaultPitch = 20f;
    private float targetYaw = 0f, targetPitch = 0f;
    private bool switchCam = false, camSwitched = false;

    // Defaults
    private readonly Vector3 defaultRotation = Vector3.zero;

    private void Awake()
    {
        car = transform.Find("Car");
        rb = car.GetComponent<Rigidbody>();

        camPivot = car.Find("CamPivot");

        cam = Camera.main;
        cam.transform.parent = camPivot;

        ActionsInit();
    }

    private void ActionsInit()
    {
        accelerateAction = InputSystem.actions.FindAction("Accelerate");
        accelerateAction.performed += OnAccelerate;
        accelerateAction.canceled += OnAccelerate;

        decelerateAction = InputSystem.actions.FindAction("Decelerate");
        decelerateAction.performed += OnDecelerate;
        decelerateAction.canceled += OnDecelerate;

        steerAction = InputSystem.actions.FindAction("Steer");
        steerAction.performed += OnSteer;
        steerAction.canceled += OnSteer;

        jumpAction = InputSystem.actions.FindAction("Jump");
        jumpAction.performed += OnJump;
        jumpAction.canceled += OnJump;

        lookAction = InputSystem.actions.FindAction("Look");
        lookAction.performed += OnLook;
        lookAction.canceled += OnLook;

        switchCamAction = InputSystem.actions.FindAction("SwitchCam");
        switchCamAction.performed += OnSwitchCam;
        switchCamAction.canceled += OnSwitchCam;
    }

    private void OnEnable()
    {
        accelerateAction.Enable();
        decelerateAction.Enable();
        steerAction.Enable();
        jumpAction.Enable();
        lookAction.Enable();
        switchCamAction.Enable();
    }

    private void OnDisable()
    {
        accelerateAction.Disable();
        decelerateAction.Disable();
        steerAction.Disable();
        jumpAction.Disable();
        lookAction.Disable();
        switchCamAction.Disable();
    }

    private void Update()
    {
        // determine state
        grounded = GetGrounded();
        if (!grounded) troubled = GetTroubled();
    }

    void FixedUpdate()
    {
        if (grounded) HandleRotation();
        else HandleAirRotation();
        HandleMovement();

        HandleJump();
    }

    private void LateUpdate()
    {
        HandleCamera();
    }

    // Physics Handlers
    private void HandleRotation()
    {
        // When speed = 0, dont turn
        float speed = Math.Abs(curSpeed);
        if (speed == 0)
            return;

        // Currently, yaw only takes the input and steers
        float yaw = steerInput.x * maxSteerYaw;

        // If speed is really low, steer less. If speed is higher, steer more.
        if (speed < .45f) yaw *= speed;
        // However, at higher speeds, steer less
        else yaw *= 1.3f - speed;

        // invert steering when reversing
        if (curSpeed < 0) yaw *= -1;

        Vector3 v = new(0, yaw, 0);
        Quaternion deltaRotation = Quaternion.Euler(v*Time.fixedDeltaTime);
        rb.MoveRotation(rb.rotation * deltaRotation);
    }

    private void HandleAirRotation()
    {
        float yaw = steerInput.x * maxSteerYaw;
        float pitch = steerInput.y * maxSteerYaw;

        Vector3 v = new(pitch, yaw, 0);
        Quaternion deltaRotation = Quaternion.Euler(v*Time.fixedDeltaTime);
        rb.MoveRotation(rb.rotation * deltaRotation);
    }

    private void HandleMovement()
    {
        Vector3 forward = car.forward;
        forward.y = 0f;
        forward.Normalize();

        // Calculate accelerate or decelerate (resets to 0 if both or neither are pressed) if the car is grounded
        float acceleration = grounded ? ((accelerateInput ? 1f : 0f) - (decelerateInput ? 1f : 0f)) * Time.fixedDeltaTime : 0;

        if (curSpeed < .45f) curSpeed += acceleration;
        else curSpeed += acceleration/2;

        // Air resistance --> slightly decrease acceleration and speed over time
        curSpeed *= airResistance;
        curSpeed = Mathf.Clamp(curSpeed, maxNegSpeed/100, maxSpeed/100);

        // Prevent speeds of <0.5 if not acceleration
        if (Math.Abs(curSpeed) < minAbsSpeed && Mathf.Abs(acceleration) == 0) curSpeed = 0f;

        rb.MovePosition(rb.position + curSpeed*forward);
    }

    private void HandleJump()
    {
        Vector3 forward = car.forward;
        Vector3 up = car.up;
        up.Normalize();
        forward.Normalize();

        if (jumpInput)
        {
            // if grounded jump
            if (grounded)
            {
                Debug.Log("JUMOPING");
                rb.AddForce(up * jumpForce, ForceMode.Impulse);
            } else if (troubled)
            {
                // reset rotation
                //rb.MoveRotation(Quaternion.Euler(defaultRotation));
                //rb.MovePosition(rb.position + up * 2f);
            } else if (false)
            {
                // double jump logic
            }
        }
    }

    private void HandleCamera()
    {
        float sensitivity = controllerSensitivity*Time.deltaTime*sensitivityMultiplier;

        if (lookInput != Vector2.zero)
        {
            // Input between -1 and 1 --> it's literally a percentage of maxCamYaw/Pitch
            targetYaw = lookInput.x * maxCamYaw + (switchCam ? 180f : 0);
            targetPitch = defaultPitch + lookInput.y * maxCamPitch;

            targetPitch = Mathf.Clamp(targetPitch, minCamPitch, maxCamPitch + defaultPitch);
        }
        else
        {
            // Smoothly return to default
            targetYaw = Mathf.Lerp(targetYaw, defaultYaw + (switchCam ? 180f : 0), sensitivity);
            targetPitch = Mathf.Lerp(targetPitch, defaultPitch, sensitivity);

            if (Mathf.Abs(targetYaw) < 0.01f) targetYaw = 0f;
            if (Mathf.Abs(targetPitch) < 0.01f) targetPitch = 0f;
        }

        if (switchCam)
        {
            if (!camSwitched)
            {
                camSwitched = true;

                camPivot.localRotation = Quaternion.Euler(
                    camPivot.localRotation.eulerAngles.x,
                    camPivot.localRotation.eulerAngles.y + 180f,
                    0);
            }
        }
        else if (camSwitched)
        {
            camSwitched = false;

            camPivot.localRotation = Quaternion.Euler(
                camPivot.localRotation.eulerAngles.x,
                camPivot.localRotation.eulerAngles.y - 180f,
                0);
        }

        Quaternion targetRotation = Quaternion.Euler(targetPitch, targetYaw, 0f);

        camPivot.localRotation = Quaternion.Slerp(
            camPivot.localRotation,
            targetRotation,
            sensitivity
            );

        // 0 Z rotation
        camPivot.localRotation = Quaternion.Euler(
            camPivot.localRotation.eulerAngles.x,
            camPivot.localRotation.eulerAngles.y,
            0f);
    }

    // Methods
    private bool GetGrounded()
    {
        bool fr, fl, rr, rl, gr = false;

        fr = Physics.Raycast(FRWheel.position, -FRWheel.up, groundedDistance);
        fl = Physics.Raycast(FLWheel.position, -FRWheel.up, groundedDistance);
        rr = Physics.Raycast(RRWheel.position, -RRWheel.up, groundedDistance);
        rl = Physics.Raycast(RLWheel.position, -RLWheel.up, groundedDistance);

        Debug.DrawRay(FRWheel.position, -FRWheel.up * groundedDistance, Color.red);
        Debug.DrawRay(FLWheel.position, -FLWheel.up * groundedDistance, Color.red);
        Debug.DrawRay(RRWheel.position, -RRWheel.up * groundedDistance, Color.red);
        Debug.DrawRay(RLWheel.position, -RLWheel.up * groundedDistance, Color.red);

        if (fr && fl && rr && rl)
        {
            gr = true;
            // Maybe reset forces and rotation?
            car.position.Normalize();
        }

        return gr;
    }

    private bool GetTroubled()
    {
        bool troubled = false;

        Debug.DrawRay(car.position, car.up * troubledUpDistance, Color.blue);
        Debug.DrawRay(car.position, car.right * troubledSideDistance, Color.blue);
        Debug.DrawRay(car.position, -car.right * troubledSideDistance, Color.blue);

        if (Physics.Raycast(car.position, car.up, troubledUpDistance) ||
            Physics.Raycast(car.position, car.right, troubledSideDistance) ||
            Physics.Raycast(car.position, -car.right, troubledSideDistance))
            troubled = true;

        if (troubled) Debug.Log("TROUBLED");
        return troubled;
    }

    // Action Handlers
    private void OnAccelerate(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
        {
            accelerateInput = true;
        }
        else if (ctx.canceled)
        {
            accelerateInput = false;
        }
    }

    private void OnDecelerate(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
        {
            decelerateInput = true;
        }
        else if (ctx.canceled)
        {
            decelerateInput = false;
        }
    }

    private void OnSteer(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
        {
            steerInput = ctx.ReadValue<Vector2>();
        }
        else if (ctx.canceled)
        {
            steerInput = Vector2.zero;
        }
    }

    private void OnJump(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
        {
            jumpInput = true;
        }
        if (ctx.canceled)
        {
            jumpInput = false;
        }
    }

    private void OnLook(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
        {
            lookInput = ctx.ReadValue<Vector2>();
        }
        else if (ctx.canceled)
        {
            lookInput = Vector2.zero;
        }
    }

    private void OnSwitchCam(InputAction.CallbackContext ctx)
    {
        if (ctx.started || ctx.performed)
        {
            switchCam = true;
            targetYaw += 180f;
        }
        else if (ctx.canceled)
        {
            switchCam = false;
            targetYaw -= 180f;
        }
    }
}
