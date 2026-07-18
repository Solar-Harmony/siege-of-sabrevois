using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;

namespace SolarHarmony.Config
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Solar Harmony/Game Config")]
    public class GameConfigInstaller : ScriptableObjectInstaller<GameConfigInstaller>
    {
        [SerializeReference]
        public List<object> Configs = new();

        public override void InstallBindings()
        {
            foreach (var config in Configs)
            {
                if (config != null)
                {
                    Container.Bind(config.GetType()).FromInstance(config).IfNotBound();
                }
            }
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            var configTypes = UnityEditor.TypeCache.GetTypesWithAttribute<GameConfigAttribute>()
                .Where(t => !t.IsAbstract && !t.IsGenericType)
                .ToList();

            var existingTypes = Configs.Where(c => c != null).Select(c => c.GetType()).ToHashSet();

            bool changed = false;
            foreach (var type in configTypes)
            {
                if (!existingTypes.Contains(type))
                {
                    try
                    {
                        var instance = Activator.CreateInstance(type);
                        Configs.Add(instance);
                        changed = true;
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[GameConfigInstaller] Failed to create instance of config type {type}: {e.Message}");
                    }
                }
            }
            
            // Optional: remove configs that no longer have the attribute
            for (int i = Configs.Count - 1; i >= 0; i--)
            {
                var config = Configs[i];
                if (config == null || !configTypes.Contains(config.GetType()))
                {
                    Configs.RemoveAt(i);
                    changed = true;
                }
            }

            if (changed)
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
#endif
        }
    }
}
