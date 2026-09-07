using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vkaike2.StarterKit.Base.Abstracts;
using Vkaike2.StarterKit.Base.Models;
using Vkaike2.StarterKit.Managers.LoadManager.Base;
using Vkaike2.StarterKit.UI;

namespace Vkaike2.StarterKit.Managers.LoadManager
{
    public class LoadManager : MySingleton<LoadManager>
    {
        [SerializeField] private Configurations _configurations;
        [SerializeField] private Components _components;

        private SequenceRunner _sequenceRunner;

        private SequenceRunner Runner => _sequenceRunner ??= new SequenceRunner(this);

        private void Awake()
        {
            RegisterInstance();
        }

        private void OnValidate()
        {
            _configurations.ValidateSequences(this);
        }

        private async void Start()
        {
            await UnloadActiveScenes();
#if DEBUG
            LoadInitialSequence(_configurations.TestSequence);
#else
            LoadInitialSequence(_configurations.Sequences);
#endif
        }

        private async void LoadInitialSequence(List<LoadSequence> sequences)
        {
            var sequencesToLoad = sequences.Where(e => e.IsActive).ToList();

            foreach (var sequence in sequencesToLoad)
            {
                await LoadSequence(sequence, isInitialLoad: true);
            }
        }

        private async Awaitable LoadSequence(LoadSequence sequence, bool isInitialLoad = false)
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

            await Runner.LoadEntities(sequence);

            await loader.ToggleLoader(open: true);
        }

        private async Awaitable UnloadActiveScenes()
        {
            var sceneToKeep = gameObject.scene;

            if (SceneManager.GetActiveScene() != sceneToKeep)
            {
                SceneManager.SetActiveScene(sceneToKeep);
            }

            var scenesToUnload = new List<Scene>();

            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);

                if (scene == sceneToKeep || !scene.isLoaded) continue;

                scenesToUnload.Add(scene);
            }

            foreach (var scene in scenesToUnload)
            {
                var unloadOperation = SceneManager.UnloadSceneAsync(scene);

                if (unloadOperation == null) continue;

                while (!unloadOperation.isDone)
                {
                    await Awaitable.NextFrameAsync(destroyCancellationToken);
                }
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


            public void ValidateSequences(UnityEngine.Object context)
            {
#if DEBUG
                SequenceRunner.ValidateSequences(TestSequence, context);
#endif
                SequenceRunner.ValidateSequences(Sequences, context);
            }
        }

        [Serializable]
        private class Components
        {
            [field: SerializeField] public LoaderUI DefaultLoader { get; set; }
        }
    }
}