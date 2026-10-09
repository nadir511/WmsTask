using System;
using System.Collections.Generic;
using System.Text;

namespace Wms.Outbound.Communication
{
    public class EventPublisher : IEventPublisher
    {
        private readonly Dictionary<Type, List<Func<object, Task>>> _handlers = [];

        public void Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : class
        {
            var eventType = typeof(TEvent);
            if (!_handlers.ContainsKey(eventType))
            {
                _handlers[eventType] = new List<Func<object, Task>>();
            }

            _handlers[eventType].Add(e => handler((TEvent)e));
        }

        public async Task PublishAsync<TEvent>(TEvent @event) where TEvent : class
        {
            var eventType = typeof(TEvent);

            if (_handlers.TryGetValue(eventType, out var handlers))
            {
                foreach (var handler in handlers)
                {
                    await handler(@event);
                }
            }
        }
    }
}
