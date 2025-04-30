using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

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
    public TMP_Text endScoreText;
    public GameObject moreRoundsButton;
    public GameObject lessRoundsButton;
    public GameObject roundsInputField;
    public GameObject pauseText;
    public GameObject gameEndPanel;
    public GameObject newGameButton;
    
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
                textComponent.text = $"{playerIDDisplay}: {player.name} - {playerPointsDisplay}";
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
        yield return new WaitForSeconds(1f);
        
        SynchronizePlayerListsServerRpc();
    }
    private void OnPlayerPointsChanged(Dictionary<int, int> previousValue, Dictionary<int, int> newValue)
    {
        RefreshPlayerListUI();
    }

    public override void OnNetworkDespawn()
    {
        spawnPowerUp.OnValueChanged -= OnSpawnPowerUpChanged;
        lastWinner.OnValueChanged -= OnLastWinnerChanged;
        networkPlayerPoints.OnValueChanged -= OnPlayerPointsChanged;
        
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
        if (GameSceneObject.activeSelf)
        {
            bool pausePressed = 
                (Keyboard.current != null && Keyboard.current[Key.Escape].wasPressedThisFrame) ||
                (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);

            if (pausePressed)
            {
                if (Time.timeScale == 1f)
                {
                    PauseGameClientRpc();
                }
                else if (Time.timeScale == 0f)
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
    public void NewGameButton()
    {
        ToggleGameEndPanelClientRPC();
        ResetPointsServerRpc();
        if (!IsServer)
        {
            Debug.Log("Only the host can start a new game");
            return;
        }
        
        networkRounds.Value = networkRounds.Value = int.Parse(roundsNumberText.text);
        networkPlayerPoints.Value = new Dictionary<int, int>();
        ResetPlayersPositionsClientRpc();
        InitializeAlivePlayers();
        
        StartCoroutine(DelayThenNextRound());
    }
    
    public void NextRoundButton()
    {
        networkRounds.Value--;
        if (!IsServer)
        {
            Debug.Log("Only the host can advance rounds");
            return;
        }
        
        Debug.Log("Next Round Button Pressed. Rounds left: " + networkRounds.Value);
        if(networkRounds.Value <= 0)
        {
            SetupNewRoundClientRpc();
            ToggleGameEndPanelClientRPC();
            return;
        }
        
        
        ResetPlayersPositionsClientRpc();
        InitializeAlivePlayers();
        
        StartCoroutine(DelayThenNextRound());
    }
    [ServerRpc(RequireOwnership = false)]
    public void ResetPointsServerRpc()
    {
        foreach (PlayerData playerData in playerList.players)
        {
            if (playerPoints.ContainsKey(playerData.ID))
            {
                playerPoints[playerData.ID] = 0;
            }
        }
        
        networkPlayerPoints.Value = new Dictionary<int, int>(playerPoints);
    }
    [ClientRpc]
    public void ToggleGameEndPanelClientRPC()
    {
                                    EventSystem.current.SetSelectedGameObject(newGameButton);
                            Debug.Log("CURRENT SELECTED"+EventSystem.current.currentSelectedGameObject.name);
        winnerText.gameObject.SetActive(false);
        endScoreText.text = "";

        List<PlayerData> sortedPlayers = new List<PlayerData>(playerList.players);
        
        sortedPlayers.Sort((a, b) =>
        {
            int pointsA = playerPoints.ContainsKey(a.ID) ? playerPoints[a.ID] : 0;
            int pointsB = playerPoints.ContainsKey(b.ID) ? playerPoints[b.ID] : 0;
            return pointsB.CompareTo(pointsA); 
        });

        for (int i = 0; i < sortedPlayers.Count; i++)
        {
            int rank = i + 1;
            var player = sortedPlayers[i];
            Color color = player.color;
            string hexColor = ColorUtility.ToHtmlStringRGB(color); 
            string playerNameColored = $"<color=#{hexColor}>{player.name}</color>";
            int playerScore = playerPoints.ContainsKey(player.ID) ? playerPoints[player.ID] : 0;

            endScoreText.text += $"{rank}. {playerNameColored}: {playerScore}\n";
        }

        gameEndPanel.SetActive(!gameEndPanel.activeSelf);
    }


    
    private IEnumerator DelayThenNextRound()
    {
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
          //  ForceDisconnectAllClientsClientRpc();
            
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
        pauseText.SetActive(false);
        
        // Reset game state
        FreezePlayersClientRpc();
        deleteAllPlayers();
        
        GameSceneObject.SetActive(false);   
        LobbySceneObject.SetActive(true);
        
        // Clear local player points
        playerPoints.Clear();
        if(IsServer)
        {
            networkPlayerPoints.Value = new Dictionary<int, int>();
        }

    }
    
    #endregion
    
    #region Player Management
    
    
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
    
    private List<int> deathOrderList = new List<int>();
    private Dictionary<int, bool> hasReceivedPoints = new Dictionary<int, bool>();

    public void DisplayPlayers(int playerID)
    {
        if (IsServer)
        {
            // Server-side logic to update player lists and calculate points
            if (playerID == -1)
            {
                // Reset everything for a new round
                deadPlayers = new List<int>();
                deathOrderList = new List<int>();
                hasReceivedPoints = new Dictionary<int, bool>();
                
                // Initialize all players as not having received points yet
                foreach (PlayerData player in playerList.players)
                {
                    hasReceivedPoints[player.ID] = false;
                    
                    // Initialize player points if needed
                    if (!playerPoints.ContainsKey(player.ID))
                        playerPoints[player.ID] = 0;
                }
            }
            else
            {
                // Record this player's death
                deadPlayers.Add(playerID);
                deathOrderList.Add(playerID);
                
                // Assign points for this death specifically
                AssignPointsForDeath(playerID);
                
                // Notify all clients about player death
                UpdateDeadPlayersClientRpc(playerID);
            }
            
            // Update all clients with the new player state
            UpdatePlayerListClientRpc();
        }
    }

    private void AssignPointsForDeath(int deadPlayerId)
    {
        int totalPlayers = playerList.players.Count;
        
        int deathPosition = deathOrderList.Count - 1; 
        
        if (!hasReceivedPoints[deadPlayerId])
        {
            playerPoints[deadPlayerId] += deathPosition;
            hasReceivedPoints[deadPlayerId] = true;
            
            Debug.Log($"Player {deadPlayerId} died in position {deathPosition+1} and received {deathPosition} points");
        }
        
        // Check if we have a winner (only one player left)
        List<int> alivePlayers = GetAlivePlayers();
        
        if (alivePlayers.Count == 1)
        {
            int winnerID = alivePlayers[0];
            
            // Winner gets max points (total players - 1)
            if (!hasReceivedPoints[winnerID])
            {
                playerPoints[winnerID] += (totalPlayers - 1);
                hasReceivedPoints[winnerID] = true;
                
                Debug.Log($"Player {winnerID} is the winner and received {totalPlayers-1} points");
            }
        }
        
        // Update network points
        Dictionary<int, int> updatedNetworkPoints = new Dictionary<int, int>();
        foreach (var pair in playerPoints)
        {
            updatedNetworkPoints[pair.Key] = pair.Value;
        }
        networkPlayerPoints.Value = updatedNetworkPoints;
    }

    private List<int> GetAlivePlayers()
    {
        List<int> alive = new List<int>();
        foreach (PlayerData player in playerList.players)
        {
            if (!deadPlayers.Contains(player.ID))
            {
                alive.Add(player.ID);
            }
        }
        return alive;
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
                textComponent.text = $"{playerIDDisplay}: {sortedPlayers[i].name} - {playerPointsDisplay}";
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
                    RequestScaleDownWinnerTextServerRpc();
                    RequestWaitAfterRoundServerRPC();
                    if(networkRounds.Value > 0)
                    {
                        if (networkRounds.Value <= 0)
                        {
                            // Game over, show end screen
                            NewGameButton();
                        }
                        else
                        {
                            NextRoundButton();
                        }
                    }
                }
            }
        }
    }
    [ServerRpc(RequireOwnership = false)]
    void RequestWaitAfterRoundServerRPC()
    {
        StartCoroutine(waitForSeconds(1.5f));
    }
    IEnumerator waitForSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);
    }

    
    [ClientRpc]
    void ShowWinnerClientRpc(int winningPlayer)
    {
        winnerText.gameObject.SetActive(true);
        winnerText.transform.localScale = new Vector3(2f, 2f, 2f);
        winnerText.text = "Player " + (winningPlayer + 1) + " won!";
        winnerText.color = playerList.players[winningPlayer].color;
    }
    [ServerRpc(RequireOwnership = false)]
    void RequestScaleDownWinnerTextServerRpc()
    {
        StartCoroutine(ScaleDownWinnerText(winnerText.transform, 1.5f));
    }
    IEnumerator ScaleDownWinnerText(Transform textTransform, float duration)
    {
        Vector3 originalScale = new Vector3(2f, 2f, 2f);
        Vector3 targetScale = new Vector3(.5f, .5f, .5f);
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            textTransform.localScale = Vector3.Lerp(originalScale, targetScale, elapsedTime / duration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        textTransform.localScale = targetScale;
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
        playerPoints.Clear();
        networkPlayerPoints.Value = new Dictionary<int, int>();
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