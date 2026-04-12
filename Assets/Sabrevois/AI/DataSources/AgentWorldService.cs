using System;
using System.Collections.Generic;
using System.Linq;
using Sabrevois.AI.Considerations;

namespace Sabrevois.AI.DataSources
{
    public record AgentWorldSnapshot(Dictionary<Type, float> Data)
    {
        public float GetData(IDataSource source)
        {
            return Data[source.GetType()]; 
        }
    }

    public class AgentWorldService
    {
        public AgentWorldSnapshot RequestDataSnapshot(Agent agent)
        {
            // fixme: in case you didn't realise, this is retarded
            return new AgentWorldSnapshot(agent.Archetype.Actions
                .SelectMany(a => a.Preconditions.Concat(a.Considerations.Cast<Consideration>()))
                .Select(a => a.Source)
                .GroupBy(a => a.GetType())
                .Select(g => g.First())
                .ToDictionary(ds => ds.GetType(), ds => ds.GetValue(agent.gameObject)));
        }
    }
}