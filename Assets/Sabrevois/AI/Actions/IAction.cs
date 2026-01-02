namespace Sabrevois.AI.Actions
{
    public interface IAction
    {
        void Execute(ActionContext ctx, object config);
    }
    
    public interface IAction<in TConfig> : IAction where TConfig : class
    {
        void IAction.Execute(ActionContext ctx, object config)
        {
            Execute(ctx, config as TConfig);
        }
        
        void Execute(ActionContext ctx, TConfig config);
    }
}