using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vkaike2.StarterKit.Base.Interfaces;
using Vkaike2.StarterKit.Base.Models;
using static Vkaike2.StarterKit.Base.Models.LoadSequence;

namespace Vkaike2.StarterKit.Managers.LoadManager.Base
{
    public class SequenceRunner
    {
        private readonly MonoBehaviour _owner;
        private readonly Dictionary<string, SceneLoader> _sceneLoaders = new();

        public SequenceRunner(MonoBehaviour owner)
        {
            _owner = owner;
        }

        public IReadOnlyDictionary<string, SceneLoader> SceneLoaders => _sceneLoaders;

        public static void ValidateSequences(List<LoadSequence> sequences, UnityEngine.Object context)
        {
            if (sequences == null) return;

            foreach (var sequence in sequences)
            {
                if (sequence == null) continue;

                sequence.IsValid(context);
            }
        }

        public async Awaitable LoadEntities(LoadSequence sequence)
        {
            foreach (var entity in sequence.Entities)
            {
                if (entity == null || !entity.ShouldLoad) continue;

                switch (entity.DataType)
                {
                    case Entity.Type.GameObject:
                        await LoadGameObjectEntity(sequence, entity);
                        break;

                    case Entity.Type.Object:
                        await LoadObjectEntity(sequence, entity);
                        break;

                    case Entity.Type.Scene:
                        await LoadSceneEntity(sequence, entity);
                        break;
                }

                await Awaitable.NextFrameAsync(_owner.destroyCancellationToken);
            }
        }

        private async Awaitable LoadGameObjectEntity(LoadSequence sequence, Entity entity)
        {
            var parent = entity.Parent != null ? entity.Parent : _owner.transform;
            var instance = UnityEngine.Object.Instantiate(entity.GameObject, parent);
            var loadableEntity = instance.GetComponent<ILoadableEntity>();

            if (loadableEntity == null)
            {
                throw new Exception(
                    $"The LoadSequence {sequence.Name} has the GameObject '{entity.GameObject.name}', " +
                    $"which does not implement {nameof(ILoadableEntity)}!");
            }

            await loadableEntity.Load();
        }

        private async Awaitable LoadObjectEntity(LoadSequence sequence, Entity entity)
        {
            var loadableEntity = entity.GetLoadableEntity();

            if (loadableEntity == null)
            {
                throw new Exception(
                    $"The LoadSequence {sequence.Name} has an Object which does not implement " +
                    $"{nameof(ILoadableEntity)}!");
            }

            await loadableEntity.Load();
        }

        private async Awaitable LoadSceneEntity(LoadSequence sequence, Entity entity)
        {
            var loadOperation = SceneManager.LoadSceneAsync(entity.ScenePath, LoadSceneMode.Additive);

            if (loadOperation == null)
            {
                throw new Exception(
                    $"The LoadSequence {sequence.Name} points at the scene '{entity.ScenePath}', " +
                    $"which could not be loaded!");
            }

            while (!loadOperation.isDone)
            {
                await Awaitable.NextFrameAsync(_owner.destroyCancellationToken);
            }

            var scene = SceneManager.GetSceneByPath(entity.ScenePath);
            var sceneLoader = FindSceneLoader(scene);

            if (sceneLoader == null)
            {
                throw new Exception($"The scene '{entity.ScenePath}' has no {nameof(SceneLoader)}!");
            }

            _sceneLoaders[entity.ScenePath] = sceneLoader;

            await sceneLoader.Load();
        }

        private static SceneLoader FindSceneLoader(Scene scene)
        {
            if (!scene.IsValid()) return null;

            foreach (var rootGameObject in scene.GetRootGameObjects())
            {
                var sceneLoader = rootGameObject.GetComponentInChildren<SceneLoader>(includeInactive: true);

                if (sceneLoader != null) return sceneLoader;
            }

            return null;
        }
    }
}
