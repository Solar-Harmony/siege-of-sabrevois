using System;
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
        public Archetype archetype;
        
        [SerializeField]
        private bool _useCustomInterval = false;
        
        [SerializeField] [EnableIf(nameof(_useCustomInterval))] 
        private float _decisionMakingInterval = 1f;

        [SerializeField]
        private ActionCandidate[] _perAgentActions;

        private ActionCandidate[] _actions;
        private ActionContext _ctx;
        
        private IAction _currentAction;
        
        [Inject]
        private DecisionMakingService _decisionMakingService;

        [Inject]
        private IObjectResolver _resolver;

        private void Start()
        {
            _actions = archetype.Actions.Concat(_perAgentActions).ToArray();
            _ctx = new ActionContext(gameObject);
            float interval = _useCustomInterval ? _decisionMakingInterval : archetype.DecisionMakingInterval;
            InvokeRepeating(nameof(UpdateCurrentAction), 0f, interval);
        }

        private void Update()
        {
            if (_currentAction != null)
            {
                _resolver.Inject(_currentAction);
                _currentAction.Execute(_ctx);
            }
        }

        private void UpdateCurrentAction()
        {
            _currentAction = _decisionMakingService.ChooseAction(_actions, _ctx);
        }
    }
}