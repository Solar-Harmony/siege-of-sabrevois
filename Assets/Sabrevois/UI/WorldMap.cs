using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Sabrevois.UI
{
    public class WorldMap : MonoBehaviour
    {
        [SerializeField]
        private UIDocument _document;
        
        [SerializeField]
        private GameObject _player;
        
        [SerializeField]
        private Rect _worldBounds; // TODO: to service
        
        [SerializeField]
        private float _minZoom = 0.5f;
        
        [SerializeField]
        private float _maxZoom = 3f;
        
        [SerializeField]
        private float _zoomSpeed = 0.1f;
        
        private VisualElement _root;
        private VisualElement _map;
        private VisualElement _playerMarker;
        private float _currentZoom = 1f;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Vector3 center3D = new(_worldBounds.center.x, 0, _worldBounds.center.y);
            Vector3 size3D = new(_worldBounds.size.x, 0, _worldBounds.size.y);
            Gizmos.DrawWireCube(center3D, size3D);
        }

        private void Awake()
        {
            _root = _document.rootVisualElement;
            _map = _root.Q<VisualElement>("map");
            _playerMarker = _root.Q<VisualElement>("player-marker");
        }

        private void Update()
        {
            HandleZoomInput(); 
            UpdatePlayerMarkerPosition();
        }

        private void HandleZoomInput()
        {
            float scrollDelta = Mouse.current.scroll.y.ReadValue() / 120f;
            if (math.abs(scrollDelta) > 0.01f)
            {
                _currentZoom += scrollDelta * _zoomSpeed;
                _currentZoom = math.clamp(_currentZoom, _minZoom, _maxZoom);

                _map.style.scale = new Scale(new Vector2(_currentZoom, _currentZoom));
            }
        }

        private void UpdatePlayerMarkerPosition()
        {
            Vector2 playerPosWS = new(_player.transform.position.x, _player.transform.position.z);
            Vector2 normalizedPlayerPos = new(
                math.unlerp(_worldBounds.xMin, _worldBounds.xMax, playerPosWS.x),
                math.unlerp(_worldBounds.yMin, _worldBounds.yMax, playerPosWS.y)
            );
            Vector2 mapSize = _map.contentRect.size;
            Vector2 playerMarkerPos = new(
                normalizedPlayerPos.x * mapSize.x * _currentZoom,
                (1 - normalizedPlayerPos.y) * mapSize.y * _currentZoom
            );
            _playerMarker.style.left = playerMarkerPos.x;
            _playerMarker.style.top = playerMarkerPos.y;
        }
    }
}