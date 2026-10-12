using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TrashCats.App;
using TrashCats.Domain;

namespace TrashCats.Tests
{
    public sealed class ArchitectureTests
    {
        private static readonly Assembly DomainAsm = typeof(DomainAssembly).Assembly;
        private static readonly Assembly AppAsm = typeof(AppAssembly).Assembly;

        [Test]
        public void Domain_DoesNotReferenceUnity()
        {
            AssertNoReference(DomainAsm, "UnityEngine", "UnityEditor", "Unity.");
        }

        [Test]
        public void Domain_DoesNotReferenceOuterLayers()
        {
            AssertNoReference(DomainAsm, "TrashCats.App", "TrashCats.Infrastructure", "TrashCats.Presentation");
        }

        [Test]
        public void App_DoesNotReferenceUnity()
        {
            AssertNoReference(AppAsm, "UnityEngine", "UnityEditor", "Unity.");
        }

        [Test]
        public void App_DoesNotReferenceOuterLayers()
        {
            AssertNoReference(AppAsm, "TrashCats.Infrastructure", "TrashCats.Presentation");
        }

        private static void AssertNoReference(Assembly assembly, params string[] forbiddenPrefixes)
        {
            var offending = assembly.GetReferencedAssemblies()
                .Select(a => a.Name)
                .Where(name => forbiddenPrefixes.Any(name.StartsWith))
                .ToArray();

            Assert.That(offending, Is.Empty,
                $"{assembly.GetName().Name} must not reference: {string.Join(", ", offending)}");
        }
    }
}
