namespace Sabrevois.AI.Actions
{
    public class ActionInstance
    {
        public IAction Action;
        public IActionConfig Config;
        public IActionState State;
        
        public ActionStatus Begin(ActionContext ctx) => Action.Begin(ctx, Config, State);
        public ActionStatus Update(ActionContext ctx) => Action.Update(ctx, Config, State);
        public void End(ActionContext ctx) => Action.End(ctx, Config, State);
    }
}