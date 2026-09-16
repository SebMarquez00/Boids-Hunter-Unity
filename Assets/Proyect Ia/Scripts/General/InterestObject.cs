using System.Collections.Generic;
using UnityEngine;

public class InterestObject : MonoBehaviour
{
    public static List<InterestObject> AllObjects =
        new List<InterestObject>();

    [Header("Health")]
    [SerializeField] private float _maxHealth = 20f;
    [SerializeField] private float _currentHealth;

    public bool IsAlive => _currentHealth > 0f;

    private void OnEnable()
    {
        _currentHealth = _maxHealth;

        AllObjects.Add(this);
    }

    private void OnDisable()
    {
        AllObjects.Remove(this);
    }

    public void TakeDamage(float damage)
    {
        if (!IsAlive || damage <= 0f)
        {
            return;
        }

        _currentHealth = Mathf.Max(
            0f,
            _currentHealth - damage
        );

        if (!IsAlive)
        {
            Destroy(gameObject);
        }
    }
}