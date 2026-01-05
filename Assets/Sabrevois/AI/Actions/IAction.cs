namespace Sabrevois.AI.Actions
{
    public interface IAction
    {
        public Interruptible Interruptible { get; }
        bool Execute(ActionContext ctx, IActionConfig config);
    }

    public interface IAction<in T> : IAction where T : class, IActionConfig
    {
        bool Execute(ActionContext ctx, T config);
        bool IAction.Execute(ActionContext ctx, IActionConfig config) => Execute(ctx, config as T);
    }
}