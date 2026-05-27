using System.Collections.Generic;
using UnityEngine;

namespace Sabrevois.Gameplay
{
    public struct Wound
    {
        public Vector2 Position;
        public Vector3 Normal;
        public float Radius;
        public float Penetration;
        public float Intensity;
    }
    
    public class WoundsComponent : MonoBehaviour 
    {
        private List<Wound> _wounds = new List<Wound>();
        private int _sliceIndex = -1;
        
        [SerializeField]
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _mpb;

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
        }

        private void Start()
        {
            if (GlobalWoundManager.Instance != null)
            {
                _sliceIndex = GlobalWoundManager.Instance.RequestSlice();
                if (_sliceIndex >= 0 && _renderer != null)
                {
                    _renderer.GetPropertyBlock(_mpb);
                    _mpb.SetFloat("_WoundSliceIndex", _sliceIndex);
                    _renderer.SetPropertyBlock(_mpb);
                }
            }
        }

        private void OnDestroy()
        {
            if (GlobalWoundManager.Instance != null && _sliceIndex >= 0)
            {
                GlobalWoundManager.Instance.ReleaseSlice(_sliceIndex);
            }
        }
        
        public void ApplyWound(RaycastHit hit)
        {
            // Resolve perspective mismatch between thick physics capsule surface and flat visual sprite plane.
            // By casting directly from the player's camera vector to the visual math plane, we find the exact pixel the crosshair was aimed at!
            Vector3 cameraPos = Camera.main.transform.position;
            Vector3 rayDir = (hit.point - cameraPos).normalized;

            Vector3 localPoint;
            Bounds localBounds = new Bounds(Vector3.zero, Vector3.one);
            Vector2 quadSize = Vector2.one;
            
            if (_renderer != null) 
            {
                var mf = _renderer.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                    localBounds = mf.sharedMesh.bounds;
                    
                quadSize = new Vector2(localBounds.size.x * _renderer.transform.lossyScale.x, localBounds.size.y * _renderer.transform.lossyScale.y);

                // Create a mathematical plane matching the GPU Billboard facing the camera (Horizontal-only Billboarding assumption)
                Vector3 toCamera = cameraPos - _renderer.transform.position;
                toCamera.y = 0; // If you use horizontal-only GPU billboarding
                Vector3 planeNormal = -toCamera.normalized;
                
                if (planeNormal.sqrMagnitude < 0.001f) 
                    planeNormal = -_renderer.transform.forward;

                Plane quadPlane = new Plane(planeNormal, _renderer.transform.position);
                
                // Intersect the player's line of sight to find where it specifically pierces the 2D artwork
                if (quadPlane.Raycast(new Ray(cameraPos, rayDir), out float enter))
                {
                    Vector3 intersectPoint = cameraPos + rayDir * enter;
                    
                    // We must manually inverse-transform the point accounting for the billboard rotation!
                    Quaternion billboardRot = Quaternion.LookRotation(-planeNormal, Vector3.up);
                    Vector3 offset = intersectPoint - _renderer.transform.position;
                    Vector3 unrotatedOffset = Quaternion.Inverse(billboardRot) * offset;
                    
                    // Apply inverse scale
                    unrotatedOffset.x /= _renderer.transform.lossyScale.x;
                    unrotatedOffset.y /= _renderer.transform.lossyScale.y;
                    unrotatedOffset.z /= _renderer.transform.lossyScale.z;
                    
                    localPoint = unrotatedOffset;
                }
                else
                {
                    localPoint = _renderer.transform.InverseTransformPoint(hit.point);
                }
            }
            else
            {
                localPoint = transform.InverseTransformPoint(hit.point);
                quadSize = new Vector2(transform.lossyScale.x, transform.lossyScale.y);
            }

            // Convert local point to 0-1 UV space of the bounds
            float u = Mathf.InverseLerp(localBounds.min.x, localBounds.max.x, localPoint.x);
            float v = Mathf.InverseLerp(localBounds.min.y, localBounds.max.y, localPoint.y);
            Vector2 uv = new Vector2(u, v);

            Wound wound = new Wound
            {
                Position = uv, // Store UV in position
                Normal = hit.normal,        
                Radius = 0.15f,
                Penetration = 0.6f, // 0.6 means 2 hits required to fully pierce layer 0 into layer 1
                Intensity = 1f
            };
            
            _wounds.Add(wound);

            if (_sliceIndex >= 0 && GlobalWoundManager.Instance != null)
            {
                GlobalWoundManager.Instance.AddWoundSplat(_sliceIndex, uv, wound.Radius, wound.Penetration, quadSize);
            }
        }
    }
}