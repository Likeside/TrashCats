using System;
using System.Collections.Generic;

namespace TrashCats.Domain
{
    /// <summary>The gang's shared stockpile of Food and Junk. Amounts are never negative.</summary>
    public sealed class Wallet
    {
        public int Food { get; private set; }
        public int Junk { get; private set; }

        public Wallet(int food, int junk)
        {
            if (food < 0 || junk < 0) throw new ArgumentOutOfRangeException(nameof(food), "Resources must not be negative.");
            Food = food;
            Junk = junk;
        }

        public int Get(ResourceType type) => type == ResourceType.Food ? Food : Junk;

        public bool Has(ResourceType type, int amount) => Get(type) >= amount;

        public void Add(ResourceType type, int amount, List<DomainEvent> events)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount == 0) return;
            Change(type, amount, events);
        }

        /// <summary>Spends the amount if available; otherwise changes nothing and returns false.</summary>
        public bool TrySpend(ResourceType type, int amount, List<DomainEvent> events)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (!Has(type, amount)) return false;
            if (amount > 0) Change(type, -amount, events);
            return true;
        }

        private void Change(ResourceType type, int delta, List<DomainEvent> events)
        {
            if (type == ResourceType.Food) Food += delta;
            else Junk += delta;

            events.Add(new ResourcesChanged(Food, Junk,
                type == ResourceType.Food ? delta : 0,
                type == ResourceType.Junk ? delta : 0));
        }
    }
}
