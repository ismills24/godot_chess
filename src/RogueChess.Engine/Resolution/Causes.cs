namespace RogueChess.Engine.Resolution;

/// <summary>
/// Well-known <see cref="Proposal.Cause"/> strings. Content can use any string; these are the ones the
/// engine produces and that content commonly checks (e.g. Guardian only stops <see cref="Capture"/>).
/// </summary>
public static class Causes
{
    /// <summary>The moving piece's own move (wave 0 of an action).</summary>
    public const string Action = "action";

    /// <summary>The target of a normal capture.</summary>
    public const string Capture = "capture";

    /// <summary>The pawn taken en passant.</summary>
    public const string EnPassant = "en-passant";

    /// <summary>The rook's half of castling.</summary>
    public const string Castle = "castle";

    public const string Promotion = "promotion";
    public const string Explosion = "explosion";
    public const string Scapegoat = "scapegoat";

    /// <summary>Destroys caused by a move capturing a piece (normal or en passant).</summary>
    public static bool IsMoveCapture(string cause) => cause is Capture or EnPassant;
}
