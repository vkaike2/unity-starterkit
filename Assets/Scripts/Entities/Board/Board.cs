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

        private Vector2Int Center => (_configurations.Size - Vector2Int.one) / 2;

        private Vector3 CenterOffset => _centerOffset ??= GetCenterOffset();

        private Vector3? _centerOffset;

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

        public BoardTile? GetTile(Vector2Int coordinate)
        {
            return _tiles.FirstOrDefault(tile => tile.Coordinate == coordinate);
        }

        public BoardTile? GetTileAtWorldPosition(Vector2 worldPosition)
        {
            return GetTile(GetCoordinateAtWorldPosition(worldPosition));
        }

        private Vector2Int GetCoordinateAtWorldPosition(Vector2 worldPosition)
        {
            var localPosition = transform.InverseTransformPoint(worldPosition) - CenterOffset;

            var horizontal = localPosition.x / _configurations.CellSize.x;
            var vertical = localPosition.y / _configurations.CellSize.y;

            return new Vector2Int(
                Mathf.RoundToInt(vertical + horizontal),
                Mathf.RoundToInt(vertical - horizontal)) + Center;
        }

        private Vector3 GetCenterOffset()
        {
            var boardTile = _tiles.FirstOrDefault();
            
            if (boardTile == null) return Vector3.zero;

            return transform.InverseTransformVector(
                boardTile.CenterPosition.position - boardTile.transform.position);
        }

        private void DestroyChildren()
        {
            _tiles.Clear();

            _centerOffset = null;

            for (var index = transform.childCount - 1; index >= 0; index--)
            {
                Destroy(transform.GetChild(index).gameObject);
            }
        }

        private void BuildTiles()
        {
            for (var x = 0; x < _configurations.Size.x; x++)
            {
                for (var y = 0; y < _configurations.Size.y; y++)
                {
                    _tiles.Add(BuildTile(new Vector2Int(x, y)));
                }
            }
        }

        private BoardTile BuildTile(Vector2Int coordinate)
        {
            var offset = coordinate - Center;

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
