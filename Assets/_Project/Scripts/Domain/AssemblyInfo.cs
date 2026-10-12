using System.Runtime.CompilerServices;

// Tests may set up states directly (e.g. a captured territory) without replaying a whole game.
[assembly: InternalsVisibleTo("TrashCats.Tests.EditMode")]
