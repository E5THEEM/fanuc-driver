# Fanuc Gateway Deployment Guide

This guide explains how to deploy the Fanuc Gateway with automatic CNC discovery capabilities.

## Overview

The Fanuc Gateway is a lightweight, single-machine data collector that automatically discovers Fanuc CNC machines on the network and collects OEE data. It's designed for distributed deployment where each CNC machine has its own gateway device.

## Features

- **Automatic CNC Discovery**: Automatically finds CNC machines on the network
- **OEE Data Collection**: Collects essential data for OEE calculation
- **MQTT Transport**: Sends data to MQTT brokers
- **Environment Configuration**: Configurable via environment variables
- **Docker Deployment**: Containerized for easy deployment
- **Dual Mode Operation**: Supports both server and gateway modes

## Operation Modes

### Server Mode (Default)
```bash
# Multi-machine centralized server (existing behavior)
dotnet run --mode server
# or simply
dotnet run
```

**What it does:**
- Loads `config.machines.yml` (multiple machines)
- Manages concurrent connections to multiple CNCs
- Runs centralized data collection server

### Gateway Mode (New)
```bash
# Single-machine distributed gateway (new behavior)
dotnet run --mode gateway
```

**What it does:**
- Loads `machine.yml` (single machine)
- Manages one connection to one CNC
- Runs lightweight distributed gateway

## Prerequisites

- Docker installed and running
- Network access to CNC machines (port 8193)
- Network access to MQTT broker
- Raspberry Pi 3B+ or similar ARM64 device (recommended)

## Quick Start

### 1. Set Environment Variables

```bash
# Required
export BROKER_IP="your-mqtt-broker.com"
export BROKER_USER="your-username"
export BROKER_PASSWORD="your-password"

# Optional (with defaults)
export MACHINE_ID="CNC001"
export SWEEP_MS="1000"
export CNC_PORT="8193"
export CNC_TIMEOUT="3"
export BROKER_PORT="1883"
export BROKER_TLS="true"
export ENABLE_DISCOVERY="true"
export DISCOVERY_TIMEOUT_MS="10000"
```

### 2. Deploy Gateway

```bash
# Make script executable
chmod +x deploy-gateway.sh

# Deploy
./deploy-gateway.sh
```

### 3. Monitor Deployment

```bash
# View logs
docker logs -f fanuc-gateway

# Check status
docker ps --filter name=fanuc-gateway
```

## CNC Discovery

The gateway uses a three-tier discovery strategy to find CNC machines:

### Strategy 1: Common IPs (Fastest - 1-3 seconds)
Tries common CNC IP addresses:
- 192.168.1.100 (most common)
- 192.168.1.101
- 192.168.1.200
- 192.168.1.1
- 192.168.1.10
- 10.0.0.100
- 172.16.0.100

### Strategy 2: Smart Subnet Scan (5-10 seconds)
Scans likely CNC IPs in the local subnet:
- 192.168.1.100, 101, 200, 1, 10, 50, 150, 250

### Strategy 3: Full Subnet Scan (30-60 seconds)
Scans entire subnet (1-254) as fallback.

## Configuration Options

### Environment Variables

| Variable | Description | Default | Required |
|----------|-------------|---------|----------|
| `MACHINE_ID` | Unique machine identifier | `auto-generated` | No |
| `CNC_IP` | CNC IP address | `auto-discover` | No |
| `CNC_PORT` | FOCAS port | `8193` | No |
| `CNC_TIMEOUT` | Connection timeout (seconds) | `3` | No |
| `SWEEP_MS` | Data collection interval (ms) | `1000` | No |
| `BROKER_IP` | MQTT broker IP address | - | **Yes** |
| `BROKER_PORT` | MQTT broker port | `1883` | No |
| `BROKER_USER` | MQTT username | - | **Yes** |
| `BROKER_PASSWORD` | MQTT password | - | **Yes** |
| `BROKER_TLS` | Enable TLS encryption | `true` | No |
| `ENABLE_DISCOVERY` | Enable automatic discovery | `true` | No |
| `DISCOVERY_TIMEOUT_MS` | Discovery timeout (ms) | `10000` | No |

### Configuration Files

The gateway uses three configuration files:

#### `config.system.yml` - System Configuration
```yaml
# System-level settings
logging:
  level: INFO
  file: gateway.log

system:
  hostname: ${HOSTNAME:-gateway}
  timezone: ${TZ:-UTC}

network:
  discovery_enabled: ${ENABLE_DISCOVERY:-true}
  discovery_timeout_ms: ${DISCOVERY_TIMEOUT_MS:-10000}
```

#### `config.user.yml` - User Configuration
```yaml
# User-specific settings
user:
  company: ${COMPANY_NAME:-Company}
  site: ${SITE_NAME:-Site}
  department: ${DEPARTMENT:-Production}

features:
  health_monitoring: ${HEALTH_MONITORING:-true}
  remote_access: ${REMOTE_ACCESS:-false}
```

#### `machine.yml` - Machine Configuration
```yaml
# Single machine configuration
machine:
  id: ${MACHINE_ID:-auto-generated}
  type: l99.driver.fanuc.FanucMachine, fanuc
  strategy: l99.driver.fanuc.strategies.FanucMultiStrategy, fanuc
  handler: l99.driver.fanuc.handlers.FanucOne, fanuc
  transport: l99.driver.fanuc.transports.MQTT, fanuc

# Collectors enabled by default
collectors:
  - MachineInfo      # Machine identification
  - StateData        # Machine status & timing
  - ProductionData   # Part counts & cycle times
  - Alarms          # Alarm status & quality
```

