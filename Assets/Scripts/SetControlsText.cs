using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class SetControlsText : MonoBehaviour
{
    public TMP_Text bindingText;
    public TMP_Text playerNumber;
    public GameObject allGameobject;
    private int playerID;
    public PlayerList playerList;
    public LobbyManager lobbyManager;
    public Button setControlsButton;

    void Start()
    {
        lobbyManager = FindFirstObjectByType<LobbyManager>();
        playerList = FindFirstObjectByType<PlayerList>();
    }

    public void SetBindingText(string binding)
    {
        bindingText.text = binding;
    }

    public void deletePlayer()
    {
        if(!NetworkManager.Singleton.IsServer)
        {
            return;
        }
        Debug.Log($"Attempting to delete player {playerID}");
        
        RequestPlayerDeletionServerRpc(playerID);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPlayerDeletionServerRpc(int playerToDelete)
    {
        Debug.Log($"Server received request to delete player {playerToDelete}");

        // Find the actual player data for this playerID
        PlayerData playerToRemove = null;
        int localIndex = -1;
        
        // First try to find in local players list
        for (int i = 0; i < playerList.players.Count; i++)
        {
            if (playerList.players[i].ID == playerToDelete)
            {
                playerToRemove = playerList.players[i];
                localIndex = i;
                break;
            }
        }
        
        if (playerToRemove == null)
        {
            Debug.Log($"Player {playerToDelete} not found in local list, checking network list");
            
            int networkIndex = -1;
            for (int i = 0; i < playerList.networkPlayers.Count; i++)
            {
                if (playerList.networkPlayers[i].playerId == playerToDelete)
                {
                    networkIndex = i;
                    
                    if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
                        playerList.networkPlayers[i].networkObjectId, out NetworkObject networkObj))
                    {
                        Debug.Log($"Found network player {playerToDelete} at network index {i}");
                        
                        playerToRemove = new PlayerData(
                            playerToDelete,
                            networkObj.gameObject,
                            default,
                            null,
                            null,
                            null,
                            null,
                            Color.white,
                            ""
                        );
                        break;
                    }
                }
            }
            
            if (networkIndex >= 0)
            {
                Debug.Log($"Removing player {playerToDelete} from network list at index {networkIndex}");
                playerList.RemovePlayerFromNetworkServerRpc(playerToDelete);
            }
        }
        
        if (playerToRemove != null)
        {
            // Update the player count
            playerList.playerCount.Value--;
            
            // Handle local cleanup
            if (localIndex >= 0)
            {
                Debug.Log($"Removing player {playerToDelete} from local list at index {localIndex}");
                
                if (playerToRemove.playerControls != null)
                {
                    Destroy(playerToRemove.playerControls);
                }
                
                playerList.players.RemoveAt(localIndex);
            }
            
            if (playerToRemove.gObject != null)
            {
                NetworkObject playerNetObj = playerToRemove.gObject.GetComponent<NetworkObject>();
                
                if (playerNetObj != null)
                {
                    if (playerNetObj.IsSpawned)
                    {
                        // Transfer ownership to server if needed
                        ulong oldOwner = playerNetObj.OwnerClientId;
                        if (oldOwner != NetworkManager.ServerClientId)
                        {
                            Debug.Log($"Changing ownership from client {oldOwner} to server before despawning");
                            playerNetObj.ChangeOwnership(NetworkManager.ServerClientId);
                        }
                        foreach (Transform child in playerNetObj.transform)
                        {
                            if (child.TryGetComponent(out Tail tailObj))
                            {
                                tailObj.GetComponent<NetworkObject>().Despawn(true);
                                Destroy(child);
                            }
                        }
                        
                        // Now despawn it
                        Debug.Log($"Despawning network object for player {playerToDelete}");
                        playerNetObj.Despawn(true);
                    }
                    else
                    {
                        Debug.Log($"Network object for player {playerToDelete} was not spawned, destroying directly");
                        Destroy(playerToRemove.gObject);
                    }
                }
                else
                {
                    Debug.Log($"No NetworkObject found for player {playerToDelete}, destroying directly");
                    Destroy(playerToRemove.gObject);
                }
            }
            
            // Update the UI
            if (lobbyManager != null)
            {
                lobbyManager.AdjustJoinButtonUpClientRpc();
            }
            
            // Notify all clients to update their UI
            DeletePlayerClientRpc(playerToDelete);
        }
        else
        {
            Debug.LogError($"Could not find player with ID {playerToDelete} in any list!");
        }
        playerList.RemovePlayerFromNetworkListServerRpc(playerToDelete);
        playerList.players.RemoveAll(player => player.ID == playerToDelete);
        if(this.gameObject != null)
        {
            Destroy(this.gameObject);
        }
    }

    [ClientRpc]
    private void DeletePlayerClientRpc(int playerToDelete)
    {
        Debug.Log($"Client received delete player request for player ID {playerToDelete}");
        
        // Skip further processing on the server as it's already handled the deletion
        if (NetworkManager.Singleton.IsServer)
        {
            return;
        }
        
        
        // Find the player in the local list by ID
        int localIndex = -1;
        PlayerData playerToRemove = null;
        
        for (int i = 0; i < playerList.players.Count; i++)
        {
            if (playerList.players[i].ID == playerToDelete)
            {
                localIndex = i;
                playerToRemove = playerList.players[i];
                break;
            }
        }
        
        // If found in local list, clean up
        if (localIndex >= 0 && playerToRemove != null)
        {
            Debug.Log($"Client removing player {playerToDelete} from local list at index {localIndex}");
            
            // Destroy the control UI if it exists
            if (playerToRemove.playerControls != null)
            {
                Destroy(playerToRemove.playerControls);
            }
            
            // Remove from local list
            playerList.players.RemoveAt(localIndex);
        }
        
        // If this is the control panel for the deleted player, destroy it
        if (playerID == playerToDelete)
        {
            Debug.Log($"Destroying UI control panel for player {playerToDelete}");
            Destroy(allGameobject);
        }
        Debug.LogError("Adjusting join button on client after player deletion" + lobbyManager);
        if (lobbyManager != null)
        {
            
            lobbyManager.AdjustJoinButtonUpClientRpc();
        }
        if(this.gameObject != null)
        {
            Destroy(this.gameObject);
        }
    }

    public void SetIndex(int index)
    {
        StartCoroutine(Wait1SecondSetIndex(index));
    }

    IEnumerator Wait1SecondSetIndex(int index)
    {
        yield return new WaitForSeconds(.05f);
        playerID = index;
        
        // Check if the playerList and playerNames arrays are valid
        if (playerList != null && index < playerList.playerNames.Count && index < playerList.playerColors.Count)
        {
            playerNumber.text = "" + (index+1) + ":" + playerList.playerNames[index];
            ColorBlock colorBlock = setControlsButton.colors;
            colorBlock.normalColor = playerList.playerColors[index];
            setControlsButton.colors = colorBlock;
        }
        
        allGameobject.transform.localPosition = new Vector3(0f, -70f * index, 0f);
    }

    public void RebindControls()
    {
        lobbyManager.RebindControls(playerID, true);
    }
}