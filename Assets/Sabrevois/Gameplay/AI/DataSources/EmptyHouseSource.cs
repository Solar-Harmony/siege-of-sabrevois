using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sabrevois.AI.DataSources;
using UnityEngine;

[Serializable]
public class EmptyHouseSource : IDataSource
{
    public float GetValue(GameObject agent)
    {
        return WorldObjectRegistry.Instance
            .Get(WorldObjectCategory.House)
            .Select(go => go.GetComponent<House>())
            .Count(h => !h.IsFull());
    }
}