## Deployment Scenarios

### Scenario 1: Automatic Discovery (Recommended)
```bash
# Gateway automatically finds CNC
export BROKER_IP="broker.company.com"
export BROKER_USER="gateway_user"
export BROKER_PASSWORD="secure_password"
./deploy-gateway.sh
```

### Scenario 2: Known CNC IP
```bash
# Use specific CNC IP
export CNC_IP="192.168.1.100"
export BROKER_IP="broker.company.com"
export BROKER_USER="gateway_user"
export BROKER_PASSWORD="secure_password"
./deploy-gateway.sh
```

### Scenario 3: Custom Configuration
```bash
# Full custom configuration
export MACHINE_ID="PROD_CNC_01"
export CNC_IP="10.0.1.50"
export SWEEP_MS="500"
export BROKER_IP="mqtt.company.com"
export BROKER_PORT="8883"
export BROKER_USER="prod_user"
export BROKER_PASSWORD="prod_password"
export BROKER_TLS="true"
./deploy-gateway.sh
```

## Testing Gateway Mode

### Local Testing
```bash
# Test gateway mode locally
chmod +x test-gateway-mode.sh
./test-gateway-mode.sh
```

### Manual Testing
```bash
# Test different modes
dotnet run --mode server      # Server mode
dotnet run --mode gateway    # Gateway mode
dotnet run                   # Default (server mode)

# Test with custom configuration
dotnet run --mode gateway --config config.system.yml,config.user.yml,machine.yml
```

## Data Collection

### OEE Data Points Collected

1. **StateData** (Machine Status)
   - Power-on time
   - Operating time
   - Cutting time
   - Machine status (running/idle/faulted)
   - Feed/rapid/spindle overrides
   - Modal information

2. **ProductionData** (Production Metrics)
   - Part counts (good/bad/scrapped)
   - Cycle times
   - Program execution status
   - Production mode

3. **Alarms** (Quality & Issues)
   - Active alarms
   - Alarm history
   - Alarm duration
   - Alarm severity

### MQTT Topics

Data is published to MQTT topics in this format:
```
fanuc/{MACHINE_ID}/{COLLECTOR_NAME}
```

Examples:
- `fanuc/CNC001/state`
- `fanuc/CNC001/production`
- `fanuc/CNC001/alarms`

## Troubleshooting

### Common Issues

1. **CNC Not Found**
   ```bash
   # Check discovery logs
   docker logs fanuc-gateway | grep -i discovery
   
   # Verify network connectivity
   ping 192.168.1.100
   telnet 192.168.1.100 8193
   ```

2. **MQTT Connection Failed**
   ```bash
   # Check broker connectivity
   telnet your-broker.com 1883
   
   # Verify credentials
   echo $BROKER_USER
   echo $BROKER_PASSWORD
   ```

3. **Data Not Being Collected**
   ```bash
   # Check collector status
   docker logs fanuc-gateway | grep -i collector
   
   # Verify CNC connection
   docker logs fanuc-gateway | grep -i "cnc connected"
   ```

### Log Analysis

```bash
# View all logs
docker logs fanuc-gateway

# Filter by discovery
docker logs fanuc-gateway | grep -i discovery

# Filter by connection
docker logs fanuc-gateway | grep -i "cnc\|broker"

# Filter by data collection
docker logs fanuc-gateway | grep -i "data\|collect"
```

## Performance

### Discovery Performance
- **Common IPs**: 1-3 seconds
- **Smart Scan**: 5-10 seconds
- **Full Scan**: 30-60 seconds

### Data Collection Performance
- **Collection Interval**: Configurable (default: 1000ms)
- **Data Size**: ~8KB per collection cycle
- **MQTT Messages**: 3 per collection cycle
- **CPU Usage**: ~8% on Raspberry Pi 3B+

## Security

### Network Security
- TLS encryption enabled by default
- Authentication required for MQTT
- No unnecessary network services exposed

### Data Security
- No sensitive data stored locally
- Data transmitted over encrypted MQTT
- Authentication credentials via environment variables

## Scaling

### Multiple Gateways
Each CNC machine should have its own gateway:
- Gateway 1: CNC001 → MQTT Broker
- Gateway 2: CNC002 → MQTT Broker
- Gateway 3: CNC003 → MQTT Broker

### Load Distribution
- Each gateway operates independently
- No central coordination required
- Linear scaling with machine count

## Migration from Server Mode

### Existing Customers
```bash
# Keep existing behavior unchanged
dotnet run --mode server
# or simply
dotnet run
```

### New Deployments
```bash
# Use new gateway mode
dotnet run --mode gateway
```

### Gradual Transition
1. Deploy gateway mode alongside existing server
2. Test and validate functionality
3. Gradually migrate machines to gateway mode
4. Decommission server mode when ready

## Support

For technical support or questions:
1. Check the logs: `docker logs fanuc-gateway`
2. Verify network connectivity
3. Confirm environment variable configuration
4. Review this documentation
5. Test mode detection: `./test-gateway-mode.sh`

## Version History

- **v1.0.0**: Initial gateway release with automatic discovery
- **v1.1.0**: Added dual-mode operation (server/gateway)
- Transport optimization (MQTT only)
- Environment variable configuration
- Automatic CNC discovery
- Simplified deployment process
- Backward compatibility maintained
