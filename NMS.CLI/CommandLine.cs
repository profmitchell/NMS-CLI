namespace NMS.CLI;

public enum CliCommandType
{
    None,
    SaveList,
    SaveInfo,
}

public sealed class CliOptions
{
    public CliCommandType Command { get; init; }
    public string? SaveDirectory { get; init; }
    public int Slot { get; init; }
    public bool ShowHelp { get; init; }
    public string? Error { get; init; }
}

public static class CommandLine
{
    public static CliOptions Parse(string[] args)
    {
        if (args.Length == 0 || HasHelp(args))
            return new CliOptions { ShowHelp = true, Command = CliCommandType.None };

        if (!string.Equals(args[0], "save", StringComparison.OrdinalIgnoreCase))
            return Error("Unknown command.");

        if (args.Length < 2)
            return Error("Missing save subcommand.");

        string sub = args[1];
        var rest = args.Skip(2).ToArray();

        return sub.ToLowerInvariant() switch
        {
            "list" => ParseSaveList(rest),
            "info" => ParseSaveInfo(rest),
            _ => Error("Unknown save subcommand.")
        };
    }

    public static string Usage() =>
        """
        NMS CLI (Phase 1)

        Usage:
          nms save list [--dir <path>]
          nms save info --slot <number> [--dir <path>]
        """;

    private static CliOptions ParseSaveList(string[] args)
    {
        if (!TryReadDirectory(args, out string? dir, out string? error))
            return Error(error!);

        return new CliOptions
        {
            Command = CliCommandType.SaveList,
            SaveDirectory = dir,
        };
    }

    private static CliOptions ParseSaveInfo(string[] args)
    {
        if (!TryReadDirectory(args, out string? dir, out string? error, out int? slot))
            return Error(error!);

        if (!slot.HasValue)
            return Error("Missing required option --slot <number>.");

        if (slot.Value <= 0)
            return Error("--slot must be 1 or greater.");

        return new CliOptions
        {
            Command = CliCommandType.SaveInfo,
            SaveDirectory = dir,
            Slot = slot.Value,
        };
    }

    private static bool TryReadDirectory(string[] args, out string? dir, out string? error)
    {
        return TryReadDirectory(args, out dir, out error, out _);
    }

    private static bool TryReadDirectory(string[] args, out string? dir, out string? error, out int? slot)
    {
        dir = null;
        slot = null;
        error = null;

        for (int i = 0; i < args.Length; i++)
        {
            string token = args[i];
            if (string.Equals(token, "--dir", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                {
                    error = "Missing value for --dir.";
                    return false;
                }

                dir = args[++i];
                continue;
            }

            if (string.Equals(token, "--slot", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                {
                    error = "Missing value for --slot.";
                    return false;
                }

                if (!int.TryParse(args[++i], out int parsedSlot))
                {
                    error = "--slot must be an integer.";
                    return false;
                }

                slot = parsedSlot;
                continue;
            }

            error = $"Unknown option '{token}'.";
            return false;
        }

        return true;
    }

    private static CliOptions Error(string error)
        => new() { Command = CliCommandType.None, Error = error };

    private static bool HasHelp(string[] args)
        => args.Any(a => a is "-h" or "--help" or "help");
}
