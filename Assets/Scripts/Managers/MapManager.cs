using System;
using System.Collections.Generic;
using Scripts.Entities;
using UnityEngine;
using Vkaike2.StarterKit.Base.Abstracts;

namespace Scripts.Managers
{
    public class MapManager : MySingleton<MapManager>
    {
        [SerializeField] private Configurations _configurations;
        [SerializeField] private Components _components;

        public IReadOnlyList<BoardTile> Tiles => _components.Board.Tiles;

        private void OnValidate()
        {
            _configurations.ValidateFields(this);
            _components.ValidateFields(this);
        }

        protected override async Awaitable OnLoad()
        {
            _components.Board.Initialize();
        }

        public bool TryGetTile(Vector2Int coordinate, out BoardTile boardTile)
        {
            return _components.Board.TryGetTile(coordinate, out boardTile);
        }

        [Serializable]
        private class Configurations : ValidatableFields
        {

        }

        [Serializable]
        private class Components : ValidatableFields
        {
            [field: SerializeField] public Board Board { get; set; }

            protected override void Validate()
            {
                ValidateNull(Board, nameof(Board));
            }
        }
    }
}
