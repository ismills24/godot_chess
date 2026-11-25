using System;
using System.IO;
using System.Text;

namespace RogueChess.Runs;

public enum SavePolicy { OnOverworldReturn, AfterCommand }
public interface IRunStore
{
    void Write(RunSave save);
    RunSave Read();
}

/// <summary>A complete generation is replaced atomically; a previous valid generation is retained.</summary>
public sealed class FileRunStore : IRunStore
{
    public string Path { get; }
    public FileRunStore(string path) => Path = System.IO.Path.GetFullPath(path);
    public void Write(RunSave save)
    {
        string json = RunJson.Write(save);
        _ = RunJson.Read<RunSave>(json);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        string temporary = Path + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            stream.Write(bytes);
            stream.Flush(true);
        }
        if (File.Exists(Path)) File.Replace(temporary, Path, Path + ".previous");
        else File.Move(temporary, Path);
    }
    public RunSave Read() => RunJson.Read<RunSave>(File.ReadAllText(Path));
}

/// <summary>Failure leaves the committed session dirty. Retrying writes state, never executes commands.</summary>
public sealed class RunPersistence
{
    private readonly IRunStore _store;
    public SavePolicy Policy { get; set; } = SavePolicy.OnOverworldReturn;
    public bool Dirty { get; private set; }
    public string? LastError { get; private set; }
    public long LastSavedSequence { get; private set; } = -1;
    public RunPersistence(IRunStore store) => _store = store;
    public void MarkLoaded(long sequence)
    {
        LastSavedSequence = sequence;
        Dirty = false;
        LastError = null;
    }
    public void OnCommand(RunSession session, RunPhase before)
    {
        Dirty = session.Sequence != LastSavedSequence;
        if (!Dirty) return;
        var after = session.State.Phase;
        if (Policy == SavePolicy.AfterCommand || (before == RunPhase.Encounter && after != RunPhase.Encounter)
            || after == RunPhase.Ended) Flush(session);
    }
    public bool Flush(RunSession session)
    {
        Dirty = true;
        try
        {
            var save = session.CaptureSave();
            _store.Write(save);
            LastSavedSequence = save.Checkpoint.Sequence;
            Dirty = false;
            LastError = null;
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            LastError = e.Message;
            return false;
        }
    }
}
