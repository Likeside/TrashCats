using System.Collections.Generic;

namespace TrashCats.Domain
{
    /// <summary>Onboarding progress: finished steps and one-shot hints (by id) and whether the player skipped the chain.</summary>
    public sealed class TutorialState
    {
        private readonly HashSet<string> _completed;

        public bool Skipped { get; internal set; }
        public IEnumerable<string> Completed => _completed;

        public TutorialState(IEnumerable<string> completed = null, bool skipped = false)
        {
            _completed = new HashSet<string>(completed ?? new string[0]);
            Skipped = skipped;
        }

        public bool IsDone(string id) => _completed.Contains(id);

        /// <summary>Returns false if the step was already done.</summary>
        internal bool Complete(string id) => _completed.Add(id);
    }

    /// <summary>Domain operations on <see cref="TutorialState"/> (the state itself is read-only outside the domain).</summary>
    public static class TutorialRules
    {
        /// <summary>Marks a step or hint as done; false if it already was.</summary>
        public static bool Complete(GameWorld world, string id) => world.Tutorial.Complete(id);

        public static void Skip(GameWorld world) => world.Tutorial.Skipped = true;
    }
}
