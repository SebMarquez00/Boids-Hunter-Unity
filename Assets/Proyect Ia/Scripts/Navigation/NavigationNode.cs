using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class NavigationNode
{
    [SerializeField] private Vector3 _position;
    [SerializeField] private bool _isSafeZone;
    [SerializeField] private List<int> _neighbors = new List<int>();

    public Vector3 Position => _position;
    public bool IsSafeZone => _isSafeZone;
    public IReadOnlyList<int> Neighbors => _neighbors;

    public NavigationNode(Vector3 position, bool isSafeZone)
    {
        _position = position;
        _isSafeZone = isSafeZone;
    }

    public void AddNeighbor(int index)
    {
        if (index >= 0 && !_neighbors.Contains(index)) _neighbors.Add(index);
    }
}
