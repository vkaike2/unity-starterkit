using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vkaike2.StarterKit.Attributes;
using Vkaike2.StarterKit.Base.Interfaces;
using Vkaike2.StarterKit.UI;

namespace Vkaike2.StarterKit.Data
{
    [CreateAssetMenu(fileName = "So_LoadSequence-01", menuName = "vkaike2/Load Sequence", order = 0)]
    public class LoadSequence : ScriptableObject
    {
        [SerializeField] private bool _useDefaultLoader = true;
        [SerializeField, HideIf(nameof(_useDefaultLoader))] private LoaderUI _loaderUI;
        [field: Space]
        [field: SerializeField] public List<Entity> Managers { get; private set; } = new();
        [field: SerializeField] public List<Entity> Entities { get; private set; } = new();

        public bool UseDefaultLoader => _useDefaultLoader;
        public LoaderUI LoaderUI => _loaderUI;


        private void OnValidate()
        {
            for (var index = 0; index < Managers.Count; index++)
            {
                Managers[index]?.IsValid(index, this);
            }

            for (var index = 0; index < Entities.Count; index++)
            {
                Entities[index]?.IsValid(index, this);
            }
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
                if(_dataType != Type.Object) return null;
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
                int index, UnityEngine.Object context, string elementDescription)
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
