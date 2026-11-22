using System.Diagnostics;
using System.Text;

namespace Eunomia.Governments.Ai;

public sealed record ModelReply(string Text, string? Error, long ElapsedMs)
{
    public bool Ok => Error is null;
}

/// <summary>A language model behind one branch. Only text crosses this boundary.</summary>
public interface IBranchModel
{
    string Name { get; }
    ModelReply Complete(string system, string prompt);
}

/// <summary>Canned replies, for testing the pipeline with no model calls.</summary>
public sealed class ScriptedModel(Func<string, string> reply, string name = "scripted") : IBranchModel
{
    public string Name => name;
    public ModelReply Complete(string system, string prompt) => new(reply(prompt), null, 0);
}

public sealed record CliModelConfig
{
    /// <summary>A label for logs, e.g. "claude-sonnet".</summary>
    public required string Name { get; init; }
    public required string Command { get; init; }
    public required IReadOnlyList<string> Args { get; init; }
    /// <summary>If set, the system prompt is written to a file in the call's folder and passed with this flag.</summary>
    public string? SystemPromptFileArg { get; init; }
    public required int TimeoutSeconds { get; init; }
}

/// <summary>
/// Starts a vendor's CLI as a child process on the player's own subscription login. Each call runs in a new,
/// empty folder that is deleted afterwards. The prompt goes in on stdin and the reply comes back on stdout.
/// </summary>
public sealed class CliModel(CliModelConfig config) : IBranchModel
{
    private static readonly Encoding Utf8 = new UTF8Encoding(false);

    public string Name => config.Name;

    public ModelReply Complete(string system, string prompt)
    {
        var watch = Stopwatch.StartNew();
        var dir = Directory.CreateTempSubdirectory("eunomia-call-");
        try
        {
            var psi = new ProcessStartInfo(Resolve(config.Command))
            {
                WorkingDirectory = dir.FullName,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardInputEncoding = Utf8,
                StandardOutputEncoding = Utf8,
                StandardErrorEncoding = Utf8,
            };
            foreach (string a in config.Args) psi.ArgumentList.Add(a);
            if (config.SystemPromptFileArg is not null)
            {
                string file = Path.Combine(dir.FullName, "system.txt");
                File.WriteAllText(file, system, Utf8);
                psi.ArgumentList.Add(config.SystemPromptFileArg);
                psi.ArgumentList.Add(file);
            }
            else
            {
                prompt = system + "\n\n" + prompt;
            }

            using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Could not start {config.Command}.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            process.StandardInput.Write(prompt);
            process.StandardInput.Close();

            if (!process.WaitForExit(TimeSpan.FromSeconds(config.TimeoutSeconds)))
            {
                try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                return new ModelReply("", $"timed out after {config.TimeoutSeconds}s", watch.ElapsedMilliseconds);
            }
            process.WaitForExit();
            string text = stdout.Result;
            if (process.ExitCode != 0)
                return new ModelReply(text, $"exit code {process.ExitCode}: {Truncate(stderr.Result.Trim(), 500)}", watch.ElapsedMilliseconds);
            return new ModelReply(text, null, watch.ElapsedMilliseconds);
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return new ModelReply("", e.Message, watch.ElapsedMilliseconds);
        }
        finally
        {
            try { dir.Delete(recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>On Windows, npm installs CLIs as .cmd shims, which Process.Start only finds with the extension.</summary>
    private static string Resolve(string command)
    {
        if (!OperatingSystem.IsWindows() || Path.HasExtension(command) || Path.IsPathRooted(command)) return command;
        var extensions = new[] { ".exe", ".cmd", ".bat" };
        foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            foreach (string ext in extensions)
            {
                string candidate = Path.Combine(dir.Trim(), command + ext);
                if (File.Exists(candidate)) return candidate;
            }
        return command;
    }

    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n] + "...";
}
