namespace Sabrevois.AI.Actions
{
    public enum ActionStatus
    {
        Started,
        Running,
        Done,
    }
    
    public interface IActionState {}
    
    public interface IAction
    {
        public Interruptible Interruptible { get; }
        
        ActionStatus Begin(ActionContext ctx, IActionConfig config, object state);
        ActionStatus Update(ActionContext ctx, IActionConfig config, object state);
        void End(ActionContext ctx, IActionConfig config, object state);
    }

    public interface IAction<in TConfig, in TState> : IAction 
        where TConfig : class, IActionConfig
        where TState : class, IActionState, new()
    {
        ActionStatus Begin(ActionContext ctx, TConfig config, TState state) => ActionStatus.Running;
        ActionStatus Update(ActionContext ctx, TConfig config, TState state);
        void End(ActionContext ctx, TConfig config, TState state) { }
        
        ActionStatus IAction.Begin(ActionContext ctx, IActionConfig config, object state) => Begin(ctx, config as TConfig, state as TState);
        ActionStatus IAction.Update(ActionContext ctx, IActionConfig config, object state) => Update(ctx, config as TConfig, state as TState);
        void IAction.End(ActionContext ctx, IActionConfig config, object state) => End(ctx, config as TConfig, state as TState);
    }
}