using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vkaike2.StarterKit.Base.Abstracts;

namespace Scripts.Entities
{
    public class Board : MonoBehaviour
    {
        [SerializeField] private Configurations _configurations;
        [SerializeField] private Components _components;

        public IReadOnlyList<BoardTile> Tiles => _tiles;

        private readonly List<BoardTile> _tiles = new();

        private void OnValidate()
        {
            _configurations.ValidateFields(this);
            _components.ValidateFields(this);
        }

        public void Initialize()
        {
            DestroyChildren();
            BuildTiles();
        }

        public bool TryGetTile(Vector2Int coordinate, out BoardTile boardTile)
        {
            boardTile = _tiles.FirstOrDefault(tile => tile.Coordinate == coordinate);

            return boardTile != null;
        }

        private void DestroyChildren()
        {
            _tiles.Clear();

            for (var index = transform.childCount - 1; index >= 0; index--)
            {
                Destroy(transform.GetChild(index).gameObject);
            }
        }

        private void BuildTiles()
        {
            var center = (_configurations.Size - Vector2Int.one) / 2;

            for (var x = 0; x < _configurations.Size.x; x++)
            {
                for (var y = 0; y < _configurations.Size.y; y++)
                {
                    _tiles.Add(BuildTile(new Vector2Int(x, y), center));
                }
            }
        }

        private BoardTile BuildTile(Vector2Int coordinate, Vector2Int center)
        {
            var offset = coordinate - center;

            var boardTile = Instantiate(_components.BoardTile, transform);

            boardTile.name = $"[{coordinate.x},{coordinate.y}] {nameof(BoardTile)}";

            boardTile.transform.localPosition = new Vector3(
                (offset.x - offset.y) * _configurations.CellSize.x / 2f,
                (offset.x + offset.y) * _configurations.CellSize.y / 2f,
                0f);

            boardTile.Initialize(coordinate);

            return boardTile;
        }

        [Serializable]
        private class Configurations : ValidatableFields
        {
            [field: SerializeField] public Vector2Int Size { get; set; } = new(6, 6);
            [field: SerializeField] public Vector2 CellSize { get; set; } = new(1f, 0.5f);
        }

        [Serializable]
        private class Components : ValidatableFields
        {
            [field: SerializeField] public BoardTile BoardTile { get; set; }

            protected override void Validate()
            {
                ValidateNull(BoardTile, nameof(BoardTile));
            }
        }
    }
}
