using System;
using UnityEngine;
using UnityEngine.AI;

namespace Sabrevois.Gameplay
{
    public class FakeAI : MonoBehaviour
    {
        [SerializeField] private NavMeshAgent _navAgent;
        [SerializeField] private Vector3 _target;
        public bool IsPathing => _navAgent.pathPending || _navAgent.remainingDistance > _navAgent.stoppingDistance;

        private void Awake()
        {
            _navAgent = GetComponent<NavMeshAgent>();
        }

        private void Start()
        {
            _navAgent.SetDestination(_target);
        }

        private void Update()
        {
            if (!IsPathing)
            {
                Debug.Log("je suis arrive!");
            }
        }
    }
}