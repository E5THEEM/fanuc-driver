using System.Net;
using System.Net.Sockets;
using l99.driver.fanuc.utils;

namespace l99.driver.fanuc.utils;

/// <summary>
/// Smart CNC discovery service that automatically finds Fanuc CNC machines on the network
/// </summary>
public class CncDiscovery
{
    private readonly ILogger _logger;
    private readonly int _discoveryTimeoutMs;
    private readonly int _focasPort;

    public CncDiscovery(int discoveryTimeoutMs = 10000, int focasPort = 8193)
    {
        _logger = LogManager.GetCurrentClassLogger();
        _discoveryTimeoutMs = discoveryTimeoutMs;
        _focasPort = focasPort;
    }

    /// <summary>
    /// Discovers CNC machines on the network using multiple strategies
    /// </summary>
    /// <returns>List of discovered CNC IP addresses</returns>
    public async Task<List<string>> DiscoverCncMachinesAsync()
    {
        _logger.Info("Starting CNC discovery process...");
        var discoveredIps = new List<string>();

        try
        {
            // Strategy 1: Try common CNC IPs first (fastest)
            var commonIps = await TryCommonCncIpsAsync();
            if (commonIps.Any())
            {
                discoveredIps.AddRange(commonIps);
                _logger.Info($"Found {commonIps.Count} CNC(s) using common IP strategy");
            }

            // Strategy 2: Smart subnet scan (targeted)
            if (!discoveredIps.Any())
            {
                var subnetIps = await SmartSubnetScanAsync();
                if (subnetIps.Any())
                {
                    discoveredIps.AddRange(subnetIps);
                    _logger.Info($"Found {subnetIps.Count} CNC(s) using subnet scan strategy");
                }
            }

            // Strategy 3: Full subnet scan (fallback)
            if (!discoveredIps.Any())
            {
                var fullScanIps = await FullSubnetScanAsync();
                if (fullScanIps.Any())
                {
                    discoveredIps.AddRange(fullScanIps);
                    _logger.Info($"Found {fullScanIps.Count} CNC(s) using full subnet scan strategy");
                }
            }

            if (discoveredIps.Any())
            {
                _logger.Info($"CNC discovery complete. Found {discoveredIps.Count} machine(s): {string.Join(", ", discoveredIps)}");
            }
            else
            {
                _logger.Warning("No CNC machines found on network");
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"CNC discovery failed: {ex.Message}");
        }

        return discoveredIps;
    }

    /// <summary>
    /// Strategy 1: Try common CNC IP addresses (fastest)
    /// </summary>
    private async Task<List<string>> TryCommonCncIpsAsync()
    {
        var commonIps = new[]
        {
            "192.168.1.100",  // Most common CNC IP
            "192.168.1.101",  // Alternative CNC IP
            "192.168.1.200",  // Another common pattern
            "192.168.1.1",    // Sometimes CNC is gateway
            "192.168.1.10",   // Sometimes CNC is .10
            "10.0.0.100",     // Alternative subnet
            "172.16.0.100"    // Alternative subnet
        };

        var discoveredIps = new List<string>();
        var tasks = commonIps.Select(async ip =>
        {
            if (await TestFocasConnectionAsync(ip))
            {
                discoveredIps.Add(ip);
                _logger.Info($"Found CNC at common IP: {ip}");
            }
        });

        await Task.WhenAll(tasks);
        return discoveredIps;
    }

    /// <summary>
    /// Strategy 2: Smart subnet scan (targeted, not brute force)
    /// </summary>
    private async Task<List<string>> SmartSubnetScanAsync()
    {
        var discoveredIps = new List<string>();
        var localIp = GetLocalIpAddress();
        var subnet = GetSubnetFromIp(localIp);

        if (string.IsNullOrEmpty(subnet))
        {
            _logger.Warning("Could not determine local subnet, skipping smart scan");
            return discoveredIps;
        }

        _logger.Info($"Smart scanning subnet: {subnet}.0/24");

        // Only scan likely CNC IPs (not entire subnet)
        var likelyCncIps = GenerateLikelyCncIps(subnet);

        foreach (var ip in likelyCncIps)
        {
            if (await TestFocasConnectionAsync(ip))
            {
                discoveredIps.Add(ip);
                _logger.Info($"Found CNC at likely IP: {ip}");
            }
        }

        return discoveredIps;
    }

