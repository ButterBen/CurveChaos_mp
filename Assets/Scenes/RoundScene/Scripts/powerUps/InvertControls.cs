using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InvertedControls : PowerUpEffect
{
    public void ApplyEffect(CursorController cursorController)
    {
        if (cursorController) {
            cursorController.controlsInverted = !cursorController.controlsInverted;
        }
    }

    public void RemoveEffect(CursorController cursorController)
    {
        if (cursorController) {
            cursorController.controlsInverted = !cursorController.controlsInverted;
        }
    }

    public float GetDurationInSeconds()
    {
        return 2.0f;
    }

    public string GetIconName() {
        return "InvertedControls";
    }

}
