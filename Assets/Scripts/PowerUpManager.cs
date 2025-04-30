using System.Collections.Generic;
using UnityEngine;
public class PowerUpManager : MonoBehaviour
{
    public enum PowerUpType
    {
        DoubleSpee,
        HalfSpeed,
        ResetTail,
        InvincibilityPlayer,
        InvertControlsOthers,
        InvincibilityWall,
        HalfSpeedOthers,
        DoubleSpeedOthers,
        ResetAllTails,
        InvincibilityWallAll,
        InvincibilityPlayerAll,
        NintyDegreeTurnsOnly,
        NintyDegreeTurnsOnlyOthers,
        DoubleSize,
        HalfSize,
    }
    public PowerUpType powerUpType;
    public PlayerList playerList;
    private List<GameObject> walls = new List<GameObject>();
    void Start()
    {
        playerList = FindFirstObjectByType<PlayerList>();
        if(powerUpType == PowerUpType.InvincibilityWall || powerUpType == PowerUpType.InvincibilityWallAll)
        {
            GameObject[] wallObjects = GameObject.FindGameObjectsWithTag("Wall");
            foreach (GameObject wall in wallObjects)
            {
                walls.Add(wall);
            }
        }
    }
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            SnakeMovement snakeMovement = other.GetComponent<SnakeMovement>();
            switch (powerUpType)
            {
                case PowerUpType.DoubleSpee:
                    snakeMovement.DoubleSpeedPowerUp();
                    break;
                case PowerUpType.HalfSpeed:
                    snakeMovement.HalfSpeedPowerUp();
                    break;
                case PowerUpType.ResetTail:
                    snakeMovement.ResetTailPowerUp();
                    break;
                case PowerUpType.InvincibilityPlayer:
                    snakeMovement.InvincibilityPowerUp();
                    break;
                case PowerUpType.InvincibilityWall:
                    snakeMovement.WallInvincibilityPowerUp(walls, false);
                    break;
                case PowerUpType.InvertControlsOthers:
                    InvertControllsOtherPlayers(other);
                    break;
                case PowerUpType.HalfSpeedOthers:
                    TriggerHalfSpeedOthers(other);
                    break;
                case PowerUpType.DoubleSpeedOthers:
                    TriggerDoubleSpeedOthers(other);
                    break;
                case PowerUpType.ResetAllTails:
                    ResetAllTails(other);
                    break;
                case PowerUpType.InvincibilityWallAll:
                    MakeInvincibleWallAllPlayers(other);
                    break;
                case PowerUpType.InvincibilityPlayerAll:
                    MakeInvincibleAllPlayers(other);
                    break;
                case PowerUpType.NintyDegreeTurnsOnly:
                    snakeMovement.NinetyDegreeControlsPowerUp();
                    break;
                case PowerUpType.NintyDegreeTurnsOnlyOthers:
                    NintyDegreeTurnsOnlyOtherPlayers(other);
                    break;
                case PowerUpType.DoubleSize:
                    snakeMovement.DoubleSizePowerUp();
                    break;
                case PowerUpType.HalfSize:
                    snakeMovement.HalfSizePowerUp();
                    break;
                default:
                    UnityEngine.Debug.LogWarning("Unknown PowerUpType: " + powerUpType);
                    break;
            }

            Destroy(gameObject); 
        }
    }

    void TriggerHalfSpeedOthers(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            foreach (PlayerData player in playerList.players)
            {
                if (player.gObject != other.transform.parent.gameObject)
                {
                    SnakeMovement otherSnakeMovement = player.gObject.GetComponentInChildren<SnakeMovement>();
                    otherSnakeMovement.HalfSpeedPowerUp();
                }
            }
            Destroy(gameObject); 
        }
    }
    void TriggerDoubleSpeedOthers(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            foreach (PlayerData player in playerList.players)
            {
                if (player.gObject != other.transform.parent.gameObject)
                {
                    SnakeMovement otherSnakeMovement = player.gObject.GetComponentInChildren<SnakeMovement>();
                    otherSnakeMovement.DoubleSpeedPowerUp();
                }
            }
            Destroy(gameObject); 
        }
    }
    void ResetAllTails(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            foreach (PlayerData player in playerList.players)
            {
                SnakeMovement otherSnakeMovement = player.gObject.GetComponentInChildren<SnakeMovement>();
                if (otherSnakeMovement != null)
                {
                    otherSnakeMovement.ResetTailPowerUp();
                }
            }
            Destroy(gameObject);
        }
    }
    void MakeInvincibleAllPlayers(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            foreach (PlayerData player in playerList.players)
            {
                SnakeMovement otherSnakeMovement = player.gObject.GetComponentInChildren<SnakeMovement>();
                otherSnakeMovement.InvincibilityPowerUp();
            }
            Destroy(gameObject); 
        }
    }
    void MakeInvincibleWallAllPlayers(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            foreach (PlayerData player in playerList.players)
            {
                SnakeMovement otherSnakeMovement = player.gObject.GetComponentInChildren<SnakeMovement>();
                otherSnakeMovement.WallInvincibilityPowerUp(walls, true);
            }
            Destroy(gameObject); 
        }
    }

    void InvertControllsOtherPlayers(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            foreach (PlayerData player in playerList.players)
            {
                if (player.gObject != other.transform.parent.gameObject)
                {
                    SnakeMovement otherSnakeMovement = player.gObject.GetComponentInChildren<SnakeMovement>();
                    otherSnakeMovement.InvertControlsPowerUp();
                }
            }
            Destroy(gameObject); 
        }
    }

    void NintyDegreeTurnsOnlyOtherPlayers(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            foreach (PlayerData player in playerList.players)
            {
                if (player.gObject != other.transform.parent.gameObject)
                {
                    SnakeMovement otherSnakeMovement = player.gObject.GetComponentInChildren<SnakeMovement>();
                    otherSnakeMovement.NinetyDegreeControlsPowerUp();
                }
            }
            Destroy(gameObject); 
        }
    }
}
