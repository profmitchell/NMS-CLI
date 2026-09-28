using NMS.CLI;

namespace NMSE.Tests;

public class CliCommandLineTests
{
    [Fact]
    public void Parse_SaveList_Command()
    {
        var options = CommandLine.Parse(["save", "list"]);

        Assert.Equal(CliCommandType.SaveList, options.Command);
        Assert.Null(options.Error);
        Assert.False(options.ShowHelp);
    }

    [Fact]
    public void Parse_SaveInfo_WithSlotAndDirectory()
    {
        var options = CommandLine.Parse(["save", "info", "--slot", "2", "--dir", "/tmp/nms"]);

        Assert.Equal(CliCommandType.SaveInfo, options.Command);
        Assert.Equal(2, options.Slot);
        Assert.Equal("/tmp/nms", options.SaveDirectory);
        Assert.Null(options.Error);
    }

    [Fact]
    public void Parse_SaveInfo_RequiresSlot()
    {
        var options = CommandLine.Parse(["save", "info"]);

        Assert.Equal(CliCommandType.None, options.Command);
        Assert.Equal("Missing required option --slot <number>.", options.Error);
    }
}
