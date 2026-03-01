using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
    [Header("Components and Children")]
    private Transform camPivot;
    private Rigidbody rb;
    private Camera cam;
    [SerializeField] private Transform FLWheel, FRWheel, RLWheel, RRWheel;
    [SerializeField] private WheelCollider FLCol, FRCol, RLCol, RRCol;

    // Ground Layer
    private LayerMask groundLayer;

    [Header("Sensitivity")]
    private bool accelerateInput, decelerateInput, jumpInput, switchCamInput = false;
    private Vector2 steerInput = Vector2.zero, lookInput = Vector2.zero;
    private readonly float controllerSensitivity = 5f;
    private readonly float sensitivityMultiplier = 5f;

    [Header("Car Attributes")]
    private bool grounded, troubled;

    [Header("Torque")]
    private readonly float maxTorque = 500f;
    private readonly float minTorque = -500f;
    private readonly float brakeForce = 15000f;
    private readonly float torqueThreshold = 30f;
    private readonly float maxSteerYaw = 30f;
    private readonly float aerialTorqueMultiplier = 3000f;
    private readonly float maxAerialAngularVelocity = 1f;
    private readonly float groundedDistance = 0.5f;

    [Header("Jump or Troubled")]
    private readonly float troubledUpDistance = 1.5f;
    private readonly float troubledSideDistance = 1.2f;
    private readonly float jumpForce = 10000f;
    private bool jumping = false;
    private readonly float jumpCooldown = 0.2f;
    private Coroutine jumpCoroutine = null;

    // Camera Parameters
    private readonly Vector3 camPos = new(0, 1, -10);
    private readonly float maxCamYaw = 70f;
    private readonly float maxCamPitch = 30f;
    private readonly float minCamPitch = -10f;
    private readonly float defaultYaw = 0f;
    private readonly float defaultPitch = 20f;
    public float targetYaw = 0f, targetPitch = 0f;
    private bool camSwitched = false;

    [Header("Wheels")]
    private readonly float forwardStiffness = 1f;
    private readonly float forwardES = 0;
    private readonly float forwardEV = 1.2f;
    private readonly float forwardAS = .5f;
    private readonly float forwardAV = .8f;
    private readonly float sideStiffness = 1f;
    private readonly float sideES = .3f;
    private readonly float sideEV = 1.1f;
    private readonly float sideAS = .6f;
    private readonly float sideAV = .7f;
    private readonly List<Wheel> Wheels = new();
    private readonly List<Wheel> FrontWheels = new();
    private readonly List<Wheel> RearWheels = new();

    #region Initialization
    private void Awake()
    {
        groundLayer = LayerMask.GetMask("Ground");

        rb = GetComponent<Rigidbody>();

        camPivot = transform.Find("CamPivot");

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
            f.extremumSlip = forwardES;
            f.extremumValue = forwardEV;
            f.asymptoteSlip = forwardAS;
            f.asymptoteValue = forwardAV;
            f.stiffness = forwardStiffness;
            wheel.Collider.forwardFriction = f;

            WheelFrictionCurve s = wheel.Collider.sidewaysFriction;
            s.stiffness = sideStiffness;
            s.extremumSlip  = sideES;
            s.extremumValue = sideEV;
            s.asymptoteSlip = sideAS;
            s.asymptoteValue= sideAV;
            wheel.Collider.sidewaysFriction = s;
        }
    }
    #endregion

    #region Updates
    public void HandleInput()
    {
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
        }

        if (grounded)
        {
            HandleRotation();
            HandleAcceleration();
        }
        else HandleAirRotation(troubled);

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
    #endregion

    #region Handlers
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
        float yawInput = steerInput.x;
        float pitchInput = troubled ? 0f : steerInput.y;

        Vector3 torque = new(
            -pitchInput * aerialTorqueMultiplier,
            yawInput * aerialTorqueMultiplier,
            0
        );

        rb.AddRelativeTorque(torque, ForceMode.Force);

        rb.angularVelocity = Vector3.ClampMagnitude(
        rb.angularVelocity,
        maxAerialAngularVelocity
        );
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
        }
    }

    private void HandleBrake(bool b)
    {
        if (b) RLCol.brakeTorque = RRCol.brakeTorque = FLCol.brakeTorque = FRCol.brakeTorque = brakeForce;
        else RLCol.brakeTorque = RRCol.brakeTorque = FLCol.brakeTorque = FRCol.brakeTorque = 0;
    }

    private void HandleJump()
    {
        if (jumpInput)
        {
            // if grounded jump
            if (grounded && !jumping)
            {
                rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
                jumpCoroutine ??= StartCoroutine(Jump());
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
    #endregion

    #region State Determinants
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

        bool fr = Physics.Raycast(FRWheel.position, -transform.up, groundedDistance, groundLayer);
        bool fl = Physics.Raycast(FLWheel.position, -transform.up, groundedDistance, groundLayer);
        bool rr = Physics.Raycast(RRWheel.position, -transform.up, groundedDistance, groundLayer);
        bool rl = Physics.Raycast(RLWheel.position, -transform.up, groundedDistance, groundLayer);

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
        if (jumping) return false;

        bool troubled = false;

        Debug.DrawRay(transform.position, transform.up * troubledUpDistance, Color.blue);
        Debug.DrawRay(transform.position, transform.right * troubledSideDistance, Color.blue);
        Debug.DrawRay(transform.position, -transform.right * troubledSideDistance, Color.blue);

        if (Physics.Raycast(transform.position, transform.up, troubledUpDistance, groundLayer) ||
            Physics.Raycast(transform.position, transform.right, troubledSideDistance, groundLayer) ||
            Physics.Raycast(transform.position, -transform.right, troubledSideDistance, groundLayer))
            troubled = true;

        return troubled;
    }
    #endregion

    private IEnumerator Jump()
    {
        jumping = true;
        yield return new WaitForSeconds(jumpCooldown);
        jumping = false;

        StopCoroutine(jumpCoroutine);
        jumpCoroutine = null;
    }
}
