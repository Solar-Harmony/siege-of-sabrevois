using UnityEngine;

namespace Sabrevois.Gameplay
{
    public class Wood : MonoBehaviour
    {
        public float MaxWood;

        public float CurrentWood { get; private set; }
        public float CurrentWood01 => CurrentWood / MaxWood;

        private void Awake()
        {
            CurrentWood = MaxWood;
        }

        public void SpendWood(float amount)
        {
            CurrentWood = Mathf.Max(CurrentWood - amount, 0);

            if (CurrentWood <= 0)
            {
                //Plus de wood
            }
        }

        public void AddWood(float amount)
        {
            CurrentWood = Mathf.Min(CurrentWood + amount, MaxWood);
        }
    }

}