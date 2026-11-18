using System;
using RogueChess.Engine.Content;

namespace RogueChess.Engine.Core;

/// <summary>
/// An ability attached to one piece: the stateless <see cref="AbilityDef"/> plus this instance's variables
/// (charges, timers...). Variables change only through resolver proposals.
/// </summary>
public sealed class AbilityInstance
{
    internal AbilityInstance(AbilityDef def, int[]? vars)
    {
        Def = def;
        Vars = VarStore.Create(def.VarCount, vars);
    }

    public AbilityDef Def { get; }
    internal int[] Vars { get; }
    public int VarCount => Vars.Length;
    public int Var(int slot) => Vars[slot];

    internal AbilityInstance Copy() => new(Def, Vars);

    public override string ToString() => VarStore.Describe(Def.Id, Vars);
}

/// <summary>A board-wide effect or relic in play, with its variables (e.g. an interval counter).</summary>
public sealed class GlobalInstance
{
    internal GlobalInstance(GlobalDef def, int[]? vars)
    {
        Def = def;
        Vars = VarStore.Create(def.VarCount, vars);
    }

    public GlobalDef Def { get; }
    internal int[] Vars { get; }
    public int VarCount => Vars.Length;
    public int Var(int slot) => Vars[slot];

    internal GlobalInstance Copy() => new(Def, Vars);

    public override string ToString() => VarStore.Describe(Def.Id, Vars);
}

internal static class VarStore
{
    public static int[] Create(int count, int[]? initial)
    {
        if (count == 0) return Array.Empty<int>();
        var vars = new int[count];
        if (initial != null) Array.Copy(initial, vars, Math.Min(count, initial.Length));
        return vars;
    }

    public static string Describe(string id, int[] vars)
        => vars.Length == 0 ? id : $"{id}({string.Join(",", vars)})";
}
