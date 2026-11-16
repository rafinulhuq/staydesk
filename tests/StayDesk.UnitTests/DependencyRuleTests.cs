using System.Reflection;

namespace StayDesk.UnitTests;

// SRS §3 dependency rule. GetReferencedAssemblies lists only assemblies whose
// types are actually used, so this catches real usage across layers, not unused
// project references.
public class DependencyRuleTests
{
    private static string[] StayDeskReferencesOf(string assemblyName) =>
        Assembly.Load(assemblyName)
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => n.StartsWith("StayDesk.", StringComparison.Ordinal))
            .ToArray();

    [Fact]
    public void Domain_depends_on_no_other_StayDesk_layer() =>
        Assert.Empty(StayDeskReferencesOf("StayDesk.Domain"));

    [Fact]
    public void Application_depends_only_on_Domain() =>
        Assert.All(StayDeskReferencesOf("StayDesk.Application"), n => Assert.Equal("StayDesk.Domain", n));
}
