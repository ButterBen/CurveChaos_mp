using System;
using System.Collections.Generic;
using UnityEngine;

public class CursorController : MonoBehaviour
{

    public String inputAxisName = "";

    public Color playerColor;

    public float speed = 7f;
    public float rotationSpeed = 250f;

    public bool controlsInverted = false;

    float horizontalAxis = 0f;

    bool playerLost = false;

    public GameObject tracePrefab;

    Trace currentTraceScript = null;

    List<CursorEffect> activeEffects = new List<CursorEffect>();

    void Start()
    {
        GameObject currentTrace = transform.parent.gameObject.transform.Find("Trace").gameObject;
        LineRenderer lineRenderer = currentTrace.GetComponent<LineRenderer>();
        SetLineRendererColor(lineRenderer);

        currentTraceScript = currentTrace.GetComponent<Trace>();
        InitGap();
    }

    // Update is called once per frame
    void Update()
    {
        horizontalAxis = Input.GetAxisRaw(inputAxisName);
        CreateGap();
    }

    void FixedUpdate() {
        if (!playerLost) {
            transform.Translate(speed * Time.fixedDeltaTime * Vector2.up, Space.Self);

            float inverted = -1.0f;
            if (controlsInverted) inverted = 1.0f;
            transform.Rotate(Vector3.forward * inverted * horizontalAxis * rotationSpeed * Time.fixedDeltaTime);

            activeEffects.ForEach(it => {
                it.Update();
            });

            activeEffects = activeEffects.FindAll(it => {
                return !it.HasEnded();
            });
        }
    }

    void OnTriggerEnter2D(Collider2D collision) {
        if (collision.CompareTag("PlayerTrace")) {
            Die();
        }
        if (collision.CompareTag("Wall")) {
            Die();
        }
        if (collision.CompareTag("PowerUp")) {
            HandlePowerUp(collision);
        }
        Debug.Log($"Collision with: {collision.name}");
    }

    void Die() {
        RoundGameManager roundGameManager = GameObject.FindObjectOfType<RoundGameManager>();
        playerLost = true;

        roundGameManager.OnPlayerDied(gameObject.transform.parent.gameObject);
    }

    void HandlePowerUp(Collider2D collision) {
        PowerUp powerUp = collision.gameObject.GetComponent<PowerUp>();
        PowerUpEffect powerUpEffect = powerUp.Consume();
        CursorEffect cursorEffect = new CursorEffect();
        cursorEffect.Init(powerUpEffect, this);
        activeEffects.Add(cursorEffect);
    }

    // TODO: externalize gap

    float randomGapTime = 0f;
    float gapSize = 1.0f;
    float nextGapAt = 0;

    void CreateGap() {
        if (Time.time > nextGapAt) {

            currentTraceScript.EndTrace();

            if (Time.time > GapEndTime()) {
                GameObject newTrace = Instantiate(tracePrefab, Vector3.zero, Quaternion.identity);
                LineRenderer lineRenderer = newTrace.GetComponent<LineRenderer>();
                SetLineRendererColor(lineRenderer);

                newTrace.transform.parent = gameObject.transform.parent;
                currentTraceScript = newTrace.GetComponent<Trace>();
                currentTraceScript.cursor = this.gameObject.transform;

                nextGapAt = GenerateNextGapStartTime();
            }
        }
    }

    void InitGap() {
         randomGapTime = UnityEngine.Random.Range(3.0f, 5.0f);
         nextGapAt = Time.time + randomGapTime;
    }

    float GenerateNextGapStartTime() {
        return Time.time + UnityEngine.Random.Range(3.0f, 5.0f);
    }

    float GapEndTime() {
        return nextGapAt + gapSize;
    }

    void SetLineRendererColor(LineRenderer lineRenderer) {
        lineRenderer.startColor = playerColor;
        lineRenderer.endColor = playerColor;
    }

}
