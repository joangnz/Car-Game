using Fusion;
using Fusion.Addons.Physics;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum TorqueState
{
    Front,
    Rear,
    Stopped
}

public class Wheel
{
    public Wheel(Transform t, WheelCollider c)
    {
        this.Transform = t;
        this.Collider = c;
    }

    public Transform Transform { get; set; }
    public WheelCollider Collider { get; set; }
}

public class PlayerInputSystem : NetworkBehaviour
{
    // Components and Children
    [Header("Components and Children")]
    private Transform camPivot;
    private Rigidbody rb;
    private NetworkRigidbody3D nrb;
    private Camera cam;
    [SerializeField] private Transform FLWheel, FRWheel, RLWheel, RRWheel;
    [SerializeField] private WheelCollider FLCol, FRCol, RLCol, RRCol;

    // Actions
    //private InputAction accelerateAction, decelerateAction, steerAction, jumpAction, lookAction, switchCamAction;

    // Action Inputs
    [Header("Sensitivity")]
    private bool accelerateInput, decelerateInput, jumpInput, switchCamInput = false;
    private Vector2 steerInput = Vector2.zero, lookInput = Vector2.zero;
    [SerializeField] private float controllerSensitivity = 5f;
    [SerializeField] private float sensitivityMultiplier = 5f;

    // Attributes
    [Header("Car Attributes")]
    private bool grounded, troubled;
    [SerializeField] private TorqueState torqueState = TorqueState.Front;

    // Serialized Parameters
    [Header("Torque")]
    //[SerializeField] private float curTorque = 0f;
    [SerializeField] private float maxTorque = 500f;
    [SerializeField] private float minTorque = -500f;
    [SerializeField] private float brakeForce = 15000f;
    [SerializeField] private float torqueThreshold = 30f;
    [SerializeField] private float maxSteerYaw = 30f;
    [SerializeField] private float groundedDistance = 0.5f;

    [SerializeField] private float curTorque = 0f;
    [SerializeField] private int curBrake = 0;

    [Header("Jump or Troubled")]
    [SerializeField] private float troubledUpDistance = 1.5f;
    [SerializeField] private float troubledSideDistance = 1.2f;
    [SerializeField] private float jumpForce = 10000f;

    // Camera Parameters
    private readonly Vector3 camPos = new(0, 1, -10);
    private readonly float maxCamYaw = 70f;
    private readonly float maxCamPitch = 30f;
    private readonly float minCamPitch = -10f;
    private readonly float defaultYaw = 0f;
    private readonly float defaultPitch = 20f;
    public float targetYaw = 0f, targetPitch = 0f;
    private bool camSwitched = false;

    // Wheels
    [Header("Wheels")]
    [SerializeField] private float forwardStiffness = 1f;
    [SerializeField] private float forwardES = 0;
    [SerializeField] private float forwardEV = 1.2f;
    [SerializeField] private float forwardAS = .5f;
    [SerializeField] private float forwardAV = .8f;
    [SerializeField] private float sideStiffness = 1f;
    [SerializeField] private float sideES = .3f;
    [SerializeField] private float sideEV = 1.1f;
    [SerializeField] private float sideAS = .6f;
    [SerializeField] private float sideAV = .7f;
    private readonly List<Wheel> Wheels = new();
    private readonly List<Wheel> FrontWheels = new();
    private readonly List<Wheel> RearWheels = new();

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        nrb = GetComponent<NetworkRigidbody3D>();

        camPivot = transform.Find("CamPivot");

        //cam = FindFirstObjectByType<Camera>();
        //cam.transform.parent = camPivot;

        //ActionsInit();

