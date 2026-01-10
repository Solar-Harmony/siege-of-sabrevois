using System;

namespace Sabrevois.AI.Actions
{
    public interface IActionConfig
    {
        Type ActionType { get; }
        Type StateType { get; }
    }
    
    public interface IActionConfig<in TAction, in TState> : IActionConfig
        where TAction : class, IAction
        where TState : class, IActionState
    {
        Type IActionConfig.ActionType => typeof(TAction);
        Type IActionConfig.StateType => typeof(TState);
    }
}