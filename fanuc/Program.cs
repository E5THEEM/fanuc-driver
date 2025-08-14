#pragma warning disable CS1998

using System.IO;
using l99.driver.@base;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;

// ReSharper disable once CheckNamespace
namespace l99.driver.fanuc;

// ReSharper disable once ClassNeverInstantiated.Global
internal class Program
{
    private static async Task Main(string[] args)
    {
        Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

        var hostBuilder = Host.CreateDefaultBuilder(args);

        if (WindowsServiceHelpers.IsWindowsService())
            hostBuilder.UseWindowsService();

        // ReSharper disable once UnusedParameter.Local
        await hostBuilder.ConfigureServices((hostContext, services) =>
            {
                services.AddSingleton(args);
                
                // Determine mode and register appropriate service
                var mode = GetMode(args);
                if (mode.Equals("gateway", StringComparison.OrdinalIgnoreCase))
                {
                    services.AddHostedService<FanucGatewayService>();
                }
                else
                {
                    // Default to server mode (backward compatibility)
                    services.AddHostedService<FanucService>();
                }
            })
            .Build()
            .RunAsync();
    }

    /// <summary>
    /// Determines the operation mode from command line arguments
    /// </summary>
    private static string GetMode(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals("--mode", StringComparison.OrdinalIgnoreCase) ||
                args[i].Equals("-m", StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }
        return "server"; // Default mode
    }
}

/// <summary>
/// Fanuc Service for server mode (multi-machine, existing behavior)
/// </summary>
public class FanucService : BackgroundService
{
    private readonly string[] _args;

    public FanucService(string[] args)
    {
        _args = args;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = await Bootstrap.Start(_args);
        Machines machines = await Machines.CreateMachines(config);
        await machines.RunAsync(stoppingToken);
        await Bootstrap.Stop();
    }
}

/// <summary>
/// Fanuc Gateway Service for gateway mode (single-machine, new behavior)
/// </summary>
public class FanucGatewayService : BackgroundService
{
    private readonly string[] _args;
    private readonly ILogger _logger;

    public FanucGatewayService(string[] args)
    {
        _args = args;
        _logger = LogManager.GetCurrentClassLogger();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.Info("Starting Fanuc Gateway Service (single-machine mode)");
            
            // Load configuration with environment variable support
            var config = await Bootstrap.Start(_args);
            
            // Run in gateway mode
            await RunGatewayModeAsync(config, stoppingToken);
            
            await Bootstrap.Stop();
        }
        catch (Exception ex)
        {
            _logger.Error($"Gateway service failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Runs the application in gateway mode (single machine)
    /// </summary>
    private async Task RunGatewayModeAsync(dynamic config, CancellationToken stoppingToken)
    {
        try
        {
            _logger.Info("Initializing gateway mode...");
            
            // Perform CNC discovery if enabled
            var cncIp = await PerformCncDiscoveryAsync(config);
            if (string.IsNullOrEmpty(cncIp))
            {
                _logger.Error("CNC discovery failed or no CNC found");
                return;
            }
            
            // Create and run single machine
            var machine = await CreateSingleMachineAsync(config);
            
            if (machine != null)
            {
                _logger.Info($"Gateway initialized for machine: {machine.Id} at {cncIp}");
                await machine.RunAsync(stoppingToken);
            }
            else
            {
                _logger.Error("Failed to create machine instance");
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Gateway mode execution failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Performs CNC discovery for gateway mode
    /// </summary>
    private async Task<string?> PerformCncDiscoveryAsync(dynamic config)
    {
        try
        {
            // Try to use GatewayDiscoveryService if available
            var discoveryServiceType = Type.GetType("l99.driver.fanuc.services.GatewayDiscoveryService, fanuc");
            if (discoveryServiceType != null)
            {
                var discoveryService = Activator.CreateInstance(discoveryServiceType, config);
                var discoverMethod = discoveryServiceType.GetMethod("DiscoverAndConfigureCncAsync");
                if (discoverMethod != null)
                {
                    var result = discoverMethod.Invoke(discoveryService, null);
                    if (result is Task<string?> task)
                    {
                        return await task;
                    }
                }
            }
            
            // Fallback: check if CNC IP is configured
            var configuredIp = GetConfiguredCncIp(config);
            if (!string.IsNullOrEmpty(configuredIp) && configuredIp != "auto-discover")
            {
                _logger.Info($"Using configured CNC IP: {configuredIp}");
                return configuredIp;
            }
            
            _logger.Warning("No CNC discovery service available and no IP configured");
            return null;
        }
        catch (Exception ex)
        {
            _logger.Error($"CNC discovery failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets the configured CNC IP from configuration
    /// </summary>
    private string? GetConfiguredCncIp(dynamic config)
    {
        try
        {
            if (config.ContainsKey("machine") && 
                config.machine.ContainsKey("type") &&
                config.machine.type.ContainsKey("net"))
            {
                return config.machine.type.net.ip?.ToString();
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.Warning($"Could not get configured CNC IP: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Creates a single machine instance for gateway mode
    /// </summary>
    private async Task<Machine?> CreateSingleMachineAsync(dynamic config)
    {
        try
        {
            // Use the existing machine creation logic from the base framework
            var machines = await Machines.CreateMachines(config);
            
            // Get the first (and only) machine
            var machineList = machines.GetMachines();
            if (machineList.Any())
            {
                return machineList.First();
            }
            
            _logger.Error("No machines found in configuration");
            return null;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to create machine: {ex.Message}");
            return null;
        }
    }
}
#pragma warning restore CS1998