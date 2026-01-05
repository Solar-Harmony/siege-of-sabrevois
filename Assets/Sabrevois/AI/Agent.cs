using System;
using System.Collections.Generic;
using System.Linq;
using ArtificeToolkit.Attributes;
using Sabrevois.AI.Actions;
using UnityEngine;
using VContainer;

namespace Sabrevois.AI
{
    public class Agent : MonoBehaviour
    {
        [SerializeField] [PreviewScriptable] 
        public Archetype Archetype;
        
        [SerializeField] [HorizontalGroup(nameof(_useCustomInterval))]
        private bool _useCustomInterval = false;
        
        [SerializeField] [EnableIf(nameof(_useCustomInterval))] [HorizontalGroup(nameof(_useCustomInterval))]
        private float _decisionMakingInterval = 1f;
        
        [Inject]
        private DecisionMakingService _decisionMakingService;
        
        private ActionContext _ctx;
        private (IAction action, IActionConfig config) _currentAction = (null, null);

        private void Start()
        {
            _ctx = new ActionContext(gameObject);
            float interval = _useCustomInterval ? _decisionMakingInterval : Archetype.DecisionMakingInterval;
            InvokeRepeating(nameof(UpdateCurrentAction), 0f, interval);
        }

        private void Update()
        {
            if (_currentAction.action.Execute(_ctx, _currentAction.config))
            {
                UpdateCurrentAction();
            }
        }

        private void UpdateCurrentAction()
        {
            (IAction action, IActionConfig) newAction = _decisionMakingService.ChooseAction(Archetype.Actions, _ctx);

            if (_currentAction.action == null)
            {
                _currentAction = newAction;
                return;
            }

            if (_currentAction.action.Interruptible == Interruptible.Never)
                return;
            
            if (_currentAction.action.Interruptible == Interruptible.ExceptSelf && _currentAction == newAction)
                return;

            _currentAction = newAction;
        }
    }
}