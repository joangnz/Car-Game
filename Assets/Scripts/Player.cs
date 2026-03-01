using Fusion;
using Fusion.Addons.Physics;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : NetworkBehaviour
{
    private Canvas canvas;
    private PlayerInputSystem pis;
    private readonly List<GameObject> playerObjects = new();

    public float defaultHealth = 200;
    private readonly short magnitudeMultiplier = 3;

    [Header("Network")]
    [Networked] public float Health { get; set; }
    [Networked] public bool Respawning { get; set; }

    private readonly Vector2[] spawnPositions = { new(30, 30), new(400, 100), new(250, 175), new(125, 190) };
    private readonly float spawnHeight = 5;

    #region Initialization
    private void Awake()
    {
        pis = GetComponent<PlayerInputSystem>();
        canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);

        foreach (Transform t in transform)
        {
            if (t == transform.Find("CamPivot")) continue;
            playerObjects.Add(t.gameObject);
        }
    }

    public override void Spawned()
    {
        Health = defaultHealth;
        Respawning = false;
    }
    #endregion

    #region Updates
    private void Update()
    {
        if (HasInputAuthority) canvas.gameObject.SetActive(Health == 0);
    }

    public override void FixedUpdateNetwork()
    {
        if (Respawning && HasStateAuthority)
        {
            Vector2 r = spawnPositions[Random.Range(0, spawnPositions.Length)];
            Vector3 spawnPos = new(r.x, spawnHeight, r.y);
            NetworkRigidbody3D nrb = GetComponent<NetworkRigidbody3D>();
            nrb.Teleport(spawnPos, Quaternion.identity);
            Respawning = false;
        }

        if (Health > 0) pis.HandleInput();
    }
    #endregion

    #region Player Collisions
    private void OnCollisionEnter(Collision collision)
    {
        if (!HasStateAuthority) return;

        if (collision.gameObject.CompareTag("Player"))
        {
            Player player = collision.gameObject.GetComponent<Player>();
            HandleCollision(player);
        }
    }

    private void HandleCollision(Player player)
    {
        float magnitude = GetComponent<Rigidbody>().linearVelocity.magnitude;
        if (player.GetComponent<Rigidbody>().linearVelocity.magnitude < magnitude)
        {
            Debug.Log(magnitude);
            player.Health = Mathf.Max(player.Health-magnitude*magnitudeMultiplier, 0);
            Debug.Log(player.Health);

            if (player.Health <= 0) StartCoroutine(KillPlayer(player));
        }
    }

    private IEnumerator KillPlayer(Player player)
    {
        RPC_DespawnPlayer(player);
        yield return new WaitForSeconds(5f);
        RPC_RespawnPlayer(player);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_DespawnPlayer(Player player) { player.ToggleBody(false); }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_RespawnPlayer(Player player)
    {
        player.ToggleBody(true);
        player.Health = defaultHealth;
        player.Respawning = true;
    }

    private void ToggleBody(bool _) { foreach (GameObject g in playerObjects) g.SetActive(_); }
    #endregion
}
