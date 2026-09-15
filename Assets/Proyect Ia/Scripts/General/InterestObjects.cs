using System.Collections.Generic;
using UnityEngine;

public class InterestObject : MonoBehaviour
{
    public static List<InterestObject> AllObjects =
        new List<InterestObject>();

    private void OnEnable()
    {
        AllObjects.Add(this);
    }

    private void OnDisable()
    {
        AllObjects.Remove(this);
    }
}