using UnityEngine;

namespace Vkaike2.StarterKit.Base.Interfaces
{
    public interface ILoadableEntity
    {
        Awaitable Load();
    }
}
