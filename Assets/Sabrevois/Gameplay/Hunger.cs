using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class Hunger : MonoBehaviour
{
    [Min(1.0f)]
    public float MaxHunger;
    
    [Tooltip("Hunger gained per second")]
    float HungerIncreaseRate;

    public float CurrentHunger { get; private set; } = 0;
    public float CurrentHunger01 => CurrentHunger / MaxHunger;
    
    private float _timer;

    private void Awake()
    {
        CurrentHunger = 0;
        HungerIncreaseRate = Random.Range(0.3f,1.5f); 
    }

    public void ReduceHunger(float amount)
    {
        CurrentHunger = Mathf.Max(CurrentHunger - amount, 0);
    }
    
    public void IncreaseHunger(float amount)
    {
        CurrentHunger = Mathf.Min(CurrentHunger + amount, MaxHunger);
    }
        
    public void ResetHunger()
    {
        CurrentHunger = 0;
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        if (_timer >= 1f)
        {
            _timer = 0f;
            IncreaseHunger(HungerIncreaseRate);
        }
    }
}
