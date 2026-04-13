using UnityEngine;

namespace Sabrevois.Gameplay
{
    public class Wood : MonoBehaviour
    {
        public float MaxWood;

        [field: SerializeField]
        public float currentWood { get; private set; }
        public float CurrentWood => CurrentWood;

        private void Awake()
        {
            currentWood = MaxWood;
        }

        public void SpendWood(float amount)
        {
            currentWood = Mathf.Max(currentWood - amount, 0);

            if (currentWood <= 0)
            {
                //Plus de wood
            }
        }

        public void AddWood(float amount)
        {
            currentWood = Mathf.Min(currentWood + amount, MaxWood);
        }
    }

}