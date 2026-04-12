using System;
using ArtificeToolkit.Attributes;
using Sabrevois.AI.Actions;
using UnityEngine;
using Zenject;

namespace Sabrevois.AI
{
    public class Agent : MonoBehaviour
    {
        [SerializeField] [PreviewScriptable] 
        public Archetype Archetype;
        
        public string Name;
        
        private static readonly string[] NamesList =
        {
            "Adam",
            "Arthur",
            "Dan",
            "Danick",
            "Doug",
            "Félix",
            "Gustave", 
            "Jonesy",
            "Maëlle",
            "Ning",
            "Théo",
            "Tim",
            "Vandal",
            "Vince",
            "William",
            "Yanick",
            "Yannick"
        };
        private static int _nameCounter = 1;

        [SerializeField] 
        private bool _useCustomInterval = false;
        
        [SerializeField] [EnableIf(nameof(_useCustomInterval))]
        private float _decisionMakingInterval = 1f;
        
        [Inject]
        private IDecisionMakingService _decisionMakingService;
        
        private ActionContext _ctx;
        private ActionInstance _actionInstance;
        private float _interval;
        private float _timer;
        private bool _isInterruption = false;
        private bool _hasRequestedAction = false;

        private void Start()
        {
            if (string.IsNullOrEmpty(Name))
            {
                Name = $"{NamesList[(_nameCounter - 1) % NamesList.Length]}#{_nameCounter:D4}";
                _nameCounter++;
            }
            
            _ctx = new ActionContext(gameObject);
            _interval = _useCustomInterval ? _decisionMakingInterval : Archetype.DecisionMakingInterval;
            _timer = _interval;
            UpdateCurrentAction(isInterruption: false);
        }
        
        private void Update()
        {
            // If we are waiting for an action, let's freeze
            if (_hasRequestedAction)
                return;
            
            _timer -= Time.deltaTime;
            if (_timer > 0f)
                return;
            
            if (_actionInstance?.Update(_ctx) is ActionStatus.Done)
            {
                UpdateCurrentAction(isInterruption: false);
                _timer = _interval;
            }
        }

        private void UpdateCurrentAction(bool isInterruption)
        {
            _hasRequestedAction = true;
            _isInterruption = isInterruption;
            _decisionMakingService.ChooseAction(Archetype.Actions, _ctx, _actionInstance, Archetype.Hysteresis);
        }

        public void ReceiveAction(ActionInstance newAction)
        {
            _hasRequestedAction = false;
            
            if (newAction == null)
                return;

            bool sameAction = newAction.Config.ActionType == _actionInstance?.Config.ActionType;
            switch (_actionInstance?.Action?.Interruptible)
            {
                case Interruptible.Never:
                case Interruptible.ExceptSelf when sameAction && _isInterruption:
                    return;
            }
            
            _actionInstance?.End(_ctx);
            _actionInstance = newAction;
            _actionInstance.Begin(_ctx);
        }
    }
}