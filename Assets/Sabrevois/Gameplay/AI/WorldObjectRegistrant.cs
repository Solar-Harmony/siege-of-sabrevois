using UnityEngine;

namespace Sabrevois.Gameplay.AI
{
    public class WorldObjectRegistrant : MonoBehaviour
    {
        [SerializeField] 
        private WorldObjectCategory category = WorldObjectCategory.Food;

        private void OnEnable()
        {
            WorldObjectRegistry.Instance.Register(category, gameObject);
        }

        private void OnDisable()
        {
            WorldObjectRegistry.Instance.Unregister(category, gameObject);
        }
    }
}
