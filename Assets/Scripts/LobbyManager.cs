using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;
using UnityEngine.InputSystem.Utilities;
using static UnityEngine.InputSystem.InputActionRebindingExtensions;

public class LobbyManager : NetworkBehaviour
{
    public PlayerList playerList;
    public GameObject playerPrefab;    
    public GameObject SetControlsPanel;
    public GameObject BindDevice;
    public GameObject Bindleft;
    public GameObject BindRight;
    public GameObject playerControlsprefab;
    private List<GameObject> playerControlsList = new List<GameObject>();
    public GameObject joinButton;
    private Vector3 joinButtonStartPos;
    public Transform LobbyScene;

    public TMP_InputField roundsInputField;
    public NetworkVariable<int> rounds = new NetworkVariable<int>(3);

    private List<int> localPlayerIndices = new List<int>();
    private Dictionary<int, GameObject> playerControlsDict = new Dictionary<int, GameObject>();
    private RebindingOperation m_RebindOperation;

    private void Start()
    {
        roundsInputField.text = rounds.Value.ToString();
        joinButtonStartPos = joinButton.transform.localPosition;

        if (!NetworkManager.Singleton.IsListening)
        {
            RegisterPrefab(playerPrefab);
            RegisterPrefab(playerControlsprefab);
        }

        rounds.OnValueChanged += OnRoundsValueChanged;
    }
    
    private void RegisterPrefab(GameObject prefab)
    {
        bool prefabFound = false;
        foreach (var p in NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs)
        {
            if (p.Prefab == prefab)
            {
                prefabFound = true;
                break;
            }
        }
        
        if (!prefabFound)
        {
            Debug.Log($"Registering prefab {prefab.name} with NetworkManager");
            NetworkManager.Singleton.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = prefab });
        }
    }
    
    private void OnRoundsValueChanged(int previousValue, int newValue)
    {
        roundsInputField.text = newValue.ToString();
    }
    
    [ClientRpc]
    private void AdjustJoinButtonClientRpc()
    {
        joinButton.transform.localPosition = new Vector3(joinButton.transform.localPosition.x, joinButton.transform.localPosition.y-80f, 0f);
    }

    [ClientRpc]
    public void AdjustJoinButtonUpClientRpc()
    {
        joinButton.transform.localPosition = new Vector3(joinButton.transform.localPosition.x, joinButton.transform.localPosition.y+80f, 0f);
    }
    
    public void JoinPlayer()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            CreateServerPlayer(NetworkManager.Singleton.LocalClientId);
        }
        else if (NetworkManager.Singleton.IsClient)
        {
            RequestPlayerCreationServerRpc(NetworkManager.Singleton.LocalClientId);
        }
    }

    private void CreateServerPlayer(ulong clientId)
    {
        int newPlayerId = playerList.playerCount.Value;
        Debug.Log($"Server creating player {newPlayerId} for client {clientId}");
        
        Vector3 spawnPoint = new Vector3(Random.Range(-4f, 4f), Random.Range(-4f, 4f), 0f);
        
        // Spawn the player
        GameObject player = Instantiate(playerPrefab, spawnPoint, Quaternion.identity);
        NetworkObject networkObject = player.GetComponent<NetworkObject>();
        
        GameObject controlsObj = Instantiate(playerControlsprefab, Vector3.zero, Quaternion.identity);

        controlsObj.transform.localPosition = new Vector3(0, 120 - (newPlayerId * 80), 0);
        
        NetworkObject controlsNetObj = controlsObj.GetComponent<NetworkObject>();
        
        if (networkObject != null)
        {
            // Set up player on the server (without input controls)
            SnakeMovement snakeMovement = player.GetComponentInChildren<SnakeMovement>();
            snakeMovement.SetUpPlayer(newPlayerId, spawnPoint);
            
            networkObject.SpawnWithOwnership(clientId);
            playerList.AddPlayerToNetworkServerRpc(newPlayerId, networkObject.NetworkObjectId);
            
            SetPlayerIdClientRpc(newPlayerId, networkObject.NetworkObjectId);
            
            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                // Create input controls for local player
                InputUser m_User = new InputUser();
                InputActionMap m_ActionMap = new InputActionMap();
                InputAction m_Left = m_ActionMap.AddAction("Left", binding: "/*/*");
                InputAction m_Right = m_ActionMap.AddAction("Right", binding: "/*/*");
                
                // Track this as a local player on this client
                localPlayerIndices.Add(newPlayerId);
                
                playerList.AddLocalPlayer(
                    newPlayerId,
                    player,
                    m_User,
                    m_ActionMap,
                    m_Left,
                    m_Right,
                    controlsObj
                );
            }
            
            playerList.playerCount.Value++;
        }
        else
        {
            Debug.LogError("Player prefab is missing NetworkObject component");
            Destroy(controlsObj);
            Destroy(player);
            return;
        }
        
        if (controlsNetObj != null)
        {
            playerControlsList.Add(controlsObj);
            playerControlsDict[newPlayerId] = controlsObj;
            
            // Setup the text before spawning
            controlsObj.GetComponent<SetControlsText>().SetIndex(newPlayerId);
            
            controlsNetObj.Spawn();
            controlsNetObj.transform.SetParent(LobbyScene, false);
            controlsNetObj.transform.localPosition = new Vector3(0, 0, 0);
            
            SetupControlsObjectClientRpc(newPlayerId, controlsNetObj.NetworkObjectId);
        }
        
        AdjustJoinButtonClientRpc();
        
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            RebindControls(newPlayerId, false);
        }
        else
        {
            SetupClientControlsClientRpc(newPlayerId, networkObject.NetworkObjectId, new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { clientId }
                }
            });
        }
        
        Debug.Log($"Player {newPlayerId} joined for client {clientId}");
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPlayerCreationServerRpc(ulong clientId)
    {
        CreateServerPlayer(clientId);
    }
    
    [ClientRpc]
    private void SetupControlsObjectClientRpc(int playerId, ulong controlsNetObjectId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(controlsNetObjectId, out NetworkObject networkObject))
        {
            GameObject controlsObject = networkObject.gameObject;
            controlsObject.transform.localPosition = new Vector3(0, 0, 0);

            if (!playerControlsList.Contains(controlsObject))
            {
                playerControlsList.Add(controlsObject);
            }
            
            playerControlsDict[playerId] = controlsObject;
            
            // Set the player index on the UI
            SetControlsText controlsText = controlsObject.GetComponent<SetControlsText>();
            if (controlsText != null)
            {
                controlsText.SetIndex(playerId);
            }
            
            if (localPlayerIndices.Contains(playerId))
            {
                PlayerData player = playerList.FindPlayerById(playerId);
                if (player != null)
                {
                    player.playerControls = controlsObject;
                }
            }
        }
    }

    [ClientRpc]
    private void SetPlayerIdClientRpc(int playerId, ulong networkObjectId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject networkObject))
        {
            SnakeMovement snakeMovement = networkObject.GetComponentInChildren<SnakeMovement>();
            if (snakeMovement != null)
            {
                snakeMovement.SetUpPlayer(playerId, networkObject.transform.position);
            }
        }
    }

    [ClientRpc]
    private void SetupClientControlsClientRpc(int playerId, ulong networkObjectId, ClientRpcParams clientRpcParams = default)
    {
        if (NetworkManager.Singleton.IsServer && NetworkManager.Singleton.IsHost)
            return;
            
        Debug.Log($"Setting up local controls for player {playerId}");
        
        // Create local input controls
        InputUser m_User = new InputUser();
        InputActionMap m_ActionMap = new InputActionMap();
        InputAction m_Left = m_ActionMap.AddAction("Left", binding: "/*/*");
        InputAction m_Right = m_ActionMap.AddAction("Right", binding: "/*/*");
        
        // Track this as a local player
        localPlayerIndices.Add(playerId);
        
        // Find the network object for this player
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject networkObject))
        {
            GameObject player = networkObject.gameObject;
            
            // Get the controls object from our dictionary
            if (playerControlsDict.TryGetValue(playerId, out GameObject controlsObject))
            {
                // Add to player list with full control setup
                playerList.AddLocalPlayer(
                    playerId,
                    player,
                    m_User,
                    m_ActionMap,
                    m_Left,
                    m_Right,
                    controlsObject
                );
                
                // Begin control binding
                RebindControls(playerId, false);
            }
            else
            {
                Debug.LogError($"Controls object not found for player {playerId}");
            }
        }
        else
        {
            Debug.LogError($"Could not find network object with ID {networkObjectId}");
        }
    }

    public void RebindControls(int pID, bool rebind)
    {
        // Skip if this isn't a local player
        if (!localPlayerIndices.Contains(pID))
            return;
            
        SetControlsPanel.SetActive(true);
        BindDevice.SetActive(true);
        PlayerData cPlayer = playerList.FindPlayerById(pID);
        
        if (cPlayer == null)
        {
            Debug.LogError($"Could not find player with ID {pID} for rebinding controls");
            return;
        }
        cPlayer.deviceAssigned = false; 
        if (cPlayer.deviceAssigned)
        {
            cPlayer.m_User.UnpairDevices();
            cPlayer.deviceAssigned = false;
        }

        cPlayer.m_Left.Disable();
        cPlayer.m_Right.Disable();

        // Bind device to user
        InputSystem.onAnyButtonPress.CallOnce(ctrl => {
            cPlayer.m_Device = ctrl.device;
            cPlayer.m_User = InputUser.PerformPairingWithDevice(cPlayer.m_Device);
            cPlayer.m_User.AssociateActionsWithUser(cPlayer.m_ActionMap);

            Debug.Log($"Device {ctrl.device.displayName}  {cPlayer.m_Device.deviceId} was assigned to Player {pID}");
            BindDevice.SetActive(false);
            Bindleft.SetActive(true);

            // Interactive Rebinding (first left, then right)
            m_RebindOperation = cPlayer.m_Left.PerformInteractiveRebinding().WithControlsHavingToMatchPath(cPlayer.m_Device.path).OnComplete(
                        operation =>
                        {
                            string mLeftPath = operation.selectedControl.path;
                            Debug.Log(mLeftPath);
                            m_RebindOperation.Dispose();
                            Debug.Log("Player" + pID + " updated Left");
                            Bindleft.SetActive(false);
                            BindRight.SetActive(true);
                            cPlayer.playerControls.GetComponent<SetControlsText>().SetBindingText(cPlayer.m_Left.GetBindingDisplayString() + "/?");

                            cPlayer.m_Left.Enable();
                            m_RebindOperation = cPlayer.m_Right.PerformInteractiveRebinding().WithControlsExcluding(mLeftPath).OnComplete(
                                operation =>
                                {
                                    m_RebindOperation.Dispose();
                                    Debug.Log("Player" + pID + " updated Right");
                                    cPlayer.m_Right.Enable();
                                    SnakeMovement snakeMovement = cPlayer.gObject.GetComponentInChildren<SnakeMovement>();
                                    snakeMovement.SetInput(cPlayer.m_Left, cPlayer.m_Right, rebind);
                                    BindRight.SetActive(false);
                                    SetControlsPanel.SetActive(false);
                                    cPlayer.playerControls.GetComponent<SetControlsText>().SetBindingText(cPlayer.m_Left.GetBindingDisplayString() + "/" + cPlayer.m_Right.GetBindingDisplayString());
                                });
                            m_RebindOperation.Start();
                        }).Start();

            cPlayer.deviceAssigned = true;
        });
    }
    
    public void deleteAllPlayersMenu()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            foreach (GameObject playerControls in playerControlsList)
            {
                NetworkObject netObj = playerControls.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned)
                {
                    netObj.Despawn();
                }
            }
            
            // Clear the network list to notify all clients
            playerList.ClearAllPlayers();
            
            // Then despawn all player objects
            foreach (var player in playerList.players)
            {
                if (player.gObject != null)
                {
                    NetworkObject netObj = player.gObject.GetComponent<NetworkObject>();
                    if (netObj != null && netObj.IsSpawned)
                    {
                        netObj.Despawn();
                    }
                }
            }
        }
        
        // Clear local lists
        playerControlsList.Clear();
        playerControlsDict.Clear();
        localPlayerIndices.Clear();
        
        // Reset join button position
        joinButton.transform.localPosition = joinButtonStartPos;
    }
    
    public void OnMoreRoundsClick()
    {
        if (!IsServer) return;
        
        if (rounds.Value < 100)
        {
            rounds.Value++;
            roundsInputField.text = rounds.Value.ToString();
        }
    }
    public void OnLessRoundsClick()
    {
        if (!IsServer) return;
        
        if (rounds.Value > 1)
        {
            rounds.Value--;
            roundsInputField.text = rounds.Value.ToString();
        }
    }
    
    public override void OnDestroy()
    {
        base.OnDestroy();
        
        // Unsubscribe from events
        if (rounds != null)
        {
            rounds.OnValueChanged -= OnRoundsValueChanged;
        }
    }
}