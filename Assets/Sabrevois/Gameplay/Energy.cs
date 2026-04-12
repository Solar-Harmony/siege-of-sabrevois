using System;
using UnityEngine;

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
            CurrentEnergy = MaxEnergy;
        }
        
        public void SpendEnergy(float amount)
        {
            CurrentEnergy = Mathf.Max(CurrentEnergy - amount, 0);
            
            Debug.Log(name + "Energy spent: " + amount + ", current energy: " + CurrentEnergy);
            
            if (CurrentEnergy <= 0)
            {
                //s'endort
            }
        }
        
        public void ResetEnergy()
        {
            CurrentEnergy = MaxEnergy;
        }
    }
}