    /// <summary>
    /// Strategy 3: Full subnet scan (fallback, slowest)
    /// </summary>
    private async Task<List<string>> FullSubnetScanAsync()
    {
        var discoveredIps = new List<string>();
        var localIp = GetLocalIpAddress();
        var subnet = GetSubnetFromIp(localIp);

        if (string.IsNullOrEmpty(subnet))
        {
            _logger.Warning("Could not determine local subnet, skipping full scan");
            return discoveredIps;
        }

        _logger.Info($"Full scanning subnet: {subnet}.0/24 (this may take a while)");

        // Scan entire subnet (1-254)
        var tasks = new List<Task>();
        var semaphore = new SemaphoreSlim(10, 10); // Limit concurrent connections

        for (int i = 1; i <= 254; i++)
        {
            var ip = $"{subnet}.{i}";
            tasks.Add(ScanIpWithSemaphoreAsync(ip, discoveredIps, semaphore));
        }

        await Task.WhenAll(tasks);
        return discoveredIps;
    }

    /// <summary>
    /// Scan a single IP with semaphore limiting
    /// </summary>
    private async Task ScanIpWithSemaphoreAsync(string ip, List<string> discoveredIps, SemaphoreSlim semaphore)
    {
        await semaphore.WaitAsync();
        try
        {
            if (await TestFocasConnectionAsync(ip))
            {
                lock (discoveredIps)
                {
                    discoveredIps.Add(ip);
                }
                _logger.Info($"Found CNC at IP: {ip}");
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Generate likely CNC IP addresses based on common patterns
    /// </summary>
    private string[] GenerateLikelyCncIps(string subnet)
    {
        return new[]
        {
            $"{subnet}.100",  // Common CNC IP
            $"{subnet}.101",  // Alternative CNC IP
            $"{subnet}.200",  // Another common pattern
            $"{subnet}.1",    // Sometimes CNC is gateway
            $"{subnet}.10",   // Sometimes CNC is .10
            $"{subnet}.50",   // Mid-range IP
            $"{subnet}.150",  // Mid-range IP
            $"{subnet}.250"   // High-range IP
        };
    }

    /// <summary>
    /// Test if a FOCAS connection can be established to the given IP
    /// </summary>
    private async Task<bool> TestFocasConnectionAsync(string ip)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(ip, _focasPort);
            
            if (await Task.WhenAny(connectTask, Task.Delay(_discoveryTimeoutMs)) == connectTask)
            {
                await connectTask; // Wait for completion to catch any exceptions
                
                if (client.Connected)
                {
                    _logger.Debug($"FOCAS port {_focasPort} accessible at {ip}");
                    
                    // Additional verification: try to read basic machine info
                    if (await VerifyFocasProtocolAsync(ip))
                    {
                        _logger.Info($"Verified FOCAS protocol at {ip}");
                        return true;
                    }
                }
            }
            else
            {
                _logger.Debug($"Connection timeout to {ip}:{_focasPort}");
            }
        }
        catch (Exception ex)
        {
            _logger.Debug($"Connection test failed for {ip}: {ex.Message}");
        }

        return false;
    }

    /// <summary>
    /// Verify that the device actually supports FOCAS protocol
    /// </summary>
    private async Task<bool> VerifyFocasProtocolAsync(string ip)
    {
        try
        {
            // Create a FOCAS endpoint and try to read basic machine info
            var endpoint = new FocasEndpoint(ip, (ushort)_focasPort, 3);
            
            // Try to read a basic parameter to verify it's actually a CNC
            var machineInfo = await endpoint.GetMachineInfoAsync();
            
            if (machineInfo != null)
            {
                _logger.Info($"FOCAS protocol verified at {ip} - Machine info: {machineInfo}");
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.Debug($"FOCAS protocol verification failed for {ip}: {ex.Message}");
        }

        return false;
    }

    /// <summary>
    /// Get the local IP address of this machine
    /// </summary>
    private string GetLocalIpAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warning($"Could not get local IP address: {ex.Message}");
        }

        return "192.168.1.101"; // Fallback default
    }

    /// <summary>
    /// Extract subnet from IP address
    /// </summary>
    private string GetSubnetFromIp(string ip)
    {
        try
        {
            var parts = ip.Split('.');
            if (parts.Length == 4)
            {
                return $"{parts[0]}.{parts[1]}.{parts[2]}";
            }
        }
        catch (Exception ex)
        {
            _logger.Warning($"Could not parse IP address {ip}: {ex.Message}");
        }

        return "192.168.1"; // Fallback default
    }

    /// <summary>
    /// Get discovery statistics
    /// </summary>
    public DiscoveryStats GetDiscoveryStats()
    {
        return new DiscoveryStats
        {
            DiscoveryTimeoutMs = _discoveryTimeoutMs,
            FocasPort = _focasPort,
            LocalIp = GetLocalIpAddress(),
            Subnet = GetSubnetFromIp(GetLocalIpAddress())
        };
    }
}

/// <summary>
/// Discovery statistics and information
/// </summary>
public class DiscoveryStats
{
    public int DiscoveryTimeoutMs { get; set; }
    public int FocasPort { get; set; }
    public string LocalIp { get; set; } = string.Empty;
    public string Subnet { get; set; } = string.Empty;
}
