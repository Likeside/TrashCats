using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrashCats.Presentation.UI
{
    /// <summary>
    /// Sheets and modals shown above the current screen. Only the top one receives "Back" (Android back / Escape).
    /// </summary>
    public sealed class OverlayStack
    {
        private readonly List<(GameObject Root, Action OnClosed)> _stack = new List<(GameObject, Action)>();

        public bool IsEmpty => _stack.Count == 0;

        public void Push(GameObject root, Action onClosed = null)
        {
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            _stack.Add((root, onClosed));
        }

        public void Close(GameObject root)
        {
            var index = _stack.FindIndex(e => e.Root == root);
            if (index < 0)
                return;

            var entry = _stack[index];
            _stack.RemoveAt(index);
            entry.Root.SetActive(false);
            entry.OnClosed?.Invoke();
        }

        /// <summary>Closes the top overlay. Returns false if there was nothing to close.</summary>
        public bool Back()
        {
            if (_stack.Count == 0)
                return false;
            Close(_stack[_stack.Count - 1].Root);
            return true;
        }
    }
}
