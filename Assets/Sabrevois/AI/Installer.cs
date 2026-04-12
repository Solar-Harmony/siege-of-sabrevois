using System;
using System.Collections.Generic;
using System.Linq;
using Sabrevois.AI.Actions;
using UnityEngine;
using VContainer;

namespace Sabrevois.AI
{
    public static class Installer
    {
        public static void Configure(IContainerBuilder builder)
        {
            List<Type> actionTypes = AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(asm => asm.GetTypes())
                .Where(t => typeof(IAction).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
                .ToList();
            
            foreach (Type type in actionTypes)
            {
                builder.Register(type, Lifetime.Singleton).AsImplementedInterfaces();
            }
            
            string actionTypesStr = string.Join(", ", actionTypes.Select(t => t.Name));
            Debug.LogFormat("Registered {0} action types: {1}", actionTypes.Count, actionTypesStr);
            
            builder.Register<IDecisionMakingService, SequentialDecisionMakingService>(Lifetime.Singleton);
        }
    }
}