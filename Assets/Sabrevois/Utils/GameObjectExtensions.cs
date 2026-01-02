using UnityEngine;

namespace Sabrevois.Utils
{
    public static class GameObjectExtensions
    {
        // Finds the component of type T on the GameObject. Throws otherwise.
        // Use this in initialization code where the component is required to be present, like BT actions.
        public static void GetComponentChecked<T>(this GameObject obj, out T component) where T : Component
        {
            if (obj.TryGetComponent(out component)) 
                return;
            
            throw new MissingComponentException($"GameObject '{obj.name}' is missing a {typeof(T).Name} component.");
        }
        
        public static void GetComponentChecked<T>(this Component source, out T component) where T : Component
        {
            if (source.TryGetComponent(out component)) 
                return;
            
            throw new MissingComponentException($"GameObject '{source.name}' is missing a {typeof(T).Name} component.");
        }
        
        public static bool HasComponent<T>(this GameObject target) where T : Component
        {
            return target.TryGetComponent<T>(out _);
        }
    }
}