using UnityEngine;

namespace Sabrevois.Gameplay.AI
{
    public class WorldObjectRegistrant : MonoBehaviour
    {
        [SerializeField] 
        private string category = "SleepSpot";

        private void OnEnable()
        {
            WorldObjectRegistry.Instance.Register(category, transform);
        }

        private void OnDisable()
        {
            WorldObjectRegistry.Instance.Unregister(category, transform);
        }
    }
}
