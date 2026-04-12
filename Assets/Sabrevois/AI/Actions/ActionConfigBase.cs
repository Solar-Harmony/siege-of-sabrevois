using System;
using Sabrevois.AI.Actions;
using UnityEngine;

[Serializable]
public abstract class ActionConfigBase : IActionConfig
{
    public float EnergyCost = 0f;

    public abstract Type ActionType { get; }
    public abstract Type StateType { get; }
}

[Serializable]
public abstract class ActionConfigBase<TAction, TState> : ActionConfigBase, IActionConfig<TAction, TState>
    where TAction : class, IAction
    where TState : class, IActionState
{
    public override Type ActionType => typeof(TAction);
    public override Type StateType => typeof(TState);
}
