using System.Collections.Generic;
using UnityEngine;

public class WorldObjectRegistry : MonoBehaviour
{
    public static WorldObjectRegistry Instance { get; private set; }

    private readonly Dictionary<string, List<Transform>> _registry = new();

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void Register(string category, Transform t)
    {
        if (!_registry.TryGetValue(category, out var list))
        {
            list = new List<Transform>();
            _registry[category] = list;
        }
        list.Add(t);
    }

    public void Unregister(string category, Transform t)
    {
        if (_registry.TryGetValue(category, out var list))
            list.Remove(t);
    }

    public List<Transform> Get(string category)
    {
        return _registry.GetValueOrDefault(category);
    }

}
