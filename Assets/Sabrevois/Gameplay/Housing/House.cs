using Sabrevois.Gameplay;
using UnityEngine;

public class House : MonoBehaviour
{
    [SerializeField]
    private int maxAmountOfOccupants = 4;
    
    [SerializeField]
    private Color fullColor = Color.red;

    private Renderer _houseRenderer;
    private Color _defaultColor;
    private int _currentAmountOfOccupants;

    private void Start()
    {
        if (_houseRenderer == null)
            _houseRenderer = GetComponent<Renderer>();

        _defaultColor = _houseRenderer.material.color;
    }
    
   public void AddOccupant()
   {
       if (_currentAmountOfOccupants < maxAmountOfOccupants)
       {
           _currentAmountOfOccupants++;
           UpdateColor();

       }
   }
   
   public void RemoveOccupant()
   {
       if (_currentAmountOfOccupants > 0)
       {
           _currentAmountOfOccupants--;
           UpdateColor();
       }
   }
   
   public bool IsFull()
   {
       return _currentAmountOfOccupants >= maxAmountOfOccupants;
   }

    public bool NeedsWood()
    {
        Wood houseWood = this.GetComponent<Wood>();
        return houseWood.CurrentWood < houseWood.MaxWood;
    }

    private void UpdateColor()
   {
       _houseRenderer.material.color = IsFull() ? fullColor : _defaultColor;
   }
}
