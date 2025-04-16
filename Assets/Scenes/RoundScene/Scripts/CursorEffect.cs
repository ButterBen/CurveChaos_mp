using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CursorEffect
{

    CursorController cursor = null;
    PowerUpEffect powerUpEffect = null;

    bool initialized = false;
    float effectEndTime = 0f;

    bool ended = false;

    public void Init(PowerUpEffect powerUpEffect, CursorController cursor) {
        this.cursor = cursor;
        this.powerUpEffect = powerUpEffect;
        this.effectEndTime = Time.time + powerUpEffect.GetDurationInSeconds();

        Debug.Log("Applying PowerUpEffect");
        powerUpEffect.ApplyEffect(cursor);
        this.initialized = true;
    }

    // Update needs to be called by cursorController
    public void Update()
    {
        if (initialized && Time.time > effectEndTime) {
            powerUpEffect.RemoveEffect(cursor);
            ended = true;
        }
    }

    public bool HasEnded()
    {
        return ended;
    }
}
