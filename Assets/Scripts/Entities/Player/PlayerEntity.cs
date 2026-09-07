using Scripts.Enums;
using UnityEngine;
using Vkaike2.StarterKit.Base.Interfaces;
using Vkaike2.StarterKit.Enums;
using Vkaike2.StarterKit.Managers;

namespace Scripts.Entities.Player
{
    public class PlayerEntity : MonoBehaviour, ILoadableEntity
    {

        public async Awaitable Load()
        {
            UpdateManager.Instance.Register(
                UpdateType.FixedUpdate,
                UpdateOrder.Entities,
                MyFixedUpdate);
        }

        private void MyFixedUpdate() { }
    }
}
