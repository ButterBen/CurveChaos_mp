using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using Unity.Netcode;
using Unity.VisualScripting;

[RequireComponent(typeof(LineRenderer))]
[RequireComponent(typeof(EdgeCollider2D))]
public class Tail : NetworkBehaviour
{
    public GameObject SnakeHead;
    LineRenderer line;
    EdgeCollider2D col2D;
    List<Vector2> points;
    public bool addPoints = true;
    [SerializeField]
    public float pointSpacing = 0.1f;
    private bool startGame = false;
    public GameObject tailPrefab;
    
    // Number of points to exclude from collision at the head of the snake
    [SerializeField]
    public int colliderPointsOffset = 2;
    
    public NetworkVariable<bool> shouldAddPoints = new NetworkVariable<bool>(true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public void SetUpLine()
    {
        line = GetComponent<LineRenderer>();
        col2D = GetComponent<EdgeCollider2D>();
        points = new List<Vector2>();
        SetPoint();
        startGame = true;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        shouldAddPoints.OnValueChanged += OnShouldAddPointsChanged;

        addPoints = shouldAddPoints.Value;
    }
    private void OnShouldAddPointsChanged(bool previousValue, bool newValue)
    {
        addPoints = newValue;
    }
    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        shouldAddPoints.OnValueChanged -= OnShouldAddPointsChanged;
    }
    void FixedUpdate()
    {
        if(startGame)
        {
            if(addPoints)
            {
                if(Vector3.Distance(points[points.Count - 1], SnakeHead.transform.position) > pointSpacing)
                {
                    SetPoint();
                }
            }
        }
    }
    public void ClearLine()
    {
        line.positionCount = 0;
        col2D.points = new Vector2[0];
        points.Clear();
    }

    void SetPoint()
    {
        // Add the new point to our list and update the line renderer
        points.Add(SnakeHead.transform.position);
        line.positionCount = points.Count;
        line.SetPosition(points.Count - 1, SnakeHead.transform.position);
        
        // Update the edge collider, but only use points that are at least 
        // colliderPointsOffset positions away from the head
        UpdateCollider();
    }
    
    void UpdateCollider()
    {
        // Only update the collider if we have enough points
        if (points.Count > colliderPointsOffset)
        {
            Debug.Log("Updating collider with points count: " + points.Count + " and offset: " + colliderPointsOffset);
            // Create a new array for collider points, excluding the most recent points
            Vector2[] colliderPoints = new Vector2[points.Count - colliderPointsOffset];
            
            // Copy all points except the last colliderPointsOffset ones
            for (int i = 0; i < points.Count - colliderPointsOffset; i++)
            {
                colliderPoints[i] = points[i];
            }
            
            // Update the edge collider with these points
            col2D.points = colliderPoints;
        }
        else if (points.Count > 1)
        {
            // If we don't have enough points yet, use what we have
            //col2D.points = points.GetRange(0, points.Count - 1).ToArray();
        }
    }

    public void EndTail()
    {
        addPoints = false;
        // if (points.Count > 0)
        // {
        //     // Create a new array of the exact size needed
        //     Vector2[] finalColliderPoints = new Vector2[points.Count];
            
        //     // Copy all points to the collider
        //     for (int i = 0; i < points.Count; i++)
        //     {
        //         finalColliderPoints[i] = points[i];
        //     }
            
        //     // Update the edge collider with all points
        //     col2D.points = finalColliderPoints;
        // }
        StartCoroutine(WaitForEndTail());
        if (IsServer)
        {
            shouldAddPoints.Value = false;
        }
        else
        {
            EndTailServerRpc();
        }
    }

    IEnumerator WaitForEndTail()
    {
        yield return new WaitForSeconds(0.4f);
        if (points.Count > 0)
        {
            // Create a new array of the exact size needed
            Vector2[] finalColliderPoints = new Vector2[points.Count];
            
            // Copy all points to the collider
            for (int i = 0; i < points.Count; i++)
            {
                finalColliderPoints[i] = points[i];
            }
            
            // Update the edge collider with all points
            col2D.points = finalColliderPoints;
        }
    }
    [ServerRpc(RequireOwnership = false)]
    private void EndTailServerRpc()
    {
        shouldAddPoints.Value = false;
    }
    public void StartTail()
    {
        if (IsServer)
        {
            shouldAddPoints.Value = true;
        }
        else
        {
            StartTailServerRpc();
        }
    }
    [ServerRpc(RequireOwnership = false)]
    private void StartTailServerRpc()
    {
        shouldAddPoints.Value = true;
    }
    public void SetLineZero()
    {
        line = GetComponent<LineRenderer>();
        col2D = GetComponent<EdgeCollider2D>();
        col2D.points = new Vector2[0];
        points = new List<Vector2>();
        line.positionCount = 0;
        SetPoint();
        startGame = true;
    }
    public void CreateGap() {
        addPoints = false;

        if(!IsServer)
        {
            CreateGapServerRpc();
        }
        else
        {
            EndTail();
            StartCoroutine(WaitForGap());
        }
    }
    public void CreateGap(int colliderPointsOffsetPowerUp) {
        addPoints = false;

        if(!IsServer)
        {
            CreateGapServerRpc();
        }
        else
        {
            EndTail();
            StartCoroutine(WaitForGap(colliderPointsOffsetPowerUp));
        }
    }
    IEnumerator WaitForGap()
    {
        yield return new WaitForSeconds(GapEndTime());

        if (IsServer)
        {
            GameObject newTail = Instantiate(tailPrefab, transform.position, Quaternion.identity);
            Tail tailComponent = newTail.GetComponent<Tail>();
            tailComponent.SetLineZero();
            NetworkObject netObj = newTail.GetComponent<NetworkObject>();
            netObj.Spawn();
            newTail.transform.parent = SnakeHead.transform.parent;

            // Update the references
            SnakeMovement snakeMovement = SnakeHead.GetComponent<SnakeMovement>();
            snakeMovement.currentTailRef.Value = netObj;
            snakeMovement.currentTail = newTail;

            // Initialize the new tail
            tailComponent.SnakeHead = SnakeHead;
            tailComponent.StartTail();

        }
    }
    IEnumerator WaitForGap(int colliderPointsOffsetPowerUp)
    {
        yield return new WaitForSeconds(GapEndTime());

        if (IsServer)
        {
            GameObject newTail = Instantiate(tailPrefab, transform.position, Quaternion.identity);
            Tail tailComponent = newTail.GetComponent<Tail>();
            tailComponent.SetLineZero();
            NetworkObject netObj = newTail.GetComponent<NetworkObject>();
            netObj.Spawn();
            newTail.transform.parent = SnakeHead.transform.parent;

            // Update the references
            SnakeMovement snakeMovement = SnakeHead.GetComponent<SnakeMovement>();
            snakeMovement.currentTailRef.Value = netObj;
            snakeMovement.currentTail = newTail;

            // Initialize the new tail
            tailComponent.SnakeHead = SnakeHead;
            tailComponent.colliderPointsOffset = colliderPointsOffsetPowerUp;
            tailComponent.StartTail();

        }
    }
    [ServerRpc(RequireOwnership = false)]
    public void CreateGapServerRpc()
    {
        EndTail();
        StartCoroutine(WaitForGap());
    }


    float GapEndTime()
    {
        return Random.Range(0.2f, 0.4f);
    }
}