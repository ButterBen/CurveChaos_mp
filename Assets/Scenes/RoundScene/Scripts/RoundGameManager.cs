using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using System.Linq;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

public class RoundGameManager : MonoBehaviour
{

    GameObject[] playerList = null;

    List<GameObject> playersAlive = new List<GameObject>();

    public GameObject roundEndedUi;
    public GameObject winnerAnouncementText;
    
    public GameObject playerListUi;

    // Start is called before the first frame update
    void Start()
    {
        // TODO: get a list of all players, track how many are alive 
        playerList = GameObject.FindGameObjectsWithTag("Player");

        Array.ForEach(playerList, it => {
            playersAlive.Add(it);
        });

        for (int i = 0; i < playerList.Length; i++) {
            AddPlayerNameToUi(playerList[i].name);
        }
    }

    public void OnPlayerDied(GameObject playerName) {
        playersAlive.Remove(playerName);
        Debug.Log($"Player {playerName.name} died. Remaining players {playersAlive.Count}.");
        if (playersAlive.Count == 1) {
            EndGame();
        }
    }

    public void EndGame() {
        StartCoroutine(PlayEndGameAnimation());
    }

    IEnumerator PlayEndGameAnimation() {
        ShowRoundEndedUi();
        yield return new WaitForSeconds(3f);

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void ShowRoundEndedUi() {
        roundEndedUi.SetActive(true);
        TextMeshProUGUI winner = winnerAnouncementText.GetComponent<TextMeshProUGUI>();
        Debug.Log(playersAlive.First().gameObject.name);
        winner.SetText($"{playersAlive.First().gameObject.name} wins");
    }

    private void AddPlayerNameToUi(String playername) {
        GameObject textGameObject = new GameObject();
        TextMeshProUGUI text = textGameObject.AddComponent<TextMeshProUGUI>();
        text.SetText(playername);
        textGameObject.transform.SetParent(playerListUi.transform);
    }

}
