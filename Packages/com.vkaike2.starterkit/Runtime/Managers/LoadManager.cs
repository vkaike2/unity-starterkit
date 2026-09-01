using System;
using System.Collections.Generic;
using UnityEngine;
using Vkaike2.StarterKit.Base.Abstracts;
using Vkaike2.StarterKit.Data;
using Vkaike2.StarterKit.UI;

namespace Vkaike2.StarterKit.Managers
{
    public class LoadManager : Singleton<LoadManager>
    {
        [SerializeField] private Configurations _configurations;
        [SerializeField] private Components _components;


        private void Awake() => RegisterInstance();

        private void Start()
        {
#if DEBUG
            LoadSequence(_configurations.TestSequence);
#else
            LoadSequence(_configurations.Sequences);
#endif
        }


        private void LoadSequence(LoadSequence sequence)
        {

        }

        //we need to be able to register a load sequence
        [Serializable]
        private class Configurations
        {
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