using System.Collections.Generic;
using System.Linq;
using Scripts.Managers;
using Unity.Mathematics;
using UnityEngine;
using Vkaike2.StarterKit.Managers;

namespace Scripts.Entities.Player
{
    public partial class PlayerEntity
    {
        private class Dragging : BaseState
        {
            public override State State => State.Dragging;

            private int _movementRange = 3;
            private List<BoardTile> _possibleCoordinatesToMove = new List<BoardTile>();

            public override void OnEnter()
            {
                MusicManager.Instance.Play(_components.PlacementSoundEffect, Vkaike2.StarterKit.Enums.AudioChannel.SoundEffect);
                _possibleCoordinatesToMove = TryToCalculatePossibleCoordinatesToMove(_movementRange);
                if (_possibleCoordinatesToMove.Count == 0)
                {
                    _parent.ChangeState(State.Idle);
                    return;
                }
            }

            public override void OnExit()
            {
                MusicManager.Instance.Play(_components.PlacementSoundEffect, Vkaike2.StarterKit.Enums.AudioChannel.SoundEffect);
                _possibleCoordinatesToMove.Clear();
            }

            public override void OnDrag(Vector2 worldPosition)
            {
                var boardTile = MapManager.Instance.GetTileAtWorldPosition(worldPosition);

                _components.SpritePosition.position = worldPosition;

                if (boardTile == null) return;

                MoveToTile(boardTile);
            }

            private void MoveToTile(BoardTile boardTile)
            {
                if (!_possibleCoordinatesToMove.Any(e => e.Coordinate == boardTile.Coordinate)) return;

                CurrentTile = boardTile;

                SnapToCurrentTile(snappingOnlyShadow: true);
            }

            private List<BoardTile> TryToCalculatePossibleCoordinatesToMove(int movementRange)
            {
                if (CurrentTile == null) return new List<BoardTile>();

                var possibleCoordinates = GetCoordinatesInRange(CurrentTile.Coordinate, movementRange);

                var result = possibleCoordinates
                    .Select(coordinate => MapManager.Instance.GetTile(coordinate))
                    .Where(tile => tile != null)
                    .ToList();

                return result;
            }

            private static List<Vector2Int> GetCoordinatesInRange(Vector2Int center, int range)
            {
                var coordinates = new List<Vector2Int>();


                for (int i = 0; i < range; i++)
                {
                    //horizontal
                    coordinates.Add(new Vector2Int(center.x + i, center.y));
                    coordinates.Add(new Vector2Int(center.x - i, center.y));
                    //vertical
                    coordinates.Add(new Vector2Int(center.x, center.y + i));
                    coordinates.Add(new Vector2Int(center.x, center.y - i));
                    //diagonals
                    coordinates.Add(new Vector2Int(center.x + i, center.y + i));
                    coordinates.Add(new Vector2Int(center.x - i, center.y - i));
                }
                coordinates.Add(center);
                return coordinates;

            }

        }
    }
}
