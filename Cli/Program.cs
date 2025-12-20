using DirectorySync.Cli.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("dirsync");
    config.SetApplicationVersion("1.0.0");

    config.AddCommand<SyncCommand>("sync")
        .WithDescription("Perform one-time directory synchronization")
        .WithExample("sync", "-s", "/path/to/source", "-r", "/path/to/replica");

    config.AddCommand<DaemonCommand>("daemon")
        .WithDescription("Run continuous synchronization with interval")
        .WithExample("daemon", "-s", "/path/to/source", "-r", "/path/to/replica", "-i", "60");

    config.AddCommand<PlanCommand>("plan")
        .WithDescription("Show what would be synchronized without making changes")
        .WithExample("plan", "-s", "/path/to/source", "-r", "/path/to/replica");

    config.AddCommand<VerifyCommand>("verify")
        .WithDescription("Verify that replica matches source")
        .WithExample("verify", "-s", "/path/to/source", "-r", "/path/to/replica");
});

return app.Run(args);
