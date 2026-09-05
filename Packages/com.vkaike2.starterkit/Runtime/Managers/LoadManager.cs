using System;
using System.Collections.Generic;
using UnityEngine;
using Vkaike2.StarterKit.Base.Abstracts;
using Vkaike2.StarterKit.Base.Interfaces;
using Vkaike2.StarterKit.Data;
using Vkaike2.StarterKit.UI;

namespace Vkaike2.StarterKit.Managers
{
    public class LoadManager : Singleton<LoadManager>
    {
        [SerializeField] private Configurations _configurations;
        [SerializeField] private Components _components;

        
        private void Awake() => RegisterInstance();

        private async void Start()
        {
#if DEBUG
            LoadSequence(_configurations.TestSequence, isInitialLoad: true);
#else
            LoadSequence(_configurations.Sequences);
#endif
        }


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

            foreach (LoadSequence.Entity manager in sequence.Managers)
            {
                var entity = Instantiate(manager.GameObject, this.transform);
                var loadableEntity = entity.GetComponent<ILoadableEntity>();
                if (manager.DataType != Data.LoadSequence.Entity.Type.GameObject)
                {
                    throw new Exception($"The LoadSequence {sequence.name} has a non GameObject Manager!");
                }

                await loadableEntity.Load();
            }

            foreach (var entity in sequence.Entities)
            {

            }

            await loader.ToggleLoader(open: true);
        }

        //we need to be able to register a load sequence
        [Serializable]
        private class Configurations
        {
            [field: SerializeField] public bool IsInternal { get; private set; }
            [field: Space(30)]
#if DEBUG
            [field: SerializeField] public LoadSequence TestSequence { get; set; }
#endif
            [field: SerializeField] public List<LoadSequence> Sequences { get; set; }
        }

        [Serializable]
        private class Components
        {
            [field: SerializeField] public LoaderUI DefaultLoader { get; set; }
        }
    }
}