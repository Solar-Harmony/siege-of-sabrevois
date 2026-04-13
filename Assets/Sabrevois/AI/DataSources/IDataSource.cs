using UnityEngine;

namespace Sabrevois.AI.DataSources
{
    public interface IDataSource
    {
        public float GetValue(GameObject agent);
    }
}