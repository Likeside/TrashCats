using System;
using System.Collections.Generic;

namespace TrashCats.App
{
    /// <summary>
    /// Synchronous main-thread event bus. Handlers subscribed to a base type also receive derived events.
    /// Events published from inside a handler are queued and delivered after the current one, so handlers never
    /// observe a nested, half-delivered sequence. A throwing handler is reported and does not stop the others.
    /// </summary>
    public sealed class EventBus
    {
        private readonly Dictionary<Type, List<Action<object>>> _handlers = new Dictionary<Type, List<Action<object>>>();
        private readonly Queue<object> _queue = new Queue<object>();
        private bool _dispatching;

        /// <summary>Called when a handler throws. Without it the exception is swallowed so other handlers still run.</summary>
        public Action<Exception> OnHandlerException { get; set; }

        public IDisposable Subscribe<T>(Action<T> handler) where T : class
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (!_handlers.TryGetValue(typeof(T), out var list))
                _handlers[typeof(T)] = list = new List<Action<object>>();
            Action<object> wrapper = evt => handler((T)evt);
            list.Add(wrapper);
            return new Subscription(() => list.Remove(wrapper));
        }

        public void Publish(object evt)
        {
            if (evt == null) throw new ArgumentNullException(nameof(evt));
            _queue.Enqueue(evt);
            if (_dispatching)
                return;

            _dispatching = true;
            try
            {
                while (_queue.Count > 0)
                    Dispatch(_queue.Dequeue());
            }
            finally
            {
                _dispatching = false;
            }
        }

        public void PublishAll<T>(IEnumerable<T> events) where T : class
        {
            foreach (var evt in events)
                Publish(evt);
        }

        private void Dispatch(object evt)
        {
            for (var type = evt.GetType(); type != null; type = type.BaseType)
            {
                if (!_handlers.TryGetValue(type, out var list))
                    continue;

                foreach (var handler in list.ToArray())
                {
                    try
                    {
                        handler(evt);
                    }
                    catch (Exception e)
                    {
                        OnHandlerException?.Invoke(e);
                    }
                }
            }
        }

        private sealed class Subscription : IDisposable
        {
            private Action _dispose;

            public Subscription(Action dispose) => _dispose = dispose;

            public void Dispose()
            {
                _dispose?.Invoke();
                _dispose = null;
            }
        }
    }
}
