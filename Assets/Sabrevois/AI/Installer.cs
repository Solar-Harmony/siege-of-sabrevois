using System;
using System.Collections.Generic;
using System.Linq;
using Sabrevois.AI.Actions;
using Sabrevois.AI.DataSources;
using UnityEngine;
using Zenject;

namespace Sabrevois.AI
{
    public class AIInstaller : Installer<AIInstaller>
    {
        public override void InstallBindings()
        {
            BindActionTypes();
            Container.Bind<AgentWorldService>().AsSingle();
            Container.BindInterfacesAndSelfTo<SequentialDecisionMakingService>().AsSingle();
        }

        private void BindActionTypes()
        {
            List<Type> actionTypes = AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(asm => asm.GetTypes())
                .Where(t => typeof(IAction).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
                .ToList();
            
            foreach (Type type in actionTypes)
            {
                Container.Bind<IAction>().To(type).AsSingle();
            }
            
            string actionTypesStr = string.Join(", ", actionTypes.Select(t => t.Name));
            Debug.LogFormat("Registered {0} action types: {1}", actionTypes.Count, actionTypesStr);
        }
    }
}