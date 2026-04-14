using System.Collections.Generic;
using UnityEngine;

public enum WorldObjectCategory
{
    Food,
    House,
    NPC,
    Tree,
}

public class WorldObjectRegistry : MonoBehaviour
{
    public static WorldObjectRegistry Instance { get; private set; }

    private readonly Dictionary<WorldObjectCategory, List<GameObject>> _registry = new();

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void Register(WorldObjectCategory category, GameObject t)
    {
        if (!_registry.TryGetValue(category, out var list))
        {
            list = new List<GameObject>();
            _registry[category] = list;
        }
        list.Add(t);
    }

    public void Unregister(WorldObjectCategory category, GameObject t)
    {
        if (_registry.TryGetValue(category, out var list))
            list.Remove(t);
    }

    public List<GameObject> Get(WorldObjectCategory category)
    {
        return _registry.GetValueOrDefault(category);
    }

}
