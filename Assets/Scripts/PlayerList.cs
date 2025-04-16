using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;
using System;
using Unity.Netcode;

public class PlayerList : NetworkBehaviour
{
    public NetworkList<PlayerNetworkData> networkPlayers;
    public List<PlayerData> players = new List<PlayerData>();
    public NetworkVariable<int> playerCount = new NetworkVariable<int>(0);
    public List<Color> playerColors = new List<Color>();
    public List<string> playerNames = new List<string>();
    public List<Material> playerMaterials = new List<Material>();

    private void Awake()
    {
        networkPlayers = new NetworkList<PlayerNetworkData>();
        
        networkPlayers.OnListChanged += OnNetworkPlayersChanged;
    }
    [ServerRpc(RequireOwnership = false)]
    public void RemovePlayerFromNetworkListServerRpc(int playerId)
    {
        if (IsServer)
        {
            for (int i = 0; i < networkPlayers.Count; i++)
            {
                if (networkPlayers[i].playerId == playerId)
                {
                    networkPlayers.RemoveAt(i);
                    break;
                }
            }
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        
        if (networkPlayers != null)
        {
            networkPlayers.OnListChanged -= OnNetworkPlayersChanged;
        }
    }

    private void OnNetworkPlayersChanged(NetworkListEvent<PlayerNetworkData> changeEvent)
    {
        if (!IsServer)
        {
            switch (changeEvent.Type)
            {
                case NetworkListEvent<PlayerNetworkData>.EventType.Add:
                    // A new player was added to the network list
                    HandlePlayerAdded(changeEvent.Value);
                    break;
                
                case NetworkListEvent<PlayerNetworkData>.EventType.Remove:
                    // A player was removed from the network list
                    HandlePlayerRemoved(changeEvent.Value);
                    break;
                
                case NetworkListEvent<PlayerNetworkData>.EventType.Value:
                    // A player's data was updated
                    HandlePlayerUpdated(changeEvent.Value);
                    break;
            }
        }
    }

    private void HandlePlayerAdded(PlayerNetworkData networkData)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkData.networkObjectId, out NetworkObject networkObject))
        {
            GameObject playerObject = networkObject.gameObject;
            
            // Check if this player is already in our local list
            bool playerFound = false;
            foreach (var existingPlayer in players)
            {
                if (existingPlayer.ID == networkData.playerId)
                {
                    playerFound = true;
                    break;
                }
            }
            
            if (!playerFound)
            {
                players.Add(new PlayerData(
                    networkData.playerId,
                    playerObject,
                    default, // No InputUser for non-local players
                    null, // No InputActionMap for non-local players
                    null, // No InputAction for non-local players
                    null, // No InputAction for non-local players
                    null, // No player controls for non-local players

                    playerColors[networkData.playerId % playerColors.Count],
                    playerNames[networkData.playerId % playerNames.Count]
                ));
                
                Debug.Log($"Client added player {networkData.playerId} to local player list");
            }
        }
        else
        {
            Debug.LogWarning($"Could not find NetworkObject with ID {networkData.networkObjectId}");
        }
    }

    private void HandlePlayerRemoved(PlayerNetworkData networkData)
    {
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].ID == networkData.playerId)
            {
                // Don't destroy the actual GameObject - the server handles that
                players.RemoveAt(i);
                Debug.Log($"Client removed player {networkData.playerId} from local player list");
                break;
            }
        }
    }

    private void HandlePlayerUpdated(PlayerNetworkData networkData)
    {
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].ID == networkData.playerId)
            {
                // If the NetworkObject has changed, update it
                if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkData.networkObjectId, out NetworkObject networkObject))
                {
                    PlayerData updatedPlayerData = players[i];
                    updatedPlayerData.gObject = networkObject.gameObject;
                    players[i] = updatedPlayerData;
                }
                break;
            }
        }
    }

    public PlayerData FindPlayerById(int playerId)
    {
        foreach (var player in players)
        {
            if (player.ID == playerId)
            {
                return player;
            }
        }
        return null;
    }

    public void AddLocalPlayer(int playerId, GameObject playerObject, InputUser user, 
                            InputActionMap actionMap, InputAction leftAction, InputAction rightAction, 
                            GameObject controlsUI)
    {
        // Check if player already exists
        bool playerExists = false;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].ID == playerId)
            {
                // Update existing player data
                PlayerData updatedData = players[i];
                updatedData.gObject = playerObject;
                updatedData.m_User = user;
                updatedData.m_ActionMap = actionMap;
                updatedData.m_Left = leftAction;
                updatedData.m_Right = rightAction;
                updatedData.playerControls = controlsUI;
                players[i] = updatedData;
                playerExists = true;
                break;
            }
        }

        // Add new player if doesn't exist
        if (!playerExists)
        {
            players.Add(new PlayerData(
                playerId,
                playerObject,
                user,
                actionMap,
                leftAction,
                rightAction,
                controlsUI,
                playerColors[playerId % playerColors.Count],
                playerNames[playerId % playerNames.Count]
            ));
        }
        Debug.Log($"Added local player {playerId} to local player list");
    }

    [ServerRpc(RequireOwnership = false)]
    public void AddPlayerToNetworkServerRpc(int playerId, ulong networkObjectId)
    {
        if (IsServer)
        {
            // Check if this player ID is already in the network list
            bool found = false;
            foreach (var networkPlayer in networkPlayers)
            {
                if (networkPlayer.playerId == playerId)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                networkPlayers.Add(new PlayerNetworkData
                {
                    playerId = playerId,
                    networkObjectId = networkObjectId
                });
                
                Debug.Log($"Server added player {playerId} to network player list");
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RemovePlayerFromNetworkServerRpc(int playerId)
    {
        if (IsServer)
        {
            for (int i = 0; i < networkPlayers.Count; i++)
            {
                if (networkPlayers[i].playerId == playerId)
                {
                    networkPlayers.RemoveAt(i);
                    Debug.Log($"Server removed player {playerId} from network player list");
                    break;
                }
            }
        }
    }

    public void ClearAllPlayers()
    {
        if (IsServer)
        {
            networkPlayers.Clear();
            playerCount.Value = 0;
        }
        
        players.Clear();
    }
}

public struct PlayerNetworkData : INetworkSerializable, IEquatable<PlayerNetworkData>
{
    public int playerId;
    public ulong networkObjectId;
    
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref playerId);
        serializer.SerializeValue(ref networkObjectId);
    }

    public bool Equals(PlayerNetworkData other)
    {
        return playerId == other.playerId && networkObjectId == other.networkObjectId;
    }

    public override bool Equals(object obj)
    {
        return obj is PlayerNetworkData other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(playerId, networkObjectId);
    }
}

public class PlayerData
{
    public int ID { get; private set; }
    public GameObject gObject;
    public InputUser m_User;
    public InputDevice m_Device;
    public InputActionMap m_ActionMap;
    public InputAction m_Left;
    public InputAction m_Right;
    public GameObject playerControls;
    public bool deviceAssigned = false;
    public Color color;
    public string name;
    public int score = 0;

    public PlayerData(int _id, GameObject _gObject, InputUser _m_user, InputActionMap _m_ActionMap, InputAction _m_left, InputAction _m_Right,
    GameObject _playerControls, Color _color, string _name)
    {
        ID = _id;
        gObject = _gObject;
        m_User = _m_user;
        m_ActionMap = _m_ActionMap;
        m_Left = _m_left;
        m_Right = _m_Right;
        playerControls = _playerControls;
        color = _color;
        name = _name;
    }
}