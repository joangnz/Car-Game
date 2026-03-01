using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour, INetworkRunnerCallbacks
{
    // Photon
    private NetworkRunner _runner;
    private Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new();

    // Player
    [SerializeField] List<PlayerPrefab> playerPrefabs = new();
    private InputAction accelerateAction, decelerateAction, steerAction, jumpAction, lookAction, switchCamAction;
    private bool accelerateInput = false, decelerateInput = false, jumpInput = false, switchCamInput = false;
    private Vector2 steerInput = Vector2.zero, lookInput = Vector2.zero;
    private readonly Vector3 spawnPosition = new(30, 5, 30);

    #region Initialization
    void Awake()
    {
        for (int i = playerPrefabs.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (playerPrefabs[i], playerPrefabs[j]) = (playerPrefabs[j], playerPrefabs[i]);
        }
        foreach (PlayerPrefab p in playerPrefabs) p.Taken = false;
        ActionsInit();
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
    #endregion

    #region Fusion Callbacks
    async void StartGame(GameMode mode)
    {
        // Create the Fusion runner and let it know that we will be providing user input
        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true;

        // Create the NetworkSceneInfo from the current scene
        var scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);
        var sceneInfo = new NetworkSceneInfo();
        if (scene.IsValid)
        {
            sceneInfo.AddSceneRef(scene, LoadSceneMode.Additive);
        }

        // Start or join (depends on gamemode) a session with a specific name
        await _runner.StartGame(new StartGameArgs()
        {
            GameMode = mode,
            SessionName = "TestRoom",
            Scene = scene,
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        });
    }

    private void OnGUI()
    {
        if (_runner == null)
        {
            if (GUI.Button(new Rect(0, 0, 200, 40), "Host")) StartGame(GameMode.Host);
            if (GUI.Button(new Rect(0, 40, 200, 40), "Join")) StartGame(GameMode.Client);
        }
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) {
        if (runner.IsServer)
        {
            PlayerPrefab playerPrefab = null;
            foreach (PlayerPrefab p in playerPrefabs) if (!p.Taken) playerPrefab = p;
            if (playerPrefab == null) return;
            playerPrefab.Taken = true;

            NetworkObject networkPlayerObject = runner.Spawn( playerPrefab.Prefab, spawnPosition, Quaternion.identity, player);
            networkPlayerObject.GetComponent<Player>().PlayerPrefab = playerPrefab;

            // Keep track of the player avatars for easy access
            _spawnedCharacters.Add(player, networkPlayerObject);
        }
    }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) {
        if (_spawnedCharacters.TryGetValue(player, out NetworkObject networkObject))
        {
            networkObject.GetComponent<Player>().PlayerPrefab.Taken = false;
            runner.Despawn(networkObject);
            _spawnedCharacters.Remove(player);
        }
    }
    public void OnInput(NetworkRunner runner, NetworkInput input) {
        NetworkInputData data = new()
        {
            accelerateInput = accelerateInput,
            decelerateInput = decelerateInput,
            jumpInput = jumpInput,
            steerInput = steerInput,
            lookInput = lookInput,
            switchCamInput = switchCamInput
        };

        input.Set(data);
    }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    #endregion

    #region Actions
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
            switchCamInput = true;

        }
        else if (ctx.canceled)
        {
            switchCamInput = false;
        }
    }
    #endregion
}

public struct NetworkInputData : INetworkInput
{
    public bool accelerateInput, decelerateInput, jumpInput, switchCamInput;
    public Vector2 steerInput, lookInput;
}