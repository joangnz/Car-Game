using UnityEngine;

[CreateAssetMenu(fileName = "PlayerPrefab", menuName = "Scriptable Objects/PlayerPrefab")]
public class PlayerPrefab : ScriptableObject
{
    public GameObject Prefab;
    public bool Taken;
}
