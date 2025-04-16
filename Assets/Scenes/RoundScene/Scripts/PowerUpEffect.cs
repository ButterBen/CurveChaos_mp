using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface PowerUpEffect
{
    
    public float GetDurationInSeconds();

    public void ApplyEffect(CursorController cursor);

    public void RemoveEffect(CursorController cursor);

    public string GetIconName();

}
