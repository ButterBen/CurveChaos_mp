using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HalfSpeed : PowerUpEffect
{
    public void ApplyEffect(CursorController cursorController)
    {
        if (cursorController) {
            cursorController.speed /= 2;
        }
    }

    public void RemoveEffect(CursorController cursorController)
    {
        if (cursorController) {
            cursorController.speed *= 2;
        }
    }

    public float GetDurationInSeconds()
    {
        return 2.0f;
    }

    public string GetIconName() {
        return "Slow";
    }

}
