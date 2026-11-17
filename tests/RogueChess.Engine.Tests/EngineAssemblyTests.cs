using System.Linq;
using RogueChess.Engine;
using Xunit;

namespace RogueChess.Engine.Tests;

public class EngineAssemblyTests
{
    [Fact]
    public void Engine_assembly_loads()
    {
        Assert.Equal("RogueChess.Engine", EngineInfo.Name);
    }

    [Fact]
    public void Engine_does_not_depend_on_Godot()
    {
        // The engine must stay runnable headless and in plain unit tests (docs/PORT_PLAN.md §3).
        var refs = typeof(EngineInfo).Assembly.GetReferencedAssemblies().Select(a => a.Name);
        Assert.DoesNotContain(refs, n => n != null && n.StartsWith("Godot"));
    }
}
