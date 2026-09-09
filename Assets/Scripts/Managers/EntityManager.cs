using System;
using System.Collections.Generic;
using Scripts.Entities.Player;
using UnityEngine;
using Vkaike2.StarterKit.Base.Abstracts;

namespace Scripts.Managers
{
    public class EntityManager : MySingleton<EntityManager>
    {
        [SerializeField] private Configurations _configurations;
        [SerializeField] private Components _components;

        public IReadOnlyList<PlayerEntity> Players => _players;

        private readonly List<PlayerEntity> _players = new();

        private void OnValidate()
        {
            _configurations.ValidateFields(this);
            _components.ValidateFields(this);
        }

        protected override async Awaitable OnLoad()
        {
            await SpawnPlayer(_configurations.InitialCoordinate);
        }

        private async Awaitable SpawnPlayer(Vector2Int coordinate)
        {
            var boardTile = MapManager.Instance.GetTile(coordinate);

            if (boardTile == null)
            {
                Debug.LogError(
                    $"[{nameof(EntityManager)}] There is no tile at {coordinate} to spawn a "
                    + $"{nameof(PlayerEntity)} on.",
                    this);

                return;
            }

            var player = Instantiate(_components.PlayerEntity, transform);

            player.name = $"[{coordinate.x},{coordinate.y}] {nameof(PlayerEntity)}";
            player.transform.position = boardTile.CenterPosition.position;

            await player.Initialize(boardTile);

            _players.Add(player);
        }

        [Serializable]
        private class Configurations : ValidatableFields
        {
            [field: SerializeField] public Vector2Int InitialCoordinate { get; set; } = Vector2Int.zero;
        }

        [Serializable]
        private class Components : ValidatableFields
        {
            [field: SerializeField] public PlayerEntity PlayerEntity { get; set; }

            protected override void Validate()
            {
                ValidateNull(PlayerEntity, nameof(PlayerEntity));
            }
        }
    }
}
