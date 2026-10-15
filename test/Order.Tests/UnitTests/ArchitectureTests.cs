using System.Reflection;

namespace Order.Tests.UnitTests
{
    /// <summary>
    /// Onion rule: a layer may only reference its own layer and the layers inside it
    /// (Presentation → Infrastructure → Application → Domain).
    /// </summary>
    public sealed class ArchitectureTests
    {
        private const string Presentation = "Presentation";
        private const string Infrastructure = "Infrastructure";
        private const string Application = "Application";
        private const string Domain = "Domain";

        /// <summary>
        /// Outermost first; the index is the layer's depth in the onion.
        /// </summary>
        private static readonly string[] Layers = [Presentation, Infrastructure, Application, Domain];

        private static readonly Assembly[] SolutionAssemblies = LoadSolutionAssemblies();

        [Test]
        public void SolutionAssemblies_AllBelongToALayer()
        {
            // Act
            string[] withoutLayer = SolutionAssemblies.Select(assembly => assembly.GetName().Name!).Where(name => GetLayer(name) is null).ToArray();

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(SolutionAssemblies, Is.Not.Empty);
                Assert.That(withoutLayer, Is.Empty);
            }
        }

        [TestCase(Presentation)]
        [TestCase(Infrastructure)]
        [TestCase(Application)]
        [TestCase(Domain)]
        public void Layer_ReferencesOnlyItselfAndInnerLayers(string layer)
        {
            // Assemble
            int depth = Array.IndexOf(Layers, layer);

            // Act
            string[] violations = SolutionAssemblies
                .Where(assembly => GetLayer(assembly.GetName().Name!) == layer)
                .SelectMany(assembly => assembly.GetReferencedAssemblies()
                    .Select(reference => reference.Name!)
                    .Where(reference => GetLayer(reference) is string referencedLayer && Array.IndexOf(Layers, referencedLayer) < depth)
                    .Select(reference => $"{assembly.GetName().Name} -> {reference}"))
                .ToArray();

            // Assert
            Assert.That(violations, Is.Empty);
        }

        private static string? GetLayer(string assemblyName)
        {
            string[] parts = assemblyName.Split('.');
            return parts.Length > 1 && parts[0] == nameof(Order) && Layers.Contains(parts[1]) ? parts[1] : null;
        }

        private static Assembly[] LoadSolutionAssemblies()
        {
            string testAssemblyName = typeof(ArchitectureTests).Assembly.GetName().Name!;

            return Directory.GetFiles(AppContext.BaseDirectory, $"{nameof(Order)}.*.dll")
                .Select(path => Path.GetFileNameWithoutExtension(path))
                .Where(name => name != testAssemblyName)
                .Select(name => Assembly.Load(new AssemblyName(name)))
                .ToArray();
        }
    }
}