using UnityEngine;

public class House : MonoBehaviour
{
    [SerializeField]
    private int maxAmountOfOccupants = 4;
    
    [SerializeField]
    private Renderer houseRenderer;

    [SerializeField]
    private Color fullColor = Color.red;

    private Color _defaultColor;
    private int _currentAmountOfOccupants;

    private void Start()
    {
        if (houseRenderer == null)
            houseRenderer = GetComponent<Renderer>();

        _defaultColor = houseRenderer.material.color;
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
   
   private void UpdateColor()
   {
       houseRenderer.material.color = IsFull() ? fullColor : _defaultColor;
   }
}
