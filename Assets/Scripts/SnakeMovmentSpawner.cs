using Unity.Netcode;
using UnityEngine;

public class SnakeMovmentSpawner : NetworkBehaviour
{
    public SnakeMovement snakeMovement;
    public GameObject tail;
    private GameObject instantiatedTail;
    
    [ServerRpc(RequireOwnership = false)]
    public void SpawnTailLocallySPAWNERServerRpc(ServerRpcParams rpcParams = default)
    {
        NetworkObject tailNetObj = NetworkManager.Singleton.SpawnManager.InstantiateAndSpawn(tail.GetComponent<NetworkObject>());
        tailNetObj.transform.parent = snakeMovement.transform.parent;
        instantiatedTail = tailNetObj.gameObject;

        snakeMovement.currentTail = instantiatedTail;
        instantiatedTail.GetComponent<Tail>().SnakeHead = snakeMovement.gameObject;
        instantiatedTail.GetComponent<Tail>().SetUpLine();

        snakeMovement.currentTailRef.Value = tailNetObj;
    }
}