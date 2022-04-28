using MassTransit;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using System.Reflection;

Container container = new Container();
container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
container.Options.DefaultLifestyle = Lifestyle.Scoped;
container.Options.EnableAutoVerification = true;

var execDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
Directory.SetCurrentDirectory(execDirectory);

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    ApplicationName = typeof(Program).Assembly.FullName,
    ContentRootPath = Directory.GetCurrentDirectory()
});

var services = builder.Services;

services.AddControllers();

services.AddMassTransit(x =>
    {
        x.AddConsumer<TestConsumer>();

        x.UsingAzureServiceBus((context, cfg) =>
        {
            cfg.Host("Endpoint=sb://driver-plan-shamsher.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=fxRJUgyDK3tOelNx0QTgRW+LQfM1y/tbruMTB9VpTNY=");

            cfg.ReceiveEndpoint("EngineQueue",
                e =>
                {
                    e.MaxAutoRenewDuration = TimeSpan.FromHours(1);
                    e.PrefetchCount = 1;
                    e.RequiresSession = true;
                    e.ConcurrentMessageLimit = 1;
                    e.MaxConcurrentCalls = 1;
                    e.ConfigureConsumeTopology = false;

                    e.ConfigureConsumers(context);
                });
        });

        x.AddOptions<MassTransitHostOptions>().Configure(options =>
        {
            options.WaitUntilStarted = true;
        });
    }
);

services.AddSimpleInjector(container, options =>
{
    options.AddAspNetCore().AddControllerActivation();
    options.AutoCrossWireFrameworkComponents = true;
});

container.Register<IMessageWritter, MessageWritter>();

var app = builder.Build();

app.Services.UseSimpleInjector(container);

container.Verify();

app.RunAsync();


public class TestMessage
{
    public string Content { get; set; }
}

public class TestConsumer : IConsumer<TestMessage>
{
    private readonly IMessageWritter messageWritter;

    public TestConsumer(IMessageWritter messageWritter)
    {
        this.messageWritter = messageWritter;
    }

    public async Task Consume(ConsumeContext<TestMessage> context)
    {
        await this.messageWritter.Write(context.Message.Content);
    }
}

public class MessageWritter : IMessageWritter
{
    public async Task Write(string message)
    {
        Console.WriteLine($"Recieved Message: {message}");
        await Task.Delay(100);
    }
}

public interface IMessageWritter
{
    Task Write(string message);
}