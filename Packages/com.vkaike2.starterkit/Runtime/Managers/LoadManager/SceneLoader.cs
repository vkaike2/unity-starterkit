
using System;
using System.Collections.Generic;
using UnityEngine;
using Vkaike2.StarterKit.Base.Interfaces;
using Vkaike2.StarterKit.Base.Models;

namespace Vkaike2.StarterKit.Managers.LoadManager
{
    public class SceneLoader : MonoBehaviour, ILoadableEntity
    {
        [SerializeField] private Configurations _configurations;
        [SerializeField] private Components _components;

        public Awaitable Load()
        {
            throw new NotImplementedException();
        }

        private class Configurations
        {
            [field: SerializeField] public List<LoadSequence> Sequences { get; set; }
        }

        [Serializable]
        private class Components
        {
        }
    }
}
