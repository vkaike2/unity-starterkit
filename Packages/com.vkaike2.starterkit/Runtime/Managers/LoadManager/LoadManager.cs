using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Vkaike2.StarterKit.Base.Abstracts;
using Vkaike2.StarterKit.Base.Interfaces;
using Vkaike2.StarterKit.Base.Models;
using Vkaike2.StarterKit.UI;
using static Vkaike2.StarterKit.Base.Models.LoadSequence;

namespace Vkaike2.StarterKit.Managers.LoadManager
{
    public class LoadManager : Singleton<LoadManager>
    {
        [SerializeField] private Configurations _configurations;
        [SerializeField] private Components _components;

        private void Awake()
        {
            RegisterInstance();
        }

        private void OnValidate()
        {
            _configurations.ValidateSequences(this);
        }

        // private async void Start()
        // {
        //     #if DEBUG
        //                 LoadSequence(_configurations.TestSequence, isInitialLoad: true);
        //     #else
        //                 LoadSequence(_configurations.Sequences);
        //     #endif
        // }


        private async void LoadSequence(LoadSequence sequence, bool isInitialLoad = false)
        {
            var loader = sequence.UseDefaultLoader ? _components.DefaultLoader : sequence.LoaderUI;

            if (isInitialLoad)
            {
                loader.InitiateLoader(LoaderUI.State.Closed);
            }
            else
            {
                await loader.ToggleLoader(open: false);
            }

            await LoadManagers(sequence);
            await LoadEntities(sequence);

            await loader.ToggleLoader(open: true);
        }

        private async Awaitable LoadManagers(LoadSequence sequence)
        {
            foreach (LoadSequence.Entity manager in sequence.Managers)
            {
                if (manager.DataType != Entity.Type.GameObject)
                {
                    throw new Exception($"The LoadSequence {sequence.Name} has a non GameObject Manager!");
                }

                var entity = Instantiate(manager.GameObject, this.transform);
                var loadableEntity = entity.GetComponent<ILoadableEntity>();

                await loadableEntity.Load();
            }
        }

        private async Awaitable LoadEntities(LoadSequence sequence)
        {
            foreach (var entity in sequence.Entities)
            {


            }
        }

        //we need to be able to register a load sequence
        [Serializable]
        private class Configurations
        {
#if DEBUG
            [field: SerializeField] public List<LoadSequence> TestSequence { get; set; }
#endif
            [field: SerializeField] public List<LoadSequence> Sequences { get; set; }


            public void ValidateSequences(LoadManager parent)
            {
#if DEBUG
                if (TestSequence != null && TestSequence.Count > 0)
                {
                    ValidateSequence(TestSequence, parent);
                }
#endif
                ValidateSequence(Sequences, parent);
            }

            private void ValidateSequence(List<LoadSequence> sequences, LoadManager parent)
            {
                if (TestSequence == null || TestSequence.Count == 0) return;

                foreach (var sequence in sequences)
                {
                    if (sequence == null) continue;
                    
                    sequence.IsValid(parent);
                }
            }
        }

        [Serializable]
        private class Components
        {
            [field: SerializeField] public LoaderUI DefaultLoader { get; set; }
        }
    }
}