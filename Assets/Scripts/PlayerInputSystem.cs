using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputSystem : MonoBehaviour
{
    private Rigidbody rb;
    private Transform car, camPivot;
    private Camera cam;

    // Actions
    private InputAction accelerateAction, decelerateAction, steerAction, lookAction, switchCamAction;

    // Action Inputs
    private float accelerateInput, decelerateInput;
    private Vector2 steerInput = Vector2.zero, lookInput = Vector2.zero;

    // Serialized Parameters
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float controllerSensitivity = 5f;
    [SerializeField] private float sensitivityMultiplier = 5f;
    [SerializeField] private float maxSteerYaw = 50f;
    [SerializeField] private float maxWheelYaw = 30f;

    // Camera Parameters
    private readonly float maxCamYaw = 70f;
    private readonly float maxCamPitch = 30f;
    private readonly float minCamPitch = -10f;
    private readonly float defaultYaw = 0f;
    private readonly float defaultPitch = 20f;
    private float targetYaw = 0f, targetPitch = 0f;
    private bool switchCam = false, camSwitched = false;

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
        lookAction.Enable();
        switchCamAction.Enable();
    }

    private void OnDisable()
    {
        accelerateAction.Disable();
        decelerateAction.Disable();
        steerAction.Disable();
        lookAction.Disable();
        switchCamAction.Disable();
    }

    private void Update()
    {
        HandleCamera();
    }

    void FixedUpdate()
    {
        HandleRotation();
        HandleMovement();
    }

    private void HandleRotation()
    {
        float yaw = steerInput.x * maxSteerYaw;

        Vector3 v = new(0, yaw, 0);
        Quaternion deltaRotation = Quaternion.Euler(v*Time.fixedDeltaTime);
        rb.MoveRotation(rb.rotation * deltaRotation);
    }

    private void HandleMovement()
    {
        Vector3 forward = car.forward;
        forward.y = 0f;
        forward.Normalize();

        float acceleration = accelerateInput - decelerateInput;

        Vector3 moveDir = forward*acceleration;

        rb.MovePosition(rb.position + Time.fixedDeltaTime*moveSpeed*moveDir);
    }

    private void HandleCamera()
    {
        float sensitivity = controllerSensitivity * Time.deltaTime*sensitivityMultiplier;

        if (lookInput != Vector2.zero)
        {
            // Input between -1 and 1 --> it's literally a percentage of maxCamYaw/Pitch
            targetYaw = lookInput.x * maxCamYaw;
            targetPitch = defaultPitch + lookInput.y * maxCamPitch;

            targetPitch = Mathf.Clamp(targetPitch, minCamPitch, maxCamPitch);
        }
        else
        {
            // Smoothly return to default
            targetYaw = Mathf.Lerp(targetYaw, defaultYaw, sensitivity);
            targetPitch = Mathf.Lerp(targetPitch, defaultPitch, sensitivity);
        }

        Quaternion targetRotation = Quaternion.Euler(targetPitch, targetYaw, 0f);

        camPivot.localRotation = Quaternion.Slerp(
            camPivot.localRotation,
            targetRotation,
            sensitivity
            );

        camPivot.localRotation = Quaternion.Euler(
            camPivot.localRotation.eulerAngles.x,
            camPivot.localRotation.eulerAngles.y,
            0f);
    }

    private void OnAccelerate(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
        {
            accelerateInput = ctx.ReadValue<float>();
        }
        else if (ctx.canceled)
        {
            accelerateInput = 0f;
        }
    }

    private void OnDecelerate(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
        {
            decelerateInput = ctx.ReadValue<float>();
        }
        else if (ctx.canceled)
        {
            decelerateInput = 0f;
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
        }
        else if (ctx.canceled)
        {
            switchCam = false;
        }
    }
}