        WheelsInit();
    }

    public override void Spawned()
    {
        if (Object.HasInputAuthority)
        {
            cam = FindFirstObjectByType<Camera>();
            cam.transform.parent = camPivot;
            cam.transform.localPosition = camPos;
        }
    }

    //private void ActionsInit()
    //{
    //    accelerateAction = InputSystem.actions.FindAction("Accelerate");
    //    accelerateAction.performed += OnAccelerate;
    //    accelerateAction.canceled += OnAccelerate;

    //    decelerateAction = InputSystem.actions.FindAction("Decelerate");
    //    decelerateAction.performed += OnDecelerate;
    //    decelerateAction.canceled += OnDecelerate;

    //    steerAction = InputSystem.actions.FindAction("Steer");
    //    steerAction.performed += OnSteer;
    //    steerAction.canceled += OnSteer;

    //    jumpAction = InputSystem.actions.FindAction("Jump");
    //    jumpAction.performed += OnJump;
    //    jumpAction.canceled += OnJump;

    //    lookAction = InputSystem.actions.FindAction("Look");
    //    lookAction.performed += OnLook;
    //    lookAction.canceled += OnLook;

    //    switchCamAction = InputSystem.actions.FindAction("SwitchCam");
    //    switchCamAction.performed += OnSwitchCam;
    //    switchCamAction.canceled += OnSwitchCam;
    //}

    private void WheelsInit()
    {
        Wheels.Add(new(FLWheel, FLCol));
        Wheels.Add(new(FRWheel, FRCol));
        Wheels.Add(new(RLWheel, RLCol));
        Wheels.Add(new(RRWheel, RRCol));

        FrontWheels.Add(new(FLWheel, FLCol));
        FrontWheels.Add(new(FRWheel, FRCol));

        RearWheels.Add(new(RLWheel, RLCol));
        RearWheels.Add(new(RRWheel, RRCol));

        foreach (Wheel wheel in Wheels)
        {
            WheelFrictionCurve f = wheel.Collider.forwardFriction;
            f.stiffness = forwardStiffness;
            f.extremumSlip = 0.15f;
            f.extremumValue = 1.2f;
            f.asymptoteSlip = 0.5f;
            f.asymptoteValue= 0.8f;
            f.stiffness     = 1.4f;
            wheel.Collider.forwardFriction = f;

            WheelFrictionCurve s = wheel.Collider.sidewaysFriction;
            s.stiffness = sideStiffness;
            s.extremumSlip  = 0.3f;
            s.extremumValue = 1.1f;
            s.asymptoteSlip = 0.6f;
            s.asymptoteValue= 0.7f;
            wheel.Collider.sidewaysFriction = s;
        }
    }

    //private void OnEnable()
    //{
    //    accelerateAction.Enable();
    //    decelerateAction.Enable();
    //    steerAction.Enable();
    //    jumpAction.Enable();
    //    lookAction.Enable();
    //    switchCamAction.Enable();
    //}

    //private void OnDisable()
    //{
    //    accelerateAction.Disable();
    //    decelerateAction.Disable();
    //    steerAction.Disable();
    //    jumpAction.Disable();
    //    lookAction.Disable();
    //    switchCamAction.Disable();
    //}

    private void Update()
    {
        // determine state
        //torqueState = GetTorqueState();

        //grounded = GetGrounded();
        //if (!grounded) troubled = GetTroubled();
    }

    void FixedUpdate()
    {
        //if (grounded) HandleRotation();
        //else HandleAirRotation(troubled);

        //HandleAcceleration();

        //HandleJump();
        //UpdateWheels();
    }

    public override void FixedUpdateNetwork()
    {
        torqueState = GetTorqueState();

        grounded = GetGrounded();
        if (!grounded) troubled = GetTroubled();

        if (GetInput(out NetworkInputData input))
        {
            if (switchCamInput != input.switchCamInput) targetYaw += switchCamInput ? 180 : -180;
            accelerateInput = input.accelerateInput;
            decelerateInput = input.decelerateInput;
            steerInput = input.steerInput;  
            jumpInput = input.jumpInput;
            lookInput = input.lookInput;
            switchCamInput = input.switchCamInput;
            Debug.Log("Accelerate: " + accelerateInput);
            Debug.Log("Decelerate: " + decelerateInput);
            Debug.Log("Steer: " +  steerInput);
            Debug.Log("Jump: " + jumpInput);
            Debug.Log("Look: " + lookInput);
            Debug.Log("Switch Cam: " +  switchCamInput);
        }

        if (grounded) HandleRotation();
        else HandleAirRotation(troubled);

        HandleAcceleration();

        HandleJump();
        UpdateWheels();
    }

    private void LateUpdate()
    {
        HandleCamera();
    }

    private void UpdateWheels()
    {
        foreach (Wheel wheel in Wheels)
        {
            UpdateWheelPose(wheel.Transform, wheel.Collider);
        }
    }

    private void UpdateWheelPose(Transform tr, WheelCollider col)
    {
        col.GetWorldPose(out Vector3 pos, out Quaternion rot);
        tr.SetPositionAndRotation(pos, rot);
    }

    // Physics Handlers
    private void HandleRotation()
    {
        // When speed = 0, dont turn
        float speed = rb.linearVelocity.magnitude;
        if (speed < Mathf.Epsilon)
            return;

        // Currently, yaw only takes the input and steers
        float yaw = -steerInput.x * maxSteerYaw;

        // If speed is really low, steer less. If speed is higher, steer more.
        if (speed < 70) yaw *= speed;
        // However, at higher speeds, steer less
        else yaw *= 1.3f - speed;

        // invert steering when reversing
        if (speed < 0) yaw *= -1;

        yaw = Mathf.Clamp(-yaw, -maxSteerYaw, maxSteerYaw);


        foreach (Wheel wheel in FrontWheels)
        {
            wheel.Collider.steerAngle = Mathf.Lerp(wheel.Collider.steerAngle, yaw, .07f);
        }
    }

    private void HandleAirRotation(bool troubled = false)
    {
        float yaw = steerInput.x * maxSteerYaw;
        float pitch = troubled ? 0 : steerInput.y * maxSteerYaw;

        Vector3 v = new(pitch, yaw, 0);
        Quaternion deltaRotation = Quaternion.Euler(v*Time.fixedDeltaTime);
        rb.MoveRotation(rb.rotation * deltaRotation);
    }

    private void HandleAcceleration()
    {
        float forwardSpeed = Vector3.Dot(transform.forward, rb.linearVelocity);
        float desiredTorque = 0f;

        if (accelerateInput)
        {
            if (forwardSpeed < -0.5f) HandleBrake(true);
            else desiredTorque = maxTorque;
        }
        else if (decelerateInput)
        {
            if (forwardSpeed > 0.5f) HandleBrake(true);
            else desiredTorque = minTorque;
        }
        else HandleBrake(false);

        foreach (Wheel wheel in RearWheels)
        {
            wheel.Collider.motorTorque = desiredTorque;

            if (Mathf.Abs(wheel.Collider.motorTorque) < torqueThreshold &&
                !(accelerateInput || decelerateInput)
                ) wheel.Collider.motorTorque = 0;

            curTorque = wheel.Collider.motorTorque;
        }
    }

    private void HandleBrake(bool b)
    {
        if (b) RLCol.brakeTorque = RRCol.brakeTorque = FLCol.brakeTorque = FRCol.brakeTorque = brakeForce;
        else RLCol.brakeTorque = RRCol.brakeTorque = FLCol.brakeTorque = FRCol.brakeTorque = 0;

        curBrake = b ? (int)brakeForce : 0;
    }

    private void HandleDrift(bool b)
    {
        if (b) RLCol.brakeTorque = RRCol.brakeTorque = FLCol.brakeTorque = FRCol.brakeTorque = brakeForce;
        else RLCol.brakeTorque = RRCol.brakeTorque = FLCol.brakeTorque = FRCol.brakeTorque = 0;
    }

    private void HandleJump()
    {
        Vector3 forward = transform.forward;
        Vector3 up = transform.up;
        up.Normalize();
        forward.Normalize();

        if (jumpInput)
        {
            // if grounded jump
            if (grounded)
            {
                rb.AddForce(up * jumpForce, ForceMode.Impulse);
            }
            else if (troubled)
            {
                Quaternion desiredRot = Quaternion.Lerp(rb.rotation, Quaternion.Euler(0, 0, 0), 0.1f);
                rb.MoveRotation(desiredRot);
            }
        }
    }

    private void HandleCamera()
    {
        camPivot.rotation = Quaternion.Euler(
            camPivot.rotation.eulerAngles.x,
            camPivot.rotation.eulerAngles.y,
            0);

        float sensitivity = controllerSensitivity*Time.deltaTime*sensitivityMultiplier;

        if (lookInput != Vector2.zero)
        {
            // Input between -1 and 1 --> it's literally a percentage of maxCamYaw/Pitch
            targetYaw = lookInput.x * maxCamYaw + (switchCamInput ? 180f : 0);
            targetPitch = defaultPitch + lookInput.y * maxCamPitch;

            targetPitch = Mathf.Clamp(targetPitch, minCamPitch, maxCamPitch + defaultPitch);
        }
        else
        {
            // Smoothly return to default
            targetYaw = Mathf.Lerp(targetYaw, defaultYaw + (switchCamInput ? 180f : 0), sensitivity);
            targetPitch = Mathf.Lerp(targetPitch, defaultPitch, sensitivity);

            if (Mathf.Abs(targetYaw) < 0.01f) targetYaw = 0f;
            if (Mathf.Abs(targetPitch) < 0.01f) targetPitch = 0f;
        }

        if (switchCamInput)
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
    private TorqueState GetTorqueState()
    {
        float forwardSpeed = Vector3.Dot(transform.forward, rb.linearVelocity);
        if (forwardSpeed < 0f) return TorqueState.Rear;
        else if (Mathf.Abs(forwardSpeed) < 0.01f) return TorqueState.Stopped;
        else return TorqueState.Front;
    }

    private bool GetGrounded()
    {
        bool gr = false;

        bool fr = Physics.Raycast(FRWheel.position, -transform.up, groundedDistance);
        bool fl = Physics.Raycast(FLWheel.position, -transform.up, groundedDistance);
        bool rr = Physics.Raycast(RRWheel.position, -transform.up, groundedDistance);
        bool rl = Physics.Raycast(RLWheel.position, -transform.up, groundedDistance);

        Debug.DrawRay(FRWheel.position, -transform.up * groundedDistance, Color.red);
        Debug.DrawRay(FLWheel.position, -transform.up * groundedDistance, Color.red);
        Debug.DrawRay(RRWheel.position, -transform.up * groundedDistance, Color.red);
        Debug.DrawRay(RLWheel.position, -transform.up * groundedDistance, Color.red);

        if (fr && fl && rr && rl)
        {
            gr = true;
            // Maybe reset forces and rotation?
            transform.position.Normalize();
        }

        return gr;
    }

    private bool GetTroubled()
    {
        bool troubled = false;

        Debug.DrawRay(transform.position, transform.up * troubledUpDistance, Color.blue);
        Debug.DrawRay(transform.position, transform.right * troubledSideDistance, Color.blue);
        Debug.DrawRay(transform.position, -transform.right * troubledSideDistance, Color.blue);

        if (Physics.Raycast(transform.position, transform.up, troubledUpDistance) ||
            Physics.Raycast(transform.position, transform.right, troubledSideDistance) ||
            Physics.Raycast(transform.position, -transform.right, troubledSideDistance))
            troubled = true;

        return troubled;
    }

    // Action Handlers
    //private void OnAccelerate(InputAction.CallbackContext ctx)
    //{
    //    if (ctx.performed)
    //    {
    //        accelerateInput = true;
    //    }
    //    else if (ctx.canceled)
    //    {
    //        accelerateInput = false;
    //    }
    //}

    //private void OnDecelerate(InputAction.CallbackContext ctx)
    //{
    //    if (ctx.performed)
    //    {
    //        decelerateInput = true;
    //    }
    //    else if (ctx.canceled)
    //    {
    //        decelerateInput = false;
    //    }
    //}

    //private void OnSteer(InputAction.CallbackContext ctx)
    //{
    //    if (ctx.performed)
    //    {
    //        steerInput = ctx.ReadValue<Vector2>();
    //    }
    //    else if (ctx.canceled)
    //    {
    //        steerInput = Vector2.zero;
    //    }
    //}

    //private void OnJump(InputAction.CallbackContext ctx)
    //{
    //    if (ctx.performed)
    //    {
    //        jumpInput = true;
    //    }
    //    if (ctx.canceled)
    //    {
    //        jumpInput = false;
    //    }
    //}

    //private void OnLook(InputAction.CallbackContext ctx)
    //{
    //    if (ctx.performed)
    //    {
    //        lookInput = ctx.ReadValue<Vector2>();
    //    }
    //    else if (ctx.canceled)
    //    {
    //        lookInput = Vector2.zero;
    //    }
    //}

    //private void OnSwitchCam(InputAction.CallbackContext ctx)
    //{
    //    if (ctx.started || ctx.performed)
    //    {
    //        switchCamInput = true;
    //        targetYaw += 180f;
    //    }
    //    else if (ctx.canceled)
    //    {
    //        switchCamInput = false;
    //        targetYaw -= 180f;
    //    }
    //}
}
