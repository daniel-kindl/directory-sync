using DirectorySync.Cli.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("dirsync");
    config.SetApplicationVersion("1.0.0");

    config.AddCommand<SyncCommand>("sync")
        .WithDescription("Perform one-time directory synchronization")
        .WithExample("sync", "-s", "C:\\Source", "-r", "C:\\Replica");

    config.AddCommand<DaemonCommand>("daemon")
        .WithDescription("Run continuous synchronization with interval")
        .WithExample("daemon", "-s", "C:\\Source", "-r", "C:\\Replica", "-i", "60");

    config.AddCommand<PlanCommand>("plan")
        .WithDescription("Show what would be synchronized without making changes")
        .WithExample("plan", "-s", "C:\\Source", "-r", "C:\\Replica");

    config.AddCommand<VerifyCommand>("verify")
        .WithDescription("Verify that replica matches source")
        .WithExample("verify", "-s", "C:\\Source", "-r", "C:\\Replica");
});

return app.Run(args);
