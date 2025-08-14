using l99.driver.fanuc.utils;

namespace l99.driver.fanuc.services;

/// <summary>
/// Service that integrates CNC discovery with gateway mode operation
/// </summary>
public class GatewayDiscoveryService
{
    private readonly ILogger _logger;
    private readonly CncDiscovery _discovery;
    private readonly dynamic _configuration;

    public GatewayDiscoveryService(dynamic configuration)
    {
        _logger = LogManager.GetCurrentClassLogger();
        _configuration = configuration;
        _discovery = new CncDiscovery();
    }

    /// <summary>
    /// Discovers CNC machines and updates configuration if needed
    /// </summary>
    public async Task<string?> DiscoverAndConfigureCncAsync()
    {
        try
        {
            _logger.Info("Starting CNC discovery for gateway mode...");
            
            // Check if discovery is enabled
            if (!IsDiscoveryEnabled())
            {
                _logger.Info("Discovery is disabled, using configured CNC IP");
                return GetConfiguredCncIp();
            }

            // Check if CNC IP is already configured
            var configuredIp = GetConfiguredCncIp();
            if (!string.IsNullOrEmpty(configuredIp) && configuredIp != "auto-discover")
            {
                _logger.Info($"Using configured CNC IP: {configuredIp}");
                return configuredIp;
            }

            // Perform discovery
            var discoveredIps = await _discovery.DiscoverCncMachinesAsync();
            
            if (discoveredIps.Any())
            {
                var cncIp = discoveredIps.First();
                _logger.Info($"CNC discovered at: {cncIp}");
                
                // Update configuration with discovered IP
                UpdateConfigurationWithDiscoveredIp(cncIp);
                
                return cncIp;
            }
            else
            {
                _logger.Warning("No CNC machines discovered");
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"CNC discovery failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Checks if discovery is enabled in configuration
    /// </summary>
    private bool IsDiscoveryEnabled()
    {
        try
        {
            if (_configuration.ContainsKey("discovery"))
            {
                return _configuration.discovery.enabled == true;
            }
            return true; // Default to enabled
        }
        catch (Exception ex)
        {
            _logger.Warning($"Could not determine discovery status: {ex.Message}");
            return true; // Default to enabled
        }
    }

    /// <summary>
    /// Gets the configured CNC IP from configuration
    /// </summary>
    private string? GetConfiguredCncIp()
    {
        try
        {
            if (_configuration.ContainsKey("machine") && 
                _configuration.machine.ContainsKey("type") &&
                _configuration.machine.type.ContainsKey("net"))
            {
                return _configuration.machine.type.net.ip?.ToString();
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
    /// Updates configuration with discovered CNC IP
    /// </summary>
    private void UpdateConfigurationWithDiscoveredIp(string cncIp)
    {
        try
        {
            if (_configuration.ContainsKey("machine") && 
                _configuration.machine.ContainsKey("type") &&
                _configuration.machine.type.ContainsKey("net"))
            {
                _configuration.machine.type.net.ip = cncIp;
                _logger.Info($"Configuration updated with discovered CNC IP: {cncIp}");
            }
        }
        catch (Exception ex)
        {
            _logger.Warning($"Could not update configuration with discovered IP: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets discovery statistics
    /// </summary>
    public DiscoveryStats GetDiscoveryStats()
    {
        return _discovery.GetDiscoveryStats();
    }

    /// <summary>
    /// Validates that the gateway can connect to the CNC
    /// </summary>
    public async Task<bool> ValidateCncConnectionAsync(string cncIp)
    {
        try
        {
            _logger.Info($"Validating CNC connection to {cncIp}...");
            
            // Test basic connectivity
            var discoveredIps = await _discovery.DiscoverCncMachinesAsync();
            var isConnected = discoveredIps.Contains(cncIp);
            
            if (isConnected)
            {
                _logger.Info($"CNC connection validated successfully: {cncIp}");
            }
            else
            {
                _logger.Warning($"CNC connection validation failed: {cncIp}");
            }
            
            return isConnected;
        }
        catch (Exception ex)
        {
            _logger.Error($"CNC connection validation failed: {ex.Message}");
            return false;
        }
    }
}
