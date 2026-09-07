using System;
using UnityEngine;
using Vkaike2.StarterKit.Base.Abstracts;

namespace Scripts.Entities
{
    public class BoardTile : MonoBehaviour
    {
        [SerializeField] private Configurations _configurations;
        [SerializeField] private Components _components;

        public Vector2Int Coordinate { get; private set; }

        private void OnValidate()
        {
            _configurations.ValidateFields(this);
            _components.ValidateFields(this);
        }

        public void Initialize(Vector2Int coordinate)
        {
            Coordinate = coordinate;

            PaintSprite();
        }

        private void PaintSprite()
        {
            var isAlternated = (Coordinate.x + Coordinate.y) % 2 != 0;

            _components.SpriteRenderer.sprite = isAlternated
                ? _configurations.Alternated
                : _configurations.Normal;
        }

        [Serializable]
        private class Configurations : ValidatableFields
        {
            [field: SerializeField] public Sprite Normal { get; set; }
            [field: SerializeField] public Sprite Alternated { get; set; }

            protected override void Validate()
            {
                ValidateNull(Normal, nameof(Normal));
                ValidateNull(Alternated, nameof(Alternated));
            }
        }

        [Serializable]
        private class Components : ValidatableFields
        {
            [field: SerializeField] public SpriteRenderer SpriteRenderer { get; set; }

            protected override void Validate()
            {
                ValidateNull(SpriteRenderer, nameof(SpriteRenderer));
            }
        }
    }
}
