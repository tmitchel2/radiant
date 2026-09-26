using Radiant.Host.AgentControlProtocol;
using Radiant.UI.Core;

namespace Radiant.UI.Automation.Tests;

/// <summary>An app run headless on a thread of its own, served over the agent protocol as a launched app would be.</summary>
internal sealed class LiveApp : IAsyncDisposable
{
    private readonly Thread _thread;
    private Exception? _failure;

    private LiveApp(Element root, UIClockMode clock)
    {
        Name = "t-" + Guid.NewGuid().ToString("N")[..8];
        _thread = new Thread(() =>
        {
            try
            {
                RadiantUI.Run(root, new UIAppOptions
                {
                    Title = "test app",
                    Width = 800,
                    Height = 700,
                    Headless = true,
                    Clock = clock,
                    Extensions = [new AgentServer(new AgentServerOptions { Name = Name })],
                });
            }
            catch (Exception e)
            {
                _failure = e;
            }
        })
        { IsBackground = true, Name = Name };
    }

    public string Name { get; }

    public InstanceInfo Info => InstanceRegistry.GetInstance(Name) ?? throw new InvalidOperationException($"{Name} isn't running.", _failure);

    public static async Task<LiveApp> StartAsync(Element root, UIClockMode clock = UIClockMode.Fixed)
    {
        var app = new LiveApp(root, clock);
        app._thread.Start();
        for (var i = 0; i < 500; i++)
        {
            if (InstanceRegistry.GetInstance(app.Name) is { Ready: true })
            {
                return app;
            }
            if (app._failure is { } failure)
            {
                throw new InvalidOperationException("The app failed to start.", failure);
            }
            await Task.Delay(10);
        }
        throw new TimeoutException($"{app.Name} wasn't ready.");
    }

    public async ValueTask DisposeAsync()
    {
        if (InstanceRegistry.GetInstance(Name) is not null)
        {
            await using var client = await AgentClient.ConnectAsync(Name, AgentTransport.Socket);
            await client.SendAsync("app.exit", timeoutMs: 2000);
        }
        if (!_thread.Join(TimeSpan.FromSeconds(10)))
        {
            throw new TimeoutException($"{Name} didn't exit.");
        }
        if (_failure is { } failure)
        {
            throw new InvalidOperationException("The app failed.", failure);
        }
    }
}
