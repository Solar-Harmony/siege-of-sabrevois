using System;

namespace Sabrevois.AI.Actions
{
    public interface IActionConfig
    {
        Type ActionType { get; }
    }
    
    public interface IActionConfig<in T> : IActionConfig where T : class, IAction
    {
        Type IActionConfig.ActionType => typeof(T);
    }
}