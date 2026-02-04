using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] GameObject playerPrefab;

    private Player player;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        player = Instantiate(playerPrefab).GetComponent<Player>();
        player.transform.position = new(5, 2, 5);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
