using System.Text.Json;

namespace l99.driver.fanuc.utils;

/// <summary>
/// Configuration manager for gateway deployment with environment variable support
/// </summary>
public class ConfigurationManager
{
    private readonly ILogger _logger;
    private readonly dynamic _baseConfiguration;

    public ConfigurationManager(dynamic baseConfiguration)
    {
        _logger = LogManager.GetCurrentClassLogger();
        _baseConfiguration = baseConfiguration;
    }

    /// <summary>
    /// Builds the final configuration with environment variable overrides
    /// </summary>
    public dynamic BuildConfiguration()
    {
        try
        {
            _logger.Info("Building configuration with environment variable overrides...");
            
            // Clone the base configuration
            var config = CloneConfiguration(_baseConfiguration);
            
            // Apply environment variable overrides
            ApplyEnvironmentOverrides(config);
            
            // Validate the final configuration
            ValidateConfiguration(config);
            
            _logger.Info("Configuration built successfully");
            return config;
        }
        catch (Exception ex)
        {
            _logger.Error($"Configuration build failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Applies environment variable overrides to the configuration
    /// </summary>
    private void ApplyEnvironmentOverrides(dynamic config)
    {
        // Machine configuration overrides
        ApplyMachineOverrides(config);
        
        // Network configuration overrides
        ApplyNetworkOverrides(config);
        
        // MQTT configuration overrides
        ApplyMqttOverrides(config);
        
        // Discovery configuration overrides
        ApplyDiscoveryOverrides(config);
    }

    /// <summary>
    /// Applies machine-specific environment variable overrides
    /// </summary>
    private void ApplyMachineOverrides(dynamic config)
    {
        // Machine ID override
        var machineId = Environment.GetEnvironmentVariable("MACHINE_ID");
        if (!string.IsNullOrEmpty(machineId))
        {
            if (config.ContainsKey("machine"))
            {
                config.machine.id = machineId;
                _logger.Info($"Machine ID overridden from environment: {machineId}");
            }
        }

        // Sweep interval override
        var sweepMs = Environment.GetEnvironmentVariable("SWEEP_MS");
        if (!string.IsNullOrEmpty(sweepMs) && int.TryParse(sweepMs, out var sweepInterval))
        {
            if (config.ContainsKey("machine") && config.machine.ContainsKey("type"))
            {
                config.machine.type.sweep_ms = sweepInterval;
                _logger.Info($"Sweep interval overridden from environment: {sweepInterval}ms");
            }
        }
    }

    /// <summary>
    /// Applies network configuration overrides
    /// </summary>
    private void ApplyNetworkOverrides(dynamic config)
    {
        // CNC IP override
        var cncIp = Environment.GetEnvironmentVariable("CNC_IP");
        if (!string.IsNullOrEmpty(cncIp))
        {
            if (config.ContainsKey("machine") && config.machine.ContainsKey("type"))
            {
                config.machine.type.net.ip = cncIp;
                _logger.Info($"CNC IP overridden from environment: {cncIp}");
            }
        }

        // CNC Port override
        var cncPort = Environment.GetEnvironmentVariable("CNC_PORT");
        if (!string.IsNullOrEmpty(cncPort) && int.TryParse(cncPort, out var port))
        {
            if (config.ContainsKey("machine") && config.machine.ContainsKey("type"))
            {
                config.machine.type.net.port = port;
                _logger.Info($"CNC Port overridden from environment: {port}");
            }
        }

        // Timeout override
        var timeout = Environment.GetEnvironmentVariable("CNC_TIMEOUT");
        if (!string.IsNullOrEmpty(timeout) && int.TryParse(timeout, out var timeoutSeconds))
        {
            if (config.ContainsKey("machine") && config.machine.ContainsKey("type"))
            {
                config.machine.type.net.timeout_s = timeoutSeconds;
                _logger.Info($"CNC Timeout overridden from environment: {timeoutSeconds}s");
            }
        }
    }

    /// <summary>
    /// Applies MQTT configuration overrides
    /// </summary>
    private void ApplyMqttOverrides(dynamic config)
    {
        // Broker IP override
        var brokerIp = Environment.GetEnvironmentVariable("BROKER_IP");
        if (!string.IsNullOrEmpty(brokerIp))
        {
            if (config.ContainsKey("machine") && config.machine.ContainsKey("transport"))
            {
                var transportType = config.machine.transport.ToString();
                if (transportType.Contains("MQTT"))
                {
                    config[transportType].net.ip = brokerIp;
                    _logger.Info($"Broker IP overridden from environment: {brokerIp}");
                }
            }
        }

        // Broker Port override
        var brokerPort = Environment.GetEnvironmentVariable("BROKER_PORT");
        if (!string.IsNullOrEmpty(brokerPort) && int.TryParse(brokerPort, out var port))
        {
            if (config.ContainsKey("machine") && config.machine.ContainsKey("transport"))
            {
                var transportType = config.machine.transport.ToString();
                if (transportType.Contains("MQTT"))
                {
                    config[transportType].net.port = port;
                    _logger.Info($"Broker Port overridden from environment: {port}");
                }
            }
        }

        // Username override
        var username = Environment.GetEnvironmentVariable("BROKER_USER");
        if (!string.IsNullOrEmpty(username))
        {
            if (config.ContainsKey("machine") && config.machine.ContainsKey("transport"))
            {
                var transportType = config.machine.transport.ToString();
                if (transportType.Contains("MQTT"))
                {
                    config[transportType].user = username;
                    config[transportType].anonymous = false;
                    _logger.Info($"Broker Username overridden from environment: {username}");
                }
            }
        }

        // Password override
        var password = Environment.GetEnvironmentVariable("BROKER_PASSWORD");
        if (!string.IsNullOrEmpty(password))
        {
            if (config.ContainsKey("machine") && config.machine.ContainsKey("transport"))
            {
                var transportType = config.machine.transport.ToString();
                if (transportType.Contains("MQTT"))
                {
                    config[transportType].password = password;
                    config[transportType].anonymous = false;
                    _logger.Info("Broker Password overridden from environment");
                }
            }
        }

        // TLS override
        var tlsEnabled = Environment.GetEnvironmentVariable("BROKER_TLS");
        if (!string.IsNullOrEmpty(tlsEnabled) && bool.TryParse(tlsEnabled, out var tls))
        {
            if (config.ContainsKey("machine") && config.machine.ContainsKey("transport"))
            {
                var transportType = config.machine.transport.ToString();
                if (transportType.Contains("MQTT"))
                {
                    config[transportType].tls_enabled = tls;
                    _logger.Info($"Broker TLS overridden from environment: {tls}");
                }
            }
        }
    }

    /// <summary>
    /// Applies discovery configuration overrides
    /// </summary>
    private void ApplyDiscoveryOverrides(dynamic config)
    {
        // Discovery timeout override
        var discoveryTimeout = Environment.GetEnvironmentVariable("DISCOVERY_TIMEOUT_MS");
        if (!string.IsNullOrEmpty(discoveryTimeout) && int.TryParse(discoveryTimeout, out var timeout))
        {
            if (config.ContainsKey("discovery"))
            {
                config.discovery.timeout_ms = timeout;
                _logger.Info($"Discovery timeout overridden from environment: {timeout}ms");
            }
        }

        // Enable/disable discovery
        var enableDiscovery = Environment.GetEnvironmentVariable("ENABLE_DISCOVERY");
        if (!string.IsNullOrEmpty(enableDiscovery) && bool.TryParse(enableDiscovery, out var discovery))
        {
            if (config.ContainsKey("discovery"))
            {
                config.discovery.enabled = discovery;
                _logger.Info($"Discovery enabled overridden from environment: {discovery}");
            }
        }
    }

    /// <summary>
    /// Validates the final configuration
    /// </summary>
    private void ValidateConfiguration(dynamic config)
    {
        _logger.Info("Validating configuration...");

        // Validate machine configuration
        ValidateMachineConfiguration(config);

        // Validate transport configuration
        ValidateTransportConfiguration(config);

        // Validate discovery configuration
        ValidateDiscoveryConfiguration(config);

        _logger.Info("Configuration validation completed successfully");
    }

    /// <summary>
    /// Validates machine configuration
    /// </summary>
    private void ValidateMachineConfiguration(dynamic config)
    {
        if (!config.ContainsKey("machine"))
        {
            throw new Exception("Machine configuration is missing");
        }

        if (!config.machine.ContainsKey("id") || string.IsNullOrEmpty(config.machine.id.ToString()))
        {
            throw new Exception("Machine ID is required");
        }

        if (!config.machine.ContainsKey("type"))
        {
            throw new Exception("Machine type configuration is missing");
        }

        if (!config.machine.type.ContainsKey("net"))
        {
            throw new Exception("Machine network configuration is missing");
        }

        if (!config.machine.type.net.ContainsKey("ip") || string.IsNullOrEmpty(config.machine.type.net.ip.ToString()))
        {
            throw new Exception("CNC IP address is required");
        }

        if (!config.machine.type.net.ContainsKey("port"))
        {
            throw new Exception("CNC port is required");
        }

        if (!config.machine.type.net.ContainsKey("timeout_s"))
        {
            throw new Exception("CNC timeout is required");
        }
    }

    /// <summary>
    /// Validates transport configuration
    /// </summary>
    private void ValidateTransportConfiguration(dynamic config)
    {
        if (!config.machine.ContainsKey("transport"))
        {
            throw new Exception("Transport configuration is missing");
        }

        var transportType = config.machine.transport.ToString();
        if (transportType.Contains("MQTT"))
        {
            if (!config.ContainsKey(transportType))
            {
                throw new Exception("MQTT transport configuration is missing");
            }

            var mqttConfig = config[transportType];
            if (!mqttConfig.ContainsKey("net"))
            {
                throw new Exception("MQTT network configuration is missing");
            }

            if (!mqttConfig.net.ContainsKey("ip") || string.IsNullOrEmpty(mqttConfig.net.ip.ToString()))
            {
                throw new Exception("MQTT broker IP is required");
            }

            if (!mqttConfig.net.ContainsKey("port"))
            {
                throw new Exception("MQTT broker port is required");
            }
        }
    }

    /// <summary>
    /// Validates discovery configuration
    /// </summary>
    private void ValidateDiscoveryConfiguration(dynamic config)
    {
        if (config.ContainsKey("discovery"))
        {
            if (config.discovery.ContainsKey("enabled") && config.discovery.enabled == true)
            {
                if (!config.discovery.ContainsKey("timeout_ms"))
                {
                    throw new Exception("Discovery timeout is required when discovery is enabled");
                }
            }
        }
    }

    /// <summary>
    /// Clones a configuration object (deep copy)
    /// </summary>
    private dynamic CloneConfiguration(dynamic config)
    {
        try
        {
            // Convert to JSON and back to create a deep copy
            var json = JsonSerializer.Serialize(config);
            return JsonSerializer.Deserialize<dynamic>(json);
        }
        catch (Exception ex)
        {
            _logger.Warning($"Could not clone configuration, using reference: {ex.Message}");
            return config;
        }
    }

    /// <summary>
    /// Gets configuration summary for logging
    /// </summary>
    public string GetConfigurationSummary(dynamic config)
    {
        try
        {
            return JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"Configuration summary unavailable: {ex.Message}";
        }
    }
}
