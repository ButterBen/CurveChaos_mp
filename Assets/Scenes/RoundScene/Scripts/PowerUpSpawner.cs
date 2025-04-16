using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PowerUpSpawner : MonoBehaviour
{

    public PowerUp powerUp;

    public float nextPowerUpSpawn = 5f;

    void Start() {
        nextPowerUpSpawn = Time.time + Random.Range(3,6);
    }

    // Update is called once per frame
    void Update()
    {
        if (Time.time > nextPowerUpSpawn) {
            PowerUp p = Instantiate(powerUp);
            float x = Random.Range(-35, 60);
            float y = Random.Range(-35, 35);
            p.transform.position = new Vector3(x, y, 0);

            nextPowerUpSpawn = Time.time + Random.Range(2, 12);
        }
    }
}
