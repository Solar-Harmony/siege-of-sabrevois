using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace Sabrevois.Level.Water
{
    public struct WaterDisturbance
    {
        public Vector2 position;
        public float radius;
        public float strength;
    }

    public class WaterRipplesInteraction : MonoBehaviour
    {
        [Header("Tuning Settings")]
        // Dropping default tuning back to realistic values, since logic is definitely working now
        public float disturbRadius = 1.0f;
        public float disturbStrength = 5.0f;
        
        [Tooltip("Toggle the real-time texture visualizer UI at the top left of the screen.")]
        public bool debugDrawTexture = true;

        private static readonly Queue<WaterDisturbance> _disturbances = new Queue<WaterDisturbance>();
        
        private readonly HashSet<WaterInteractor> _activeInteractors = new HashSet<WaterInteractor>();

        public static bool TryGetDisturbance(out WaterDisturbance disturbance)
        {
            if (_disturbances.Count > 0)
            {
                disturbance = _disturbances.Dequeue();
                return true;
            }
            disturbance = default;
            return false;
        }

        // Static helper to easily add ripples from anywhere (like gun logic)
        public static void AddDisturbance(Vector2 worldPosXZ, float radius, float strength)
        {
            _disturbances.Enqueue(new WaterDisturbance
            {
                position = worldPosXZ,
                radius = radius,
                strength = strength
            });
        }
        
        void Update()
        {
            _activeInteractors.RemoveWhere(i => i == null);

            foreach (var interactor in _activeInteractors)
            {
                Vector3 currentPos = interactor.transform.position;
                // Only disturb if the player has moved a bit, to prevent over-saturating one spot
                // Or you can remove the distance check if you want continuous ripples while standing still
                if (Vector3.Distance(currentPos, interactor.lastPos) > 0.05f)
                {
                    DisturbWater(new Vector2(currentPos.x, currentPos.z), 
                        disturbRadius * interactor.radiusMultiplier, 
                        disturbStrength * interactor.strengthMultiplier);
                    interactor.lastPos = currentPos;
                }
            }
        }

        private WaterInteractor GetInteractor(Collider other)
        {
            if (other.attachedRigidbody != null)
            {
                var interactor = other.attachedRigidbody.GetComponent<WaterInteractor>();
                if (interactor != null) return interactor;
            }
            return other.GetComponentInParent<WaterInteractor>();
        }

        private void OnTriggerEnter(Collider other)
        {
            var interactor = GetInteractor(other);
            if (interactor != null)
            {
                if (_activeInteractors.Add(interactor))
                {
                    interactor.lastPos = interactor.transform.position;
                }
            }
        }

        private void OnTriggerStay(Collider other)
        {
            var interactor = GetInteractor(other);
            if (interactor != null)
            {
                _activeInteractors.Add(interactor);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var interactor = GetInteractor(other);
            if (interactor != null)
            {
                _activeInteractors.Remove(interactor);
            }
        }

        public void DisturbWater(Vector2 worldPosXZ, float radius, float strength)
        {
            AddDisturbance(worldPosXZ, radius, strength);
        }

        private void OnGUI()
        {
            if (!debugDrawTexture) return;
            
            Texture tex = Shader.GetGlobalTexture("_WaterRipplesTex");
            if (tex != null)
            {
                GUI.color = Color.white;
                // Making the debug view much larger
                GUI.DrawTexture(new Rect(10, 10, 1024, 1024), tex, ScaleMode.ScaleToFit, false);
                GUI.Label(new Rect(10, 530, 512, 20), "Water Ripples Debug");
            }
            else
            {
                GUI.Label(new Rect(10, 10, 256, 20), "Water Ripples Texture not found");
            }
        }
    }
}
