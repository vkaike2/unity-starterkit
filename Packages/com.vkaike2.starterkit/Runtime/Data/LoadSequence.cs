using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vkaike2.StarterKit.Attributes;
using Vkaike2.StarterKit.Base.Interfaces;
using Vkaike2.StarterKit.UI;

namespace Vkaike2.StarterKit.Data
{
    [CreateAssetMenu(fileName = "load-sequence-01", menuName = "vkaike2/Load Sequence", order = 0)]
    public class LoadSequence : ScriptableObject
    {
        [SerializeField] private bool _useDefaultLoader = true;
        [SerializeField, HideIf(nameof(_useDefaultLoader))] private LoaderUI _loaderUI;
        [Space]
        [SerializeField] private List<Entity> _managers = new();
        [SerializeField] private List<Entity> _entities = new();

        public bool UseDefaultLoader => _useDefaultLoader;
        public LoaderUI LoaderUI => _loaderUI;

        public IEnumerable<ILoadableEntity> GetEntitiesToLoad()
        {
            foreach (var entity in _entities)
            {
                if (entity == null || !entity.ShouldLoad) continue;
                if (entity.LoadableEntity == null) continue;

                yield return entity.LoadableEntity;
            }
        }

        private void OnValidate()
        {
            for (var index = 0; index < _entities.Count; index++)
            {
                _entities[index]?.IsValid(index, this);
            }
        }

        [Serializable]
        private class Entity
        {
            [SerializeField] private bool _shouldNotLoad = true;
            [SerializeField, HideIf(nameof(_shouldNotLoad))] private Type _type;
            [SerializeField] private UnityEngine.Object _object;
            [SerializeField] private GameObject _gameObject;
            [SerializeField] private Scene _scene;

            public bool ShouldLoad => _shouldNotLoad;

            public ILoadableEntity? LoadableEntity { get; set; }
            public Scene LoadableScene => _scene;

            private const string _emptyElementError = "[{0}] Element {index} of '{1}' is empty.";
            private const string _nonLoadableEntityError = 
                "[{0}] Element {1} of '{2}' is '{3}' ({4}), which does not implement {5} and cannot be loaded.";

            private ILoadableEntity? GetLoadableEntity()
            {
                switch (_type)
                {
                    case Type.Scene: return null;
                    case Type.GameObject: return _gameObject.GetComponent<ILoadableEntity>();
                    case Type.Object: return _object as ILoadableEntity;

                    default: throw new NotImplementedException();
                }
            }

            public bool IsValid(int index, UnityEngine.Object context)
            {
                switch (_type)
                {
                    case Type.Scene: return ValidateScene(index, context);
                    case Type.GameObject: return ValidateGameObject(index, context);
                    case Type.Object: return ValidateObject(index, context);

                    default: return false;
                }
            }

            private bool ValidateObject(int index, UnityEngine.Object context)
            {
                if (_object == null)
                {
                    Debug.LogError(
                        string.Format(_emptyElementError, nameof(LoadSequence), context.name),
                        context);

                    return false;
                }

                if (_object is ILoadableEntity) return true;

                Debug.LogError(
                    $"[{nameof(LoadSequence)}] Element {index} of '{context.name}' is " +
                    $"'{_object.name}' ({_object.GetType().Name}), which does not implement " +
                    $"{nameof(ILoadableEntity)} and cannot be loaded.",
                    context);

                return false;
            }

            private bool ValidateGameObject(int index, UnityEngine.Object context)
            {
                if (_gameObject == null)
                {
                    Debug.LogError(
                        string.Format(_emptyElementError, nameof(LoadSequence), context.name),
                        context);
                    return false;
                }

                if(_gameObject.GetComponent<ILoadableEntity>() != null) return true;



                return false;
            }

            private bool ValidateScene(int index, UnityEngine.Object context)
            {

                return false;
            }

            private enum Type
            {
                Scene,
                GameObject,
                Object,
            }
        }
    }
}
