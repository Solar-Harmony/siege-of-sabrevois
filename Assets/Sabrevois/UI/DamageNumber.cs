using UnityEngine;
using TMPro;
using Zenject;

namespace Sabrevois.UI
{
    public class DamageNumber : MonoBehaviour
    {
        [SerializeField] private TextMeshPro textMesh;
        [SerializeField] private float lifetime = 1.5f;
        [SerializeField] private float floatSpeed = 1f;

        private float _timer;
        private IMemoryPool _pool;
        private bool _isMiss;

        public void Init(Vector3 position, float amount, IMemoryPool pool)
        {
            _pool = pool;
            _isMiss = false;
            transform.position = position;

            if (textMesh == null)
            {
                textMesh = GetComponentInChildren<TextMeshPro>();
                if (textMesh == null)
                {
                    textMesh = gameObject.AddComponent<TextMeshPro>();
                    textMesh.alignment = TextAlignmentOptions.Center;
                    textMesh.fontSize = 1.5f;
                    textMesh.color = Color.red;
                }
            }
            if (textMesh != null)
            {
                textMesh.text = amount.ToString("F1");
                textMesh.color = Color.red;
            }
            _timer = 0f;
        }

        public void InitMiss(Vector3 position, IMemoryPool pool)
        {
            _pool = pool;
            _isMiss = true;
            transform.position = position;

            if (textMesh == null)
            {
                textMesh = GetComponentInChildren<TextMeshPro>();
                if (textMesh == null)
                {
                    textMesh = gameObject.AddComponent<TextMeshPro>();
                    textMesh.alignment = TextAlignmentOptions.Center;
                    textMesh.fontSize = 1.5f;
                }
            }
            if (textMesh != null)
            {
                textMesh.text = "Miss!";
                textMesh.color = Color.yellow;
            }
            _timer = 0f;
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            transform.position += Vector3.up * floatSpeed * Time.deltaTime;
            
            if (textMesh != null)
            {
                var color = textMesh.color;
                color.a = 1f - (_timer / lifetime);
                textMesh.color = color;
            }

            if (_timer >= lifetime)
            {
                _pool.Despawn(this);
            }
        }
        
        private void LateUpdate()
        {
            if (Camera.main != null)
            {
                transform.forward = Camera.main.transform.forward;
            }
        }

        public class Pool : MonoMemoryPool<Vector3, float, DamageNumber>
        {
            protected override void Reinitialize(Vector3 position, float amount, DamageNumber item)
            {
                item.Init(position, amount, this);
            }
        }

        public class MissTextPool : MonoMemoryPool<Vector3, DamageNumber>
        {
            protected override void Reinitialize(Vector3 position, DamageNumber item)
            {
                item.InitMiss(position, this);
            }
        }
    }
}