using System;
using Sabrevois.Gameplay.AI;
using UnityEngine;
using Random = UnityEngine.Random;

public class Food : MonoBehaviour
{
    public float nutritionValue = 55f;

    public void Eat(GameObject agent)
    {
        nutritionValue = Random.Range(20f, 100f);
        agent.GetComponent<Hunger>().ReduceHunger(nutritionValue);
        Destroy(gameObject); 
    }
}
