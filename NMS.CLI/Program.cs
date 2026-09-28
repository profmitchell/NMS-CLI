namespace NMS.CLI;

public static class Program
{
    public static int Main(string[] args)
    {
        var options = CommandLine.Parse(args);

        if (options.ShowHelp)
        {
            Console.WriteLine(CommandLine.Usage());
            return 0;
        }

        if (!string.IsNullOrEmpty(options.Error))
        {
            Console.Error.WriteLine(options.Error);
            Console.WriteLine();
            Console.WriteLine(CommandLine.Usage());
            return 1;
        }

        return options.Command switch
        {
            CliCommandType.SaveList => SaveCommands.List(options.SaveDirectory),
            CliCommandType.SaveInfo => SaveCommands.Info(options.SaveDirectory, options.Slot),
            _ => 1,
        };
    }
}
