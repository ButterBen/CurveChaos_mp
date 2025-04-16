using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public class SnakeMovement : NetworkBehaviour
{
    public float speed = 1f;
    public float rotationSpeed = 200f;
    public delegate void CollisionEventHandler(Collider2D collider, int playerId);
    public static event CollisionEventHandler OnCollision;
    public int playerId;

    private InputAction m_Left;
    private InputAction m_Right;

    private bool frozen = true;
    private bool inputSet = false;
    public GameObject tail;
    public GameObject currentTail;
    public PlayerList playerList;
    private bool wallInvincible = false;
    private bool playerInvincible = false;
    private bool invertedControls = false;
    private bool nintyDegreeControls = false;
    public NetworkVariable<int> PlayerId = new NetworkVariable<int>();
    Vector3 spawnPoint;
    float nextGapAt = 0.5f;

    public SnakeMovmentSpawner spawner;

    private float currentRotation = 0f;

    private NetworkObject parentNetworkObject;
    public NetworkVariable<NetworkObjectReference> currentTailRef = new NetworkVariable<NetworkObjectReference>(
        default,
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server);


    private void Awake()
    {
        parentNetworkObject = GetComponentInParent<NetworkObject>();
        playerList = FindFirstObjectByType<PlayerList>();
    }


    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        currentTailRef.OnValueChanged += OnTailReferenceChanged;
        
        if (!currentTailRef.Value.Equals(default(NetworkObjectReference)) && currentTailRef.Value.TryGet(out NetworkObject tailObj))
        {
            OnTailReferenceChanged(default, currentTailRef.Value);
        }
        
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        
        currentTailRef.OnValueChanged -= OnTailReferenceChanged;
    }

    private void OnTailReferenceChanged(NetworkObjectReference previousValue, NetworkObjectReference newValue)
    {
        Debug.Log($"OnTailReferenceChanged called - Player {playerId} - IsServer: {NetworkManager.Singleton.IsServer} - IsClient: {NetworkManager.Singleton.IsClient}");
        
        if (newValue.Equals(default(NetworkObjectReference)))
        {
            Debug.Log($"Player {playerId}: Received default NetworkObjectReference, ignoring");
            return;
        }
        
        if (newValue.TryGet(out NetworkObject tailObj))
        {
            Debug.Log($"TryGet succeeded for player {playerId}, tailObj: {(tailObj != null ? tailObj.name : "null")}");
            if (tailObj != null)
            {
                currentTail = tailObj.gameObject;
                Debug.Log($"Player {playerId} assigned currentTail: {currentTail.name}");
                
                // Make sure the tail knows about this head
                Tail tailComponent = currentTail.GetComponent<Tail>();
                if (tailComponent != null)
                {
                    tailComponent.SnakeHead = this.gameObject;
                    tailComponent.gameObject.GetComponent<LineRenderer>().material = playerList.playerMaterials[playerId];
                    tailComponent.SetUpLine();
                    Debug.Log($"Set up tail for player {playerId} with SnakeHead reference");
                }
                else
                {
                    Debug.LogError($"Player {playerId}: Tail component not found on assigned tail");
                }
            }
        }
        else
        {
            Debug.LogError($"Player {playerId}: TryGet failed for NetworkObjectReference");
        }
    }

    public void SetUpPlayer(int id, Vector3 spawn)
    {
        playerId = id;
        spawnPoint = spawn;
        this.transform.position = spawnPoint;
        Debug.Log(playerList.playerNames.Count);
        Color originalColor = playerList.playerColors[playerId];
        Color.RGBToHSV(originalColor, out float h, out float s, out float v);
        s *= 0.5f;
        Color desaturatedColor = Color.HSVToRGB(h, s, v);
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.color = desaturatedColor;
       // tail.gameObject.GetComponent<LineRenderer>().material = playerList.playerMaterials[playerId];
    }

    public void ResetTailAndSpawn()
    {
        ResetPositionOnly();
        CreateNewTailOnly();
    }
    public void ResetPositionOnly()
    {
        spawnPoint = new Vector3(Random.Range(-4f, 4f), Random.Range(-4f, 4f), 0f);
        
        transform.position = spawnPoint;
        
        currentRotation = 0f;
        transform.rotation = Quaternion.Euler(0, 0, currentRotation);
        
        speed = 1f;
        rotationSpeed = 200f;
        
        wallInvincible = false;
        playerInvincible = false;
        invertedControls = false;
        nintyDegreeControls = false;
    }
    public void CreateNewTailOnly()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            SpawnTailLocally();
        }
        else if (IsClientOwner())
        {
            spawner.SpawnTailLocallySPAWNERServerRpc();
        }
        
        nextGapAt = GenerateNextGapStartTime();
    }
    private IEnumerator DelayedTailSpawn()
    {
        yield return new WaitForSeconds(0.2f);
        
        if (NetworkManager.Singleton.IsServer)
        {
            SpawnTailLocally();
        }
        else if (IsClientOwner())
        {
            spawner.SpawnTailLocallySPAWNERServerRpc();
        }
        
        nextGapAt = GenerateNextGapStartTime();
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void ServerCleanupTailsServerRpc()
    {
        Debug.Log($"ServerCleanupTailsServerRpc called for player {playerId}");
        
        if (currentTail != null)
        {
            NetworkObject tailNetObj = currentTail.GetComponent<NetworkObject>();
            if (tailNetObj != null && tailNetObj.IsSpawned)
            {
                tailNetObj.Despawn();
                currentTail = null;
                currentTailRef.Value = default;
            }
        }

        CleanupAllTailsClientRpc(playerId);
        
        StartCoroutine(DelayedSpawnAfterCleanup());
    }

    private IEnumerator DelayedSpawnAfterCleanup()
    {
        yield return null; // Wait a frame
        SpawnTailLocally();
    }
    [ClientRpc]
    private void CleanupAllTailsClientRpc(int playerToClean)
    {
        Debug.Log($"CleanupAllTailsClientRpc called for player {playerToClean}");
        
        Tail[] allTails = FindObjectsByType<Tail>(FindObjectsSortMode.None);
        
        foreach (Tail t in allTails)
        {
            if (t.SnakeHead == null) continue;
            
            SnakeMovement headMovement = t.SnakeHead.GetComponent<SnakeMovement>();
            if (headMovement != null && headMovement.playerId == playerToClean)
            {
                Debug.Log($"Found tail belonging to player {playerToClean}, destroying locally");
                Destroy(t.gameObject);
            }
        }
        
        // Clear the current tail reference
        if (playerId == playerToClean)
        {
            currentTail = null;
        }
    }

    public void ResetTail()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            SpawnTailLocally();
        }
        else if (IsClientOwner())
        {
            spawner.SpawnTailLocallySPAWNERServerRpc();
        }
    }

    private bool IsClientOwner()
    {
        if (parentNetworkObject == null)
        {
            parentNetworkObject = GetComponentInParent<NetworkObject>();
            if (parentNetworkObject == null)
            {
                Debug.LogError("Cannot find parent NetworkObject for ownership check!");
                return false;
            }
        }
        
        bool isOwner = parentNetworkObject.IsOwner;
        Debug.Log($"IsClientOwner check: IsOwner={isOwner}, " +
                  $"OwnerClientId={parentNetworkObject.OwnerClientId}, " +
                  $"LocalClientId={NetworkManager.Singleton.LocalClientId}");
        return isOwner;
    }
    private void SpawnTailLocally()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            Debug.Log("SpawnTailLocally - Server is spawning the tail.");
            NetworkObject tailNetObj = NetworkManager.Singleton.SpawnManager.InstantiateAndSpawn(tail.GetComponent<NetworkObject>());
            currentTail = tailNetObj.gameObject;

            if (tailNetObj != null)
            {
                currentTail.transform.parent = this.transform.parent;
                currentTail.GetComponent<Tail>().SnakeHead = this.transform.gameObject;
                currentTail.GetComponent<Tail>().SetUpLine();

                // Sync the reference to all clients
                currentTailRef.Value = tailNetObj;
            }
        }
        else
        {
            Debug.LogError("SpawnTailLocally - Only the server can spawn network objects!");
        }
    }


    public void SetInput(InputAction left, InputAction right, bool rebind)
    {
        m_Left = left;
        m_Right = right;
        inputSet = true;

        if (!rebind)
        {
            if (NetworkManager.Singleton.IsServer)
            {
                SpawnTailLocally();
            }
            else if (IsClientOwner())
            {
                spawner.SpawnTailLocallySPAWNERServerRpc();
            }

            nextGapAt = GenerateNextGapStartTime();
        }
    }
    private IEnumerator WaitAndResolveTail()
    {
        yield return new WaitForSeconds(.5f);  // Wait for value to sync
        GameObject parent = this.transform.parent.gameObject;
        currentTail = parent.GetComponentInChildren<Tail>().gameObject;
        currentTail.GetComponent<Tail>().SnakeHead = this.gameObject;
    }

    private float NormalizeAngle(float angle)
    {
        angle = angle % 360f;
        if (angle < 0f)
            angle += 360f;
        return angle;
    }
    public int GetPlayerId()
    {
        return playerId;
    }

    bool leftPressedLastFrame = false;
    bool rightPressedLastFrame = false;

    void FixedUpdate()
    {

        if (currentTail == null && currentTailRef.Value.TryGet(out NetworkObject tailObj) && tailObj != null)
        {
            currentTail = tailObj.gameObject;
            Debug.Log($"Client (Player {playerId}) assigned currentTail: {currentTail.name}");
            
            Tail tailComponent = currentTail.GetComponent<Tail>();
            if (tailComponent != null)
            {
                tailComponent.SnakeHead = this.gameObject;
                tailComponent.SetUpLine();
            }
        }
    


        if (inputSet)
        {
            if (nintyDegreeControls)
            {
                if (m_Left.IsPressed() && !leftPressedLastFrame)
                {
                    if (!frozen)
                    {
                        currentRotation = NormalizeAngle(currentRotation + 90f);
                        transform.rotation = Quaternion.Euler(0, 0, currentRotation);
                    }
                    leftPressedLastFrame = true;
                }
                else if (!m_Left.IsPressed())
                {
                    leftPressedLastFrame = false;
                }

                if (m_Right.IsPressed() && !rightPressedLastFrame)
                {
                    if (!frozen)
                    {
                        currentRotation = NormalizeAngle(currentRotation - 90f);
                        transform.rotation = Quaternion.Euler(0, 0, currentRotation);
                    }
                    rightPressedLastFrame = true;
                }
                else if (!m_Right.IsPressed())
                {
                    rightPressedLastFrame = false;
                }
                
                if (!frozen)
                {
                    transform.Translate(Vector2.up * speed * Time.fixedDeltaTime);
                }
            }
            else
            {
                if (invertedControls)
                {
                    if (m_Left.IsPressed())
                    {
                        if (!frozen)
                        {
                            transform.Rotate(Vector3.forward * -rotationSpeed * Time.fixedDeltaTime);
                        }
                    }
                    if (m_Right.IsPressed())
                    {
                        if (!frozen)
                        {
                            transform.Rotate(Vector3.forward * rotationSpeed * Time.fixedDeltaTime);
                        }
                    }
                }
                else
                {
                    if (m_Left.IsPressed())
                    {
                        if (!frozen)
                        {
                            transform.Rotate(Vector3.forward * rotationSpeed * Time.fixedDeltaTime);
                        }
                    }
                    if (m_Right.IsPressed())
                    {
                        if (!frozen)
                        {
                            transform.Rotate(Vector3.forward * -rotationSpeed * Time.fixedDeltaTime);
                        }
                    }
                }

                if (!frozen)
                {
                    transform.Translate(Vector2.up * speed * Time.fixedDeltaTime);
                }
            }

            if (!frozen && Time.time > nextGapAt)
            {
                currentTail.GetComponent<Tail>().CreateGap();
                nextGapAt = GenerateNextGapStartTime();
            }
        }
    }



    float GenerateNextGapStartTime() {
        return Time.time + UnityEngine.Random.Range(3.0f, 5.0f);
    }


    void OnTriggerEnter2D(Collider2D col2D)
    {
        Debug.Log("Collision with " + col2D.tag);
        if(col2D.tag == "Wall")
        {
            if (!wallInvincible)
            {
                speed = 0f;
                rotationSpeed = 0f;
                frozen = true;
                OnCollision?.Invoke(col2D, playerId);
            }
            else
            {
                currentTail.GetComponent<Tail>().CreateGap();
                Vector3 currentPosition = this.transform.position;
                switch (col2D.name)
                {
                    case "WallTop":
                        currentPosition.y = -4.862926f;
                        break;
                    case "WallBottom":
                        currentPosition.y = 4.87089f;
                        break;
                    case "WallLeft":
                        currentPosition.x = 4.853698f;
                        break;
                    case "WallRight":
                        currentPosition.x = -4.856332f;
                        break;
                }
                this.transform.position = currentPosition;
            }
        }
        if (!playerInvincible && col2D.tag == "Player")
        {
            speed = 0f;
            rotationSpeed = 0f;
            frozen = true;
            OnCollision?.Invoke(col2D, playerId);
        }
    }
    public void Freeze()
    {
        frozen = true;
    }
    public void Unfreeze()
    {
        Debug.Log("Unfreeze called" + playerId);
        frozen = false;
    }

    #region PowerUps
    public void DoubleSpeedPowerUp()
    {
        speed *= 2f;
        rotationSpeed *= 1.5f;
        Invoke("ResetPowerUp", 5f); 
    }
    public void HalfSpeedPowerUp()
    {
        speed *= 0.5f;
        rotationSpeed *= 0.5f;
        Invoke("ResetPowerUp", 5f);
    }
    public void ResetTailPowerUp()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            ServerCleanupTailsServerRpc();
            StartCoroutine(DelayedTailSpawn());
        }
        else if (IsClientOwner())
        {
            ServerCleanupTailsServerRpc();
        }
    }
    public void InvincibilityPowerUp()
    {
        playerInvincible = true;
        StartCoroutine(headblinker());
        Invoke("ResetPowerUp", 5f);
        
    }
    IEnumerator headblinker()
    {
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        Color originalColor = spriteRenderer.color;
        float blinkDuration = 4.9f;
        float blinkInterval = 0.245f;
        for (float t = 0; t < blinkDuration; t += blinkInterval *2f)
        {
            Color tempColor = spriteRenderer.color;
            tempColor.a = 0.25f;
            spriteRenderer.color = tempColor;
            yield return new WaitForSeconds(blinkInterval);
            spriteRenderer.color = originalColor;
            yield return new WaitForSeconds(blinkInterval);
        }
    }
    public void WallInvincibilityPowerUp(List<GameObject> walls)
    {
        StartCoroutine(WallBlinker(walls));
        wallInvincible = true;
        Invoke("ResetPowerUp", 5f); 
    }

    IEnumerator WallBlinker(List<GameObject> walls)
    {
        float blinkDuration = 4.9f;
        float blinkInterval = 0.245f;
        List<SpriteRenderer> renderers = new List<SpriteRenderer>();

        foreach (GameObject wall in walls)
        {
            if (wall != null)
            {
                renderers.Add(wall.GetComponent<SpriteRenderer>());
            }
        }

        List<Color> originalColors = new List<Color>();
        foreach (var renderer in renderers)
        {
            originalColors.Add(renderer.color);
        }

        for (float t = 0; t < blinkDuration; t += blinkInterval * 2f)
        {
            foreach (var renderer in renderers)
            {
                Color tempColor = renderer.color;
                tempColor.a = 0.5f;
                renderer.color = tempColor;
            }
            yield return new WaitForSeconds(blinkInterval);

            for (int i = 0; i < renderers.Count; i++)
            {
                renderers[i].color = originalColors[i];
            }
            yield return new WaitForSeconds(blinkInterval);
        }
    }
    
    public void InvertControlsPowerUp()
    {
        invertedControls = true;
        Invoke("ResetPowerUp", 5f);
    }
    public void NinetyDegreeControlsPowerUp()
    {
        nintyDegreeControls = true;
        Invoke("ResetPowerUp", 5f);
    }
    public void DoubleSizePowerUp()
    {
        currentTail.GetComponent<Tail>().CreateGap();
        StartCoroutine(waitForGapChangeSize(2f, 2f, 2f));
        Invoke("ResetSizePowerUpDouble", 5f);
    }
    public void HalfSizePowerUp()
    {
        currentTail.GetComponent<Tail>().CreateGap();
        StartCoroutine(waitForGapChangeSize(0.5f, 0.5f, 1f));
        Invoke("ResetSizePowerUpHalf", 5f);
    }
    void ResetSizePowerUpDouble()
    {
        currentTail.GetComponent<Tail>().CreateGap();
        StartCoroutine(waitForGapChangeSize(0.5f, .5f, 0.5f));
    }
    void ResetSizePowerUpHalf()
    {
        currentTail.GetComponent<Tail>().CreateGap();
        StartCoroutine(waitForGapChangeSize(2f, 2f,1f));
    }
    IEnumerator waitForGapChangeSize(float widthMultiplier, float scaleMultiplier, float pointSpacingMultiplier)
    {
        yield return new WaitForSeconds(0.4f);
        currentTail.GetComponent<LineRenderer>().startWidth *= widthMultiplier;
        currentTail.GetComponent<Tail>().pointSpacing *= pointSpacingMultiplier;
        currentTail.GetComponent<EdgeCollider2D>().edgeRadius *= widthMultiplier;
        this.transform.localScale *= scaleMultiplier;
    }
    void ResetPowerUp()
    {
        nintyDegreeControls = false;
        invertedControls = false;
        speed = 1f;
        rotationSpeed = 200f;
        playerInvincible = false;
        wallInvincible = false;
    }
    #endregion
}