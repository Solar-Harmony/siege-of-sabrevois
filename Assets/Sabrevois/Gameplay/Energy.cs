using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Sabrevois.Gameplay
{
    public class Energy : MonoBehaviour
    {
        [Min(1.0f)]
        public float MaxEnergy;
        
        public float CurrentEnergy { get; private set; }
        public float CurrentEnergy01 => CurrentEnergy / MaxEnergy;
        
        private void Awake()
        {
            MaxEnergy = Random.Range(70f, 150f);
            CurrentEnergy = MaxEnergy;
        }
        
        public void SpendEnergy(float amount)
        {
            CurrentEnergy = Mathf.Max(CurrentEnergy - amount, 0);
            
            if (CurrentEnergy <= 0)
            {
                //s'endort
            }
        }
        
        public void GainEnergy(float amount)
        {
            CurrentEnergy = Mathf.Min(CurrentEnergy + amount, MaxEnergy);
        }
        
        public void ResetEnergy()
        {
            CurrentEnergy = MaxEnergy;
        }
    }
}
