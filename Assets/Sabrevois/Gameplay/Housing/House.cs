using UnityEngine;

public class House : MonoBehaviour
{
    [SerializeField]
    private int maxAmountOfOccupants = 4;
    
    private int _currentAmountOfOccupants;
    
   public void AddOccupant()
   {
       if (_currentAmountOfOccupants < maxAmountOfOccupants)
       {
           _currentAmountOfOccupants++;
       }
   }
   
   public void RemoveOccupant()
   {
       if (_currentAmountOfOccupants > 0)
       {
           _currentAmountOfOccupants--;
       }
   }
   
   public bool IsFull()
   {
       return _currentAmountOfOccupants >= maxAmountOfOccupants;
   }
}
