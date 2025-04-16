using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using Unity.Netcode;

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

    void SetPoint ()
    {
        if(points.Count > 1)
        {
            col2D.points = points.ToArray();    
        }
        points.Add(SnakeHead.transform.position);
        line.positionCount = points.Count;
        line.SetPosition(points.Count - 1, SnakeHead.transform.position);
    }
    public void EndTail() 
    {
        addPoints = false;
        
        if (IsServer)
        {
            shouldAddPoints.Value = false;
        }
        else
        {
            EndTailServerRpc();
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
