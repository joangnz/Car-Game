using Fusion;
using Fusion.Addons.Physics;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : NetworkBehaviour
{
    private Canvas canvas;
    private PlayerInputSystem pis;
    private List<GameObject> playerObjects = new();

    public float defaultHealth = 100;

    [Header("Network")]
    [Networked] public float Health { get; set; }
    [Networked] public bool Respawning { get; set; }
    private readonly Vector3 spawnPosition = new(30, 5, 30);

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

    private void Update()
    {
        if (HasInputAuthority) canvas.gameObject.SetActive(Health == 0);
    }

    public override void FixedUpdateNetwork()
    {
        if (Respawning && HasStateAuthority)
        {
            NetworkRigidbody3D nrb = GetComponent<NetworkRigidbody3D>();
            nrb.Teleport(spawnPosition, Quaternion.identity);
            Respawning = false;
        }

        if (Health > 0) pis.HandleInput();
    }

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
        if (player.GetComponent<Rigidbody>().linearVelocity.magnitude <
            GetComponent<Rigidbody>().linearVelocity.magnitude)
        {
            player.Health = Mathf.Max(player.Health-30, 0);
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
}
