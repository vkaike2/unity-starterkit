using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vkaike2.StarterKit.Attributes;
using Vkaike2.StarterKit.Base.Interfaces;
using Vkaike2.StarterKit.UI;

namespace Vkaike2.StarterKit.Base.Models
{
    [Serializable]
    public class LoadSequence
    {
        [SerializeField] private string _name;
        [SerializeField] private bool _isActive = true;

        [SerializeField, HideIf(nameof(_isActive), false, Header = "Configurations")]
        private bool _useDefaultLoader = true;
        [SerializeField, HideIf(nameof(_useDefaultLoader)), HideIf(nameof(_isActive), false)] private LoaderUI _loaderUI;
        [SerializeField, HideIf(nameof(_isActive), false)] private EntitiesWrapper _loadableEntities;


        public bool IsActive { get; set; }
        public string Name => _name;

        public bool UseDefaultLoader => _useDefaultLoader;
        public LoaderUI LoaderUI => _loaderUI;
        public List<Entity> Managers => _loadableEntities.Managers;
        public List<Entity> Entities => _loadableEntities.Entities;


        public void IsValid(UnityEngine.Object context)
        {
            for (var index = 0; index < _loadableEntities.Managers.Count; index++)
            {
                _loadableEntities.Managers[index]?.IsValid(index, context);
            }

            for (var index = 0; index < _loadableEntities.Entities.Count; index++)
            {
                _loadableEntities.Entities[index]?.IsValid(index, context);
            }
        }

        [Serializable]
        public class EntitiesWrapper
        {
            [field: SerializeField] public List<Entity> Managers { get; private set; }
            [field: SerializeField] public List<Entity> Entities { get; private set; }
        }

        [Serializable]
        public class Entity
        {
            [HideInInspector] public string name;

            [SerializeField] private bool _shouldLoad = true;
            [SerializeField, HideIf(nameof(_shouldLoad), false)] private Type _dataType;

            [field: SerializeField, HideIf(nameof(_shouldLoad), false), ShowIf(nameof(_dataType), Type.Scene)]
            public string ScenePath { get; private set; }

            [field: SerializeField, HideIf(nameof(_shouldLoad), false), ShowIf(nameof(_dataType), Type.GameObject)]
            public GameObject GameObject { get; private set; }

            [SerializeField, HideIf(nameof(_shouldLoad), false), ShowIf(nameof(_dataType), Type.Object)]
            private UnityEngine.Object _object;

            public bool ShouldLoad => _shouldLoad;
            public Type DataType => _dataType;


            public ILoadableEntity? GetLoadableEntity()
            {
                if (_dataType != Type.Object) return null;
                return _object as ILoadableEntity;
            }

            public bool IsValid(int index, UnityEngine.Object context)
            {
                name = string.Empty;
                name = $"[{_dataType}] ";
                switch (_dataType)
                {
                    case Type.Scene:
                        ValidateScene(index, context);
                        var sceneName = ScenePath.Split('/').Last();
                        name += ScenePath != null ? $"{sceneName}" : "None";
                        break;
                    case Type.GameObject:
                        ValidateGameObject(index, context);
                        name += ScenePath != null ? $"{GameObject.name}" : "None";
                        break;
                    case Type.Object:
                        ValidateObject(index, context);
                        name += ScenePath != null ? $"{_object.name}" : "None";
                        break;

                    default: return false;
                }

                return true;
            }

            private bool ValidateObject(int index, UnityEngine.Object context)
            {
                if (_object == null) return LogEmptyElement(index, context);

                if (_object is ILoadableEntity) return true;

                return LogNotLoadableElement(
                    index, context, $"'{_object.name}' ({_object.GetType().Name})");
            }

            private bool ValidateGameObject(int index, UnityEngine.Object context)
            {
                if (GameObject == null) return LogEmptyElement(index, context);

                if (GameObject.GetComponent<ILoadableEntity>() != null) return true;

                return LogNotLoadableElement(index, context, $"'{GameObject.name}'");
            }

            private bool ValidateScene(int index, UnityEngine.Object context)
            {
                if (ScenePath == null) return LogEmptyElement(index, context);

                return true;
            }

            private static bool LogEmptyElement(int index, UnityEngine.Object context)
            {
                Debug.LogError(
                    $"[{nameof(LoadSequence)}] Element {index} of '{context.name}' is empty.",
                    context);

                return false;
            }

            private static bool LogNotLoadableElement(
                int index,
                UnityEngine.Object context,
                string elementDescription)
            {
                Debug.LogError(
                    $"[{nameof(LoadSequence)}] Element {index} of '{context.name}' is " +
                    $"{elementDescription}, which does not implement " +
                    $"{nameof(ILoadableEntity)} and cannot be loaded.",
                    context);

                return false;
            }

            public enum Type
            {
                Scene,
                GameObject,
                Object,
            }
        }
    }
}
