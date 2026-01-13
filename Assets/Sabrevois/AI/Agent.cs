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
        
        [SerializeField] 
        private bool _useCustomInterval = false;
        
        [SerializeField] [EnableIf(nameof(_useCustomInterval))]
        private float _decisionMakingInterval = 1f;
        
        [Inject]
        private DecisionMakingService _decisionMakingService;
        
        private ActionContext _ctx;
        private ActionInstance _actionInstance;
        private float _interval;
        private float _timer;

        private void Start()
        {
            _ctx = new ActionContext(gameObject);
            _interval = _useCustomInterval ? _decisionMakingInterval : Archetype.DecisionMakingInterval;
            _timer = _interval;
            UpdateCurrentAction(isInterruption: false);
        }
        
        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f)
                return;
            
            if (_actionInstance?.Update(_ctx) is ActionStatus.Done)
            {
                UpdateCurrentAction(isInterruption: false);
                _timer = _interval;
            }
            
            
        }
//
//         private void OnGUI()
//         {
// #if UNITY_EDITOR
//             var text = _actionInstance != null
//                 ? _actionInstance.Config.ActionType.Name
//                 : "No Action";
//             
//             GUIStyle style = new GUIStyle(GUI.skin.label)
//             {
//                 fontSize = 24,
//                 normal = { textColor = Color.white }
//             };
//             
//             var pos = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 2f);
//             Vector2 size = style.CalcSize(new GUIContent(text));
//             GUI.Label(new Rect(pos.x - size.x / 2, Screen.height - pos.y - size.y / 2, size.x, size.y), text, style);
// #endif
//         }

private void UpdateCurrentAction(bool isInterruption)
        {
            ActionInstance newAction = _decisionMakingService.ChooseAction(Archetype.Actions, _ctx, _actionInstance, Archetype.Hysteresis);
            if (newAction == null)
                return;

            bool sameAction = newAction.Config.ActionType == _actionInstance?.Config.ActionType;
            switch (_actionInstance?.Action?.Interruptible)
            {
                case Interruptible.Never:
                case Interruptible.ExceptSelf when sameAction && isInterruption:
                    return;
            }

            _actionInstance?.End(_ctx);
            _actionInstance = newAction;
            _actionInstance.Begin(_ctx);
        }
    }
}