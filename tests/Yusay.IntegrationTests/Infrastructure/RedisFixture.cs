using System.Net;
using System.Net.Sockets;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using StackExchange.Redis;
using Xunit;

namespace Yusay.IntegrationTests.Infrastructure;

/// <summary>
/// Contenedor Redis con la misma política de persistencia que docker-compose (AOF
/// <c>appendonly yes</c> + <c>appendfsync everysec</c>) para ejercitar la denylist de revocación
/// selectiva MP-PHYS-015: su uso, la supervivencia de las revocaciones tras un reinicio del
/// servicio y el comportamiento fail-closed cuando no es alcanzable.
/// </summary>
public sealed class RedisFixture : IAsyncLifetime
{
    // Puerto de host fijo y explícito (no aleatorio): con WithPortBinding(6379, true) Docker
    // reasigna un puerto distinto en cada start del mismo contenedor, y el multiplexer
    // existente quedaría reconectando para siempre contra el puerto viejo (fail-closed
    // permanente) en lugar de probar el nuevo. Con puerto fijo —igual que docker-compose— la
    // reconexión automática del cliente se ejercita igual que en producción.
    private readonly IContainer _container = new ContainerBuilder("redis:8-alpine")
        .WithPortBinding(GetFreeHostPort(), 6379)
        .WithCommand("redis-server", "--appendonly", "yes", "--appendfsync", "everysec")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Ready to accept connections"))
        .Build();

    private static int GetFreeHostPort()
    {
        var listener = new TcpListener(IPAddress.Any, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    /// <summary>Reinicia Redis conservando su volumen de datos (AOF), como un reinicio real.</summary>
    public async Task RestartAsync()
    {
        await _container.StopAsync();
        await _container.StartAsync();
    }

    /// <summary>Detiene Redis para ejercitar la interrupción: el cliente debe fallar en fail-closed.</summary>
    public async Task StopAsync()
    {
        await _container.StopAsync();
    }

    /// <summary>Vuelve a arrancar Redis conservando el volumen (AOF) para ejercitar la recuperación.</summary>
    public async Task StartAsync()
    {
        await _container.StartAsync();
    }

    /// <summary>Host y puerto público del Redis del fixture, para configurar la API en pruebas HTTP.</summary>
    public string Host => _container.Hostname;

    /// <summary>Puerto de host asignado al contenedor (fijo durante toda la vida del fixture).</summary>
    public int MappedPort => _container.GetMappedPublicPort(6379);

    /// <summary>Conexión al Redis del fixture con timeouts cortos para los tests de fallo.</summary>
    public IConnectionMultiplexer Connect(int timeoutMilliseconds = 5000)
    {
        var options = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            ConnectTimeout = timeoutMilliseconds,
            SyncTimeout = timeoutMilliseconds
        };
        options.EndPoints.Add(_container.Hostname, _container.GetMappedPublicPort(6379));

        return ConnectionMultiplexer.Connect(options);
    }

    /// <summary>Conexión a un endpoint que no acepta conexiones, para ejercitar el fail-closed.</summary>
    public static IConnectionMultiplexer ConnectToUnreachableEndpoint()
    {
        var options = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            ConnectTimeout = 300,
            SyncTimeout = 300
        };
        options.EndPoints.Add("127.0.0.1", 1);

        return ConnectionMultiplexer.Connect(options);
    }
}
