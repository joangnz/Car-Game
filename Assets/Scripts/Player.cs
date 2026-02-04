using UnityEngine;

public class Player : MonoBehaviour
{
    private PlayerInputSystem pis;

    private void Awake()
    {
        pis = GetComponent<PlayerInputSystem>();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void FixedUpdate()
    {

    }
}
