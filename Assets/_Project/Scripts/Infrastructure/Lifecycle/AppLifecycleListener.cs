using System;
using UnityEngine;

namespace TrashCats.Infrastructure
{
    /// <summary>Forwards Unity application lifecycle callbacks as plain C# events.</summary>
    public sealed class AppLifecycleListener : MonoBehaviour
    {
        public event Action Paused;
        public event Action Resumed;
        public event Action Quitting;

        private void OnApplicationPause(bool paused)
        {
            if (paused) Paused?.Invoke();
            else Resumed?.Invoke();
        }

        private void OnApplicationQuit() => Quitting?.Invoke();
    }
}
