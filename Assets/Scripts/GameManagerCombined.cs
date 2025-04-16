using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameManagerCombined : NetworkBehaviour
{
    [Header("References")]
    public PlayerList playerList;
    private NetworkList<int> alivePlayers;
    public GameObject playerlistPanel;
    public GameObject playerlistPrefab;
    
    [Header("Game Scenes")]
    public GameObject GameSceneObject;
    public GameObject LobbySceneObject;
    public GameObject countDown;
    
    [Header("UI Elements")]
    public TMP_InputField roundsNumberText;
    public TMP_Text roundsText;
    public TMP_Text winnerText;
    public GameObject moreRoundsButton;
    public GameObject lessRoundsButton;
    public GameObject roundsInputField;
    public GameObject pauseText;
    
    [Header("PowerUps")]
    public GameObject doubleSpeedPrefab;
    public GameObject halfSpeedPrefab;
    public GameObject invincibilityPlayerPrefab;
    public GameObject invincibilityWallPrefab;
    public GameObject resetTailPrefab;
    public GameObject invertControlsOthersPrefab;
    public GameObject halfSpeedOthersPrefab;
    public GameObject doubleSpeedOthersPrefab;
    public GameObject resetAllTailsPrefab;
    public GameObject invincibilityWallAllPrefab;
    public GameObject invincibilityPlayerAllPrefab;
    public GameObject ninetyDegreeControlsPrefab;
    public GameObject invincibilityPlayerPrefabAll;
    public GameObject doubleSizePrefab;
    public GameObject halfSizePrefab;
    
    // Private fields
    private List<int> deadPlayers;
    private List<GameObject> powerUps = new List<GameObject>();
    private Dictionary<int, int> playerPoints = new Dictionary<int, int>();
    
    // Network variables
    private NetworkVariable<int> networkRounds = new NetworkVariable<int>(3);
    private NetworkVariable<Dictionary<int, int>> networkPlayerPoints = new NetworkVariable<Dictionary<int, int>>(new Dictionary<int, int>());
    private NetworkVariable<bool> spawnPowerUp = new NetworkVariable<bool>(false);
    private NetworkVariable<bool> gameInProgress = new NetworkVariable<bool>(false);
    private NetworkVariable<int> lastWinner = new NetworkVariable<int>(-1);

    void Awake()
    {
        alivePlayers = new NetworkList<int>();
        deadPlayers = new List<int>();
    }
    
    void Start()
    {
        networkPlayerPoints.Value = new Dictionary<int, int>();
        
        // Populate local player points from any existing players
        foreach (PlayerData player in playerList.players)
        {
            if (!playerPoints.ContainsKey(player.ID))
            {
                playerPoints[player.ID] = 0;
            }
        }
        
        networkRounds.OnValueChanged += OnRoundsChanged;
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }
    
    void OnEnable()
    {
        SnakeMovement.OnCollision += HandleCollision;
    }
    
    void OnDisable()
    {
        SnakeMovement.OnCollision -= HandleCollision;
    }

    private void OnClientDisconnected(ulong clientId)
    {
        NetworkManager.Singleton.Shutdown();
        SceneManager.LoadScene(this.gameObject.scene.name);
    }
    [ServerRpc(RequireOwnership = false)]
    public void SynchronizePlayerListsServerRpc()
    {
        if (!IsServer) return;
        
        foreach (var player in playerList.players)
        {
            bool found = false;
            foreach (var networkPlayer in playerList.networkPlayers)
            {
                if (networkPlayer.playerId == player.ID)
                {
                    found = true;
                    break;
                }
            }
            
            if (!found && player.gObject != null && player.gObject.GetComponent<NetworkObject>() != null)
            {
                ulong networkId = player.gObject.GetComponent<NetworkObject>().NetworkObjectId;
                playerList.networkPlayers.Add(new PlayerNetworkData
                {
                    playerId = player.ID,
                    networkObjectId = networkId
                });
                Debug.Log($"Added missing player ID {player.ID} to network list with NetworkObjectId {networkId}");
            }
        }
        Debug.Log("Amount of players in network list: " + playerList.networkPlayers.Count + "local players: " + playerList.players.Count);
        SyncCompletePlayerListClientRpc();
    }
    [ClientRpc]
    public void SyncCompletePlayerListClientRpc()
    {
        ClearAndRebuildPlayerListUI();
    }
    private void ClearAndRebuildPlayerListUI()
    {
        foreach (Transform child in playerlistPanel.transform)
        {
            Destroy(child.gameObject);
        }
        
        List<PlayerData> allPlayers = new List<PlayerData>(playerList.players);
        
        Debug.Log($"Building UI for {allPlayers.Count} players");
        
        // Sort players by points
        allPlayers.Sort((a, b) => {
            int pointsA = playerPoints.ContainsKey(a.ID) ? playerPoints[a.ID] : 0;
            int pointsB = playerPoints.ContainsKey(b.ID) ? playerPoints[b.ID] : 0;
            return pointsB.CompareTo(pointsA);
        });
        
        // Create UI elements for each player
        for (int i = 0; i < allPlayers.Count; i++)
        {
            PlayerData player = allPlayers[i];
            
            // Instantiate the player list item
            GameObject playerListItem = Instantiate(playerlistPrefab, playerlistPanel.transform);
            playerListItem.transform.localPosition = new Vector3(0f, -70f * i, 0f);
            
            int playerIDDisplay = player.ID + 1;
            int playerPointsDisplay = playerPoints.ContainsKey(player.ID) ? playerPoints[player.ID] : 0;
            
            TextMeshProUGUI textComponent = playerListItem.GetComponentInChildren<TextMeshProUGUI>();
            Image imageComponent = playerListItem.GetComponentInChildren<Image>();
            
            if (textComponent != null)
            {
                textComponent.text = $"{playerIDDisplay}: {player.name} - {playerPointsDisplay} pts";
            }
            
            if (imageComponent != null)
            {
                imageComponent.color = player.color;
            }
        }
    }



    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        deadPlayers = new List<int>();
        
        if (IsServer && networkPlayerPoints == null)
        {
            networkPlayerPoints = new NetworkVariable<Dictionary<int, int>>(new Dictionary<int, int>());
        }
        
        if (IsServer)
        {
            alivePlayers.Clear();
            spawnPowerUp.Value = false;
            gameInProgress.Value = false;
            lastWinner.Value = -1;
        }
        
        // Subscribe to network variable changes
        spawnPowerUp.OnValueChanged += OnSpawnPowerUpChanged;
        lastWinner.OnValueChanged += OnLastWinnerChanged;
        networkPlayerPoints.OnValueChanged += OnPlayerPointsChanged;
        
        if (IsServer)
        {
            StartCoroutine(DelayedPlayerListSync());
        }
    }
    private IEnumerator DelayedPlayerListSync()
    {
        // Wait for player registration to complete
        yield return new WaitForSeconds(1f);
        
        // Force synchronize player lists
        SynchronizePlayerListsServerRpc();
    }
    private void OnPlayerPointsChanged(Dictionary<int, int> previousValue, Dictionary<int, int> newValue)
    {
        RefreshPlayerListUI();
    }



    private IEnumerator DelayedUIRefresh()
    {
        // Wait a frame to ensure network variables are properly initialized
        yield return new WaitForEndOfFrame();
        
        // Update player list UI for all clients including host
        UpdatePlayerListClientRpc();
    }
    
    public override void OnNetworkDespawn()
    {
        // Unsubscribe from network variable changes
        spawnPowerUp.OnValueChanged -= OnSpawnPowerUpChanged;
        lastWinner.OnValueChanged -= OnLastWinnerChanged;
        networkPlayerPoints.OnValueChanged -= OnPlayerPointsChanged; // Add this line
        
        base.OnNetworkDespawn();
    }
    
    public override void OnDestroy()
    {
        base.OnDestroy();
        
        if (networkRounds != null)
        {
            networkRounds.OnValueChanged -= OnRoundsChanged;
        }
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }
    
    void Update()
    {
        if(GameSceneObject.activeSelf)
        {
            if(Keyboard.current[Key.Escape].wasPressedThisFrame && Time.timeScale == 1f)
            {
                PauseGameClientRpc();
            }
            else if (Keyboard.current[Key.Escape].wasPressedThisFrame && Time.timeScale == 0f)
            {
                if (IsServer)
                {
                    StartCoroutine(waitForCountdown());
                }
                else
                {
                    RequestUnpauseServerRpc();
                }
            }
        }
    }
    
    #region Callback Methods
    
    private void OnRoundsChanged(int oldValue, int newValue)
    {
        roundsText.text = "Rounds Left: " + newValue.ToString();
    }
    
    private void OnSpawnPowerUpChanged(bool previous, bool current)
    {
        if (current && IsServer)
        {
            Invoke("spawnPowerUps", 5f);
        }
    }
    
    
    private void OnLastWinnerChanged(int previous, int current)
    {
        if (current >= 0)
        {
            winnerText.gameObject.SetActive(true);
            winnerText.text = "Player " + (current + 1) + " won!";
        }
    }
    
    #endregion
    
    #region Game Flow Control
    
    public void StartGameButton()
    {
        if (!IsServer)
        {
            Debug.Log("Only the host can start the game");
            return;
        }
        
        if (roundsNumberText.text != "")
        {
            networkRounds.Value = int.Parse(roundsNumberText.text);
        }
        else
        {
            networkRounds.Value = 3; 
        }
 if (roundsNumberText.text != "")
        {
            networkRounds.Value = int.Parse(roundsNumberText.text);
        }
        else
        {
            networkRounds.Value = 3; 
        }
        UnfreezePlayers();
        SyncLocalListFromNetworkList();
        StartGameClientRpc();
    }
        
    [ClientRpc]
    public void StartGameClientRpc()
    {
        moreRoundsButton.SetActive(false);
        lessRoundsButton.SetActive(false);
        roundsInputField.SetActive(false);
        roundsText.text = "Rounds Left: " + networkRounds.Value.ToString();
        
        GameSceneObject.SetActive(true);
        LobbySceneObject.SetActive(false);
        
        SyncCompletePlayerListClientRpc();
        DisplayPlayers(-1);
    }
    
    public void NextRoundButton()
    {
        // Only the server should control round progression
        if (!IsServer)
        {
            Debug.Log("Only the host can advance rounds");
            return;
        }
        
        Debug.Log("Next Round Button Pressed. Rounds left: " + networkRounds.Value);
        if(networkRounds.Value <= 0)
        {
            return;
        }
        
        // Decrement rounds on the server
        networkRounds.Value--;
        
        // Reset player positions
        ResetPlayersPositionsClientRpc();
        InitializeAlivePlayers();
        
        StartCoroutine(DelayThenNextRound());
    }
    
    private IEnumerator DelayThenNextRound()
    {
        // Wait for a short delay before proceeding to the next round
        yield return new WaitForSeconds(.6f);
        spawnPowerUp.Value = true;
        NextRoundClientRpc();
    }
    private void SyncLocalListFromNetworkList()
    {
        if (!IsServer) return;
        
        Debug.Log("Syncing server's local player list from network list");
        
        // For each player in the network list
        foreach (var networkPlayer in playerList.networkPlayers)
        {
            // Check if this player exists in the local list
            bool playerExists = false;
            foreach (var localPlayer in playerList.players)
            {
                if (localPlayer.ID == networkPlayer.playerId)
                {
                    playerExists = true;
                    break;
                }
            }
            
            if (!playerExists)
            {
                if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkPlayer.networkObjectId, out NetworkObject networkObject))
                {
                    // Create a local player entry with minimal required data
                    playerList.players.Add(new PlayerData(
                        networkPlayer.playerId,
                        networkObject.gameObject,
                        default, // No InputUser for remote players
                        null,    // No InputActionMap for remote players
                        null,    // No InputAction for remote players
                        null,    // No InputAction for remote players
                        null,    // No playerControls for remote players
                        playerList.playerColors[networkPlayer.playerId % playerList.playerColors.Count],
                        playerList.playerNames[networkPlayer.playerId % playerList.playerNames.Count]
                    ));
                    
                    Debug.Log($"Added missing player {networkPlayer.playerId} to server's local list");
                }
            }
        }
        InitializeAlivePlayers();
    }
        
    [ClientRpc]
    public void NextRoundClientRpc()
    {
        roundsText.text = "Rounds Left: " + networkRounds.Value.ToString();
        
        FreezePlayersClientRpc();
        resetPlayers();
        Time.timeScale = 0f;
        
        StartCoroutine(waitForCountdown());
        UnfreezePlayersClientRpc();
        
        LobbySceneObject.SetActive(false);
        
        DisplayPlayers(-1);
    }
    private void InitializeAlivePlayers()
    {
        if (!IsServer) return;
        
        alivePlayers.Clear();
        foreach (PlayerData playerData in playerList.players)
        {
            alivePlayers.Add(playerData.ID);
        }
    }
        
    public void BackToMenuButton()
    {
        // Only the server should control returning to menu
        if (!IsServer)
        {
            Debug.Log("Only the host can return to menu");
            return;
        }
        
        // Call RPC to return to menu on all clients
        BackToMenuClientRpc();
        DeleteAllPlayersFromMenuServerRpc();
        
    }
    public void DisconnectButton()
    {
        if (IsServer)
        {
            ForceDisconnectAllClientsClientRpc();
            
            StartCoroutine(DelayedServerShutdown());
        }
        else
        {
            NetworkManager.Singleton.Shutdown();
            SceneManager.LoadScene(this.gameObject.scene.name);
        }
    }
    public void ExitGameButton()
    {
        Application.Quit();
    }

    [ClientRpc]
    private void ForceDisconnectAllClientsClientRpc()
    {
        if (!IsServer)
        {
            // Client needs to handle its own disconnection
            NetworkManager.Singleton.Shutdown();
            SceneManager.LoadScene(this.gameObject.scene.name);
        }
    }

    private IEnumerator DelayedServerShutdown()
    {
        // Wait for clients to process the disconnect RPC
        yield return new WaitForSeconds(0.5f);
        
        // Now shut down the server and reload the scene
        NetworkManager.Singleton.Shutdown();
        SceneManager.LoadScene(this.gameObject.scene.name);
    }

    
    [ClientRpc]
    public void BackToMenuClientRpc()
    {
        // Restore UI elements
        moreRoundsButton.SetActive(true);
        lessRoundsButton.SetActive(true);
        roundsInputField.SetActive(true);
        roundsText.text = "Rounds:";
        
        // Reset game state
        FreezePlayersClientRpc();
        deleteAllPlayers();
        
        GameSceneObject.SetActive(false);   
        LobbySceneObject.SetActive(true);
        
        // Clear local player points
        playerPoints.Clear();
    }
    
    #endregion
    
    #region Player Management
    
    public void DisplayPlayers(int playerID)
    {
        if (IsServer)
        {
            // Server-side logic to update player lists and calculate points
            if (playerID == -1)
            {
                deadPlayers = new List<int>();
            }
            else
            {
                deadPlayers.Add(playerID);
                
                // Notify all clients about player death
                UpdateDeadPlayersClientRpc(playerID);
            }
            
            // Calculate points on server
            CalculatePoints();
            
            // Update all clients with the new player state
            UpdatePlayerListClientRpc();
        }
    }
    
    [ClientRpc]
    public void UpdateDeadPlayersClientRpc(int playerID)
    {
        // For clients, just add the player to dead players list
        if (!IsServer)
        {
            if (deadPlayers == null)
                deadPlayers = new List<int>();
                
            if (playerID != -1 && !deadPlayers.Contains(playerID))
                deadPlayers.Add(playerID);
        }
    }
    
    private void CalculatePoints()
    {
        // Only calculate points if there have been deaths this round
        if (deadPlayers.Count == 0) 
            return;

        List<PlayerData> alivePlayers = new List<PlayerData>();
        List<PlayerData> deadPlayersList = new List<PlayerData>();

        foreach (PlayerData playerData in playerList.players)
        {
            if (!playerPoints.ContainsKey(playerData.ID))
            {
                playerPoints[playerData.ID] = 0; 
            }

            if (deadPlayers.Contains(playerData.ID))
            {
                deadPlayersList.Add(playerData);
            }
            else
            {
                alivePlayers.Add(playerData);
            }
        }

        Dictionary<int, int> roundPoints = new Dictionary<int, int>();
        int totalPlayers = playerList.players.Count;

        // Award points based on order of death (first to die gets 0, second gets 1, etc.)
        for (int i = 0; i < deadPlayersList.Count; i++)
        {
            int playerId = deadPlayersList[i].ID;
            roundPoints[playerId] = i;
        }
        
        // Last survivor gets maximum points
        if (alivePlayers.Count == 1)
        {
            int lastSurvivorId = alivePlayers[0].ID;
            roundPoints[lastSurvivorId] = totalPlayers - 1; 
        }
        
        Dictionary<int, int> updatedNetworkPoints = new Dictionary<int, int>();
        
        foreach (var pair in playerPoints)
        {
            updatedNetworkPoints[pair.Key] = pair.Value;
        }
        
        // Add round points to total points
        foreach (var player in roundPoints)
        {
            int playerId = player.Key;
            int points = player.Value;
            
            playerPoints[playerId] += points;
            updatedNetworkPoints[playerId] = playerPoints[playerId];
        }
        
        networkPlayerPoints.Value = updatedNetworkPoints;
    }
    
    [ClientRpc]
    public void UpdatePlayerListClientRpc()
    {
        // Clear any existing player list UI first
        foreach (Transform child in playerlistPanel.transform)
        {
            Destroy(child.gameObject);
        }
        
        // For all clients, including server, update the player list display
        RefreshPlayerListUI();
    }
    
    private void RefreshPlayerListUI()
    {
        foreach (var pair in networkPlayerPoints.Value)
        {
            playerPoints[pair.Key] = pair.Value;
        }
        
        List<PlayerData> sortedPlayers = new List<PlayerData>(playerList.players);
        // Sort by points, descending
        sortedPlayers.Sort((a, b) =>
        {
            int pointsA = playerPoints.ContainsKey(a.ID) ? playerPoints[a.ID] : 0;
            int pointsB = playerPoints.ContainsKey(b.ID) ? playerPoints[b.ID] : 0;
            return pointsB.CompareTo(pointsA); 
        });

        foreach (Transform child in playerlistPanel.transform)
        {
            Destroy(child.gameObject);
        }
        
        // Recreate UI with sorted players
        for (int i = 0; i < sortedPlayers.Count; i++)
        {
            GameObject playerListItem = Instantiate(playerlistPrefab, playerlistPanel.transform);
            playerListItem.transform.localPosition = new Vector3(0f, -70f * i, 0f);
            int playerIDDisplay = sortedPlayers[i].ID + 1;
            int playerPointsDisplay = playerPoints.ContainsKey(sortedPlayers[i].ID) ? playerPoints[sortedPlayers[i].ID] : 0;

            TextMeshProUGUI textComponent = playerListItem.GetComponentInChildren<TextMeshProUGUI>();
            Image imageComponent = playerListItem.GetComponentInChildren<Image>();
            
            if (textComponent != null)
            {
                textComponent.text = $"{playerIDDisplay}: {sortedPlayers[i].name} - {playerPointsDisplay} pts";
                Debug.Log($"Updated UI for player {sortedPlayers[i].ID}: {textComponent.text}");
            }
            
            if (imageComponent != null)
            {
                imageComponent.color = sortedPlayers[i].color;
            }
        }
    }
    void HandleCollision(Collider2D collider, int playerId)
    {
        if (!IsServer) return;
        
        Debug.Log("Collision detected with player " + playerId + " and object " + collider.name);
        if (collider.tag == "Player" || collider.tag == "Wall")
        {
            if (alivePlayers.Contains(playerId))
            {
                alivePlayers.Remove(playerId);
                
                // Update player list UI
                DisplayPlayers(playerId);
                if (alivePlayers.Count == 1)
                {
                    Debug.Log("Player " + alivePlayers[0] + " won!");
                    lastWinner.Value = alivePlayers[0];
                    ShowWinnerClientRpc(alivePlayers[0]);
                    
                    FreezePlayers();
                }
            }
        }
    }

    
    [ClientRpc]
    void ShowWinnerClientRpc(int winningPlayer)
    {
        winnerText.gameObject.SetActive(true);
        winnerText.text = "Player " + (winningPlayer + 1) + " won!";
    }
    
    public void UnfreezePlayers()
    {
        if (!IsServer) return;
        
        InitializeAlivePlayers();
        
        UnfreezePlayersClientRpc();
        
        spawnPowerUp.Value = true;
        gameInProgress.Value = true;
    }
        
    [ClientRpc]
    void UnfreezePlayersClientRpc()
    {
        SnakeMovement[] snakeMovements = FindObjectsByType<SnakeMovement>(FindObjectsSortMode.None);
        foreach (SnakeMovement snakeMovement in snakeMovements)
        {
            snakeMovement.Unfreeze();
        }
    }
    
    public void resetPlayers()
    {
        if (IsServer)
        {
            // Clear all existing tails first
            ResetAllTailsServerRpc();
        }
        
        // Then setup for new round
        SetupNewRoundClientRpc();
    }
    
    [ClientRpc]
    void SetupNewRoundClientRpc()
    {
        winnerText.gameObject.SetActive(false);
        
        foreach (PlayerData playerData in playerList.players)
        {
            // Only create new tails here, positions are already set
            playerData.gObject.GetComponentInChildren<SnakeMovement>().CreateNewTailOnly();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    void ResetAllTailsServerRpc()
    {
        SnakeMovement[] snakeMovements = FindObjectsByType<SnakeMovement>(FindObjectsSortMode.None);
        foreach (SnakeMovement snakeMovement in snakeMovements)
        {
            // Force clean up of tails on the server first
            snakeMovement.ServerCleanupTailsServerRpc();
        }
    }
    
    [ClientRpc]
    public void ResetPlayersPositionsClientRpc()
    {
        SnakeMovement[] snakeMovements = FindObjectsByType<SnakeMovement>(FindObjectsSortMode.None);
        foreach (SnakeMovement snakeMovement in snakeMovements)
        {
            if (snakeMovement.currentTail != null)
            {
                Destroy(snakeMovement.currentTail);
                snakeMovement.currentTail = null;
            }
            
            snakeMovement.ResetPositionOnly();
            
            // Make sure they're frozen
            snakeMovement.Freeze();
        }
    }
    
    public void deleteAllPlayers()
    {
        if (!IsServer) return; 
        
        DeleteAllPlayersClientRpc();
        
        alivePlayers.Clear();
        spawnPowerUp.Value = false;
        gameInProgress.Value = false;
    }
    
    [ClientRpc]
    void DeleteAllPlayersClientRpc()
    {
        winnerText.gameObject.SetActive(false);
        
        foreach (GameObject powerUp in powerUps)
        {
            Destroy(powerUp);
        }
        powerUps.Clear();
        foreach (Transform child in playerlistPanel.transform)
        {
            Destroy(child.gameObject);
        }
        
        foreach (PlayerData playerData in playerList.players)
        {
            foreach(Transform child in playerData.gObject.transform)
            {

                Destroy(child.gameObject);
                
            }
            Destroy(playerData.gObject);
        }
    }
    
    [ServerRpc]
    void DeleteAllPlayersFromMenuServerRpc()
    {
        List<Button> deletePlayerButtons = new List<Button>(FindObjectsByType<Button>(FindObjectsSortMode.None));
        foreach (Button button in deletePlayerButtons)
        {
            if (button.tag == "DeletePlayerButton")
            {
                button.onClick.Invoke();
            }
        }
    }

    public void FreezePlayers()
    {
        if (!IsServer) return;
        
        FreezePlayersClientRpc();
        
        spawnPowerUp.Value = false;
        gameInProgress.Value = false;
    }
    
    [ClientRpc]
    void FreezePlayersClientRpc()
    {
        foreach (GameObject powerUp in powerUps)
        {
            Destroy(powerUp);
        }
        powerUps.Clear();
        
        SnakeMovement[] snakeMovements = FindObjectsByType<SnakeMovement>(FindObjectsSortMode.None);
        foreach (SnakeMovement snakeMovement in snakeMovements)
        {
            snakeMovement.Freeze();
        }
    }
    
    #endregion
    
    #region Power-ups
    
    public void spawnPowerUps()
    {
        if (!IsServer) return;
        
        if (spawnPowerUp.Value == true)
        {
            int randomPowerUp = Random.Range(0, 14);

            Vector3 spawnPosition = new Vector3(Random.Range(-4f, 4f), Random.Range(-4f, 4f), 0);
            SpawnPowerUpClientRpc(randomPowerUp, spawnPosition);
            
            int randomSpawnTime = Random.Range(5, 10);
            Invoke("spawnPowerUps", randomSpawnTime);
        }
    }
    
    [ClientRpc]
    void SpawnPowerUpClientRpc(int powerUpType, Vector3 position)
    {
        GameObject powerUpPrefab = null;
        switch (powerUpType)
            {
                case 0:
                    powerUpPrefab = doubleSpeedPrefab;
                    break;
                case 1:
                    powerUpPrefab = halfSpeedPrefab;
                    break;
                case 2:
                    powerUpPrefab = invincibilityPlayerPrefab;
                    break;
                case 3:
                    powerUpPrefab = invincibilityWallPrefab;
                    break;
                case 4:
                    powerUpPrefab = resetTailPrefab;
                    break;
                case 5:
                    powerUpPrefab = invertControlsOthersPrefab;
                    break;
                case 6:
                    powerUpPrefab = halfSpeedOthersPrefab;
                    break;
                case 7:
                    powerUpPrefab = doubleSpeedOthersPrefab;
                    break;
                case 8:
                    powerUpPrefab = resetAllTailsPrefab;
                    break;
                case 9:
                    powerUpPrefab = invincibilityWallAllPrefab;
                    break;
                case 10:
                    powerUpPrefab = invincibilityPlayerAllPrefab;
                    break;
                case 11:
                    powerUpPrefab = ninetyDegreeControlsPrefab;
                    break;
                case 12:
                    powerUpPrefab = invincibilityPlayerPrefabAll;
                    break;
                case 13:
                    powerUpPrefab = doubleSizePrefab;
                    break;
                case 14:
                    powerUpPrefab = halfSizePrefab;
                    break;
            }
        GameObject newPowerUp = Instantiate(powerUpPrefab, position, Quaternion.identity);
        powerUps.Add(newPowerUp);
    }
    
    #endregion
    
    #region Pause/Unpause
    
    [ServerRpc(RequireOwnership = false)]
    void RequestUnpauseServerRpc()
    {
        StartCoroutine(waitForCountdown());
    }

    [ClientRpc]
    void PauseGameClientRpc()
    {
        pauseText.SetActive(true);
        Time.timeScale = 0f;
    }
    
    public IEnumerator waitForCountdown()
    {
        pauseText.SetActive(false);
        ShowCountdownClientRpc();
        yield return new WaitForSecondsRealtime(1.5f);
        ResumeGameClientRpc();
    }
    
    [ClientRpc]
    void ShowCountdownClientRpc()
    {
        pauseText.SetActive(false);
        countDown.SetActive(true);
    }
    
    [ClientRpc]
    void ResumeGameClientRpc()
    {
        countDown.SetActive(false);
        Time.timeScale = 1f;
    }
    
    #endregion
}