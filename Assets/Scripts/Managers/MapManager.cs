using System;
using UnityEngine;
using UnityEngine.Tilemaps;
using Vkaike2.StarterKit.Base.Abstracts;
using Vkaike2.StarterKit.Base.Interfaces;

namespace Scripts.Managers
{
    public class MapManager : MySingleton<MapManager>
    {
        [SerializeField] private Configurations _configurations;
        [SerializeField] private Components _components;

        public Tilemap TileMap => _components.TileMap;

        [Serializable]
        private class Configurations
        {

        }

        [Serializable]
        private class Components
        {
            [field: SerializeField] public Tilemap TileMap { get; set; }
        }
    }
}