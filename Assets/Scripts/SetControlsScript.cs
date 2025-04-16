using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class SetControlsScript : MonoBehaviour
{
    public Button leftButton;
    public Button rightButton;
    public PlayerList playerList;

    public InputAction left;
    public InputAction right;

    private int playerId;

    public void SetPlayerId(int id)
    {
        playerId = id;
    }
    public int GetPlayerId()
    {
        return playerId;
    }

    public void OnLeftButtonClick(InputAction action)
    {
        Debug.Log("Set left control for Player " + playerId);

         var rebindOperation = action.PerformInteractiveRebinding()
                    .WithControlsExcluding("Mouse")
                    .OnMatchWaitForAnother(0.1f)
                    .Start();
        //StartCoroutine(WaitForLeftKeyInput(playerId));
    }
    /*IEnumerator WaitForLeftKeyInput(int player)
    {
        
        yield return new WaitUntil(() => Input.anyKeyDown);
        foreach (KeyCode keyCode in System.Enum.GetValues(typeof(KeyCode)))
        {
            if (Input.GetKeyDown(keyCode))
            {
                if (IsGamepadKeyCode(keyCode))
                {
                    Gamepad gamepad = Gamepad.current;
                    playerList.players[player].leftKey = keyCode;
                    playerList.players[player].gamepadID = gamepad;
                    playerList.players[player].isGamepad = true;
                    Debug.Log("Player " + player + " left key set to " + keyCode + " joystick name: " + gamepad.name);
                }
                else
                {
                    playerList.players[player].leftKey = keyCode;
                    playerList.players[player].isGamepad = false;
                    Debug.Log("Player " + player + " left key set to " + keyCode);
                }
                leftButton.GetComponentInChildren<TMP_Text>().text = "Left Key: " + keyCode.ToString();
                break;
            }
        }
    }*/
    
    public void OnRightButtonClick()
    {
        Debug.Log("Right button clicked for Player " + playerId);
        //StartCoroutine(WaitForRightKeyInput(playerId));
    }

    /*IEnumerator WaitForRightKeyInput(int player)
    {
        yield return new WaitUntil(() => Input.anyKeyDown); 
        foreach (KeyCode keyCode in System.Enum.GetValues(typeof(KeyCode)))
        {
            Debug.Log("Player " + player + " right key set to " + keyCode );
            if (Input.GetKeyDown(keyCode))
            {
                if (IsGamepadKeyCode(keyCode))
                {
                    Gamepad gamepad = Gamepad.current;
                    playerList.players[player].rightKey = keyCode;
                    playerList.players[player].gamepadID = gamepad;
                    playerList.players[player].isGamepad = true;
                    Debug.Log("Player " + player + " right key set to " + keyCode + " joystick name: " + gamepad.name);
                }
                else
                {
                    playerList.players[player].rightKey = keyCode;
                    playerList.players[player].isGamepad = false;
                    Debug.Log("Player " + player + " right key set to " + keyCode);
                }
                rightButton.GetComponentInChildren<TMP_Text>().text = "Right Key: " + keyCode.ToString();
                break;
            }
        }
    }*/
    bool IsGamepadKeyCode(KeyCode keyCode)
    {
        return keyCode.ToString().StartsWith("Joystick") || keyCode.ToString().StartsWith("Gamepad");
    }
}
