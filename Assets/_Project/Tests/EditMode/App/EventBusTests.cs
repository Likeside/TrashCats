using System;
using System.Collections.Generic;
using NUnit.Framework;
using TrashCats.App;
using TrashCats.Domain;

namespace TrashCats.Tests
{
    public sealed class EventBusTests
    {
        private sealed class Ping { public int N; }

        [Test]
        public void Handlers_ReceiveExactAndBaseTypes()
        {
            var bus = new EventBus();
            var exact = 0;
            var all = 0;
            bus.Subscribe<NeedsChanged>(_ => exact++);
            bus.Subscribe<DomainEvent>(_ => all++);

            bus.Publish(new NeedsChanged(new CatId("cat_a"), 1, 1));
            bus.Publish(new ResourcesChanged(1, 1, 1, 0));

            Assert.That((exact, all), Is.EqualTo((1, 2)));
        }

        [Test]
        public void Dispose_Unsubscribes()
        {
            var bus = new EventBus();
            var count = 0;
            var subscription = bus.Subscribe<Ping>(_ => count++);

            bus.Publish(new Ping());
            subscription.Dispose();
            subscription.Dispose();
            bus.Publish(new Ping());

            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public void PublishFromHandler_IsDeliveredAfterCurrentEvent()
        {
            var bus = new EventBus();
            var order = new List<string>();
            bus.Subscribe<Ping>(p =>
            {
                order.Add($"a{p.N}");
                if (p.N == 1) bus.Publish(new Ping { N = 2 });
            });
            bus.Subscribe<Ping>(p => order.Add($"b{p.N}"));

            bus.Publish(new Ping { N = 1 });

            Assert.That(order, Is.EqualTo(new[] { "a1", "b1", "a2", "b2" }));
        }

        [Test]
        public void ThrowingHandler_DoesNotStopOthers_AndIsReported()
        {
            var bus = new EventBus();
            Exception reported = null;
            var reached = false;
            bus.OnHandlerException = e => reported = e;
            bus.Subscribe<Ping>(_ => throw new InvalidOperationException("boom"));
            bus.Subscribe<Ping>(_ => reached = true);

            bus.Publish(new Ping());

            Assert.That(reached, Is.True);
            Assert.That(reported, Is.TypeOf<InvalidOperationException>());
        }
    }
}
