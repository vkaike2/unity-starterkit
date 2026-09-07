using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vkaike2.StarterKit.Base.Abstracts;
using Vkaike2.StarterKit.Base.Interfaces;
using Vkaike2.StarterKit.Base.Models;
using Vkaike2.StarterKit.Managers.LoadManager.Base;

namespace Vkaike2.StarterKit.Managers.LoadManager
{
    public class SceneLoader : MonoBehaviour, ILoadableEntity
    {
        [SerializeField] private Configurations _configurations;

        private SequenceRunner _sequenceRunner;

        private SequenceRunner Runner => _sequenceRunner ??= new SequenceRunner(this);

        private void OnValidate()
        {
            _configurations.ValidateFields(this);
        }

        public async Awaitable Load()
        {
            var sequencesToLoad = _configurations.Sequences.Where(e => e.IsActive).ToList();

            foreach (var sequence in sequencesToLoad)
            {
                await Runner.LoadEntities(sequence);
            }
        }

        [Serializable]
        private class Configurations : ValidatableFields
        {
            [field: SerializeField] public List<LoadSequence> Sequences { get; set; }

            protected override void Validate()
            {
                SequenceRunner.ValidateSequences(Sequences, Context);
            }
        }
    }
}
