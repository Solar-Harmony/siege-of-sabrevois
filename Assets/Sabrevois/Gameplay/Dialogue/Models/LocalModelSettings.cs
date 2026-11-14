using System;
using System.Collections.Generic;
using System.Linq;
using SolarHarmony.Config;
using UnityEngine;

namespace Sabrevois.Gameplay
{
    /// <summary>
    /// Game config entry that selects which local model servers the game knows about.
    ///
    /// The <see cref="Models"/> list is <c>[SerializeReference]</c> over <see cref="ILocalModel"/>,
    /// so the inspector offers a type dropdown (JevModel, RaonModel, ...) per entry. Any model
    /// left checked with <see cref="ILocalModel.AutoStart"/> is launched on scene start when its
    /// health endpoint does not answer.
    /// </summary>
    [Serializable, GameConfig]
    public sealed class LocalModelSettings
    {
        [Tooltip("On scene start, check every model below and launch the ones that are not already answering.")]
        public bool AutoStartOnLoad = true;

        [Tooltip("Add an entry with +, then pick the implementation (JevModel / RaonModel) from its type dropdown. " +
                 "Uncheck Auto Start on an entry to keep it available without launching it.")]
        [SerializeReference]
        public List<ILocalModel> Models = new() { new JevModel(), new RaonModel() };

        /// <summary>First configured model of the requested implementation, or null.</summary>
        public T GetModel<T>() where T : class, ILocalModel =>
            Models?.OfType<T>().FirstOrDefault();

        /// <summary>Configured model with the given <see cref="ILocalModel.Id"/>, or null.</summary>
        public ILocalModel GetModel(string id) =>
            Models?.FirstOrDefault(model => model != null && model.Id == id);
    }
}
