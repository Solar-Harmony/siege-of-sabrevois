using System;
using Sabrevois.AI.Actions;
using UnityEngine;

namespace Sabrevois.Gameplay.AI.Actions
{
    [Serializable]
    public class AttackActionConfig : ActionConfigBase<AttackAction, AttackActionState>
    {
        public float AttackRange = 100f;
        public float WoundRadius = 0.15f;
        public float WoundPenetration = 0.6f;
    }

    public class AttackActionState : IActionState
    {
    }

    public record AttackAction(AttackService AttackService) : IAction<AttackActionConfig, AttackActionState>
    {
        public Interruptible Interruptible => Interruptible.Always;

        public ActionStatus Begin(ActionContext ctx, AttackActionConfig config, AttackActionState state)
        {
            // Assuming the agent has a transform indicating where to shoot from, or just use the agent's transform
            Ray ray = new Ray(ctx.Agent.transform.position + Vector3.up * 1.5f, ctx.Agent.transform.forward);
            AttackService.Attack(ctx.Agent.transform, ray, config.AttackRange, config.WoundRadius, config.WoundPenetration);
            
            ctx.Agent.GetComponent<Energy>().SpendEnergy(config.EnergyCost);
            return ActionStatus.Done;
        }

        public ActionStatus Update(ActionContext ctx, AttackActionConfig config, AttackActionState state)
        {
            return ActionStatus.Done;
        }

        public void End(ActionContext ctx, AttackActionConfig config, AttackActionState state)
        {
        }
    }
}