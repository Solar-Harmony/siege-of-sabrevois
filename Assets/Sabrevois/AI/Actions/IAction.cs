using System;

namespace Sabrevois.AI.Actions
{
    public interface IActionConfig
    {
        Type ActionType { get; }
    }
    
    public interface IActionConfig<T> : IActionConfig where T : IAction
    {
        Type IActionConfig.ActionType => typeof(T);
    }
    
    public interface IAction
    {
        void Execute(ActionContext ctx, IActionConfig config);
    }

    public interface IAction<in T> : IAction where T : class, IActionConfig
    {
        void Execute(ActionContext ctx, T config);
        void IAction.Execute(ActionContext ctx, IActionConfig config) => Execute(ctx, config as T);
    }
}