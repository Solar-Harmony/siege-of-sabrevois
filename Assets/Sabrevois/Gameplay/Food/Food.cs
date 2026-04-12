using System;
using Sabrevois.Gameplay.AI;
using UnityEngine;

public class Food : MonoBehaviour
{
    public float nutritionValue = 55f;

    public void Eat(GameObject agent)
    {
        agent.GetComponent<Hunger>().ReduceHunger(nutritionValue);
        Destroy(gameObject); 
    }
}
