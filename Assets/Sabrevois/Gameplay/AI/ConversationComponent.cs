using Sabrevois.AI;
using Sabrevois.AI.Actions;
using UnityEngine;

namespace Sabrevois.Gameplay.AI
{
    public class ConversationComponent : MonoBehaviour
    {
        private Agent _agent;
        public bool WantsConversation => _agent != null && _agent.CurrentAction?.Interruptible != Interruptible.Never;
        
        private Transform _conversationPartner;
        public Transform ConversationPartner
        {
            get => _conversationPartner;
            set
            {
                if (_conversationPartner == value) return;

                var oldPartner = _conversationPartner;
                _conversationPartner = value;

                // End conversation with the old partner
                if (oldPartner != null)
                {
                    var oldPartnerComponent = oldPartner.GetComponent<ConversationComponent>();
                    if (oldPartnerComponent != null && oldPartnerComponent.ConversationPartner == transform)
                    {
                        oldPartnerComponent.ConversationPartner = null;
                    }
                }

                // Start conversation with the new partner
                if (_conversationPartner != null)
                {
                    var newPartnerComponent = _conversationPartner.GetComponent<ConversationComponent>();
                    if (newPartnerComponent != null && newPartnerComponent.ConversationPartner != transform)
                    {
                        newPartnerComponent.ConversationPartner = transform;
                    }
                }
            }
        }
        
        public float ChanceToDropConversation = 0.1f;

        private void Awake()
        {
            _agent = GetComponent<Agent>();
            InvokeRepeating(nameof(MaybeDropConversation), 1.0f, 1.0f);
        }

        private void MaybeDropConversation()
        {
            if (ConversationPartner != null)
            {
                if (!ConversationPartner.gameObject.activeInHierarchy || Random.Range(0f, 1f) <= ChanceToDropConversation)
                {
                    ConversationPartner = null;
                }
            }
        }
    }
}