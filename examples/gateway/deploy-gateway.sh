#!/bin/bash

# Gateway Deployment Script
# This script demonstrates how to deploy the Fanuc Gateway with automatic CNC discovery

set -e

echo "=== Fanuc Gateway Deployment Script ==="
echo ""

# Configuration
GATEWAY_VERSION="1.0.0"
DOCKER_IMAGE="company/fanuc-gateway:${GATEWAY_VERSION}"
CONTAINER_NAME="fanuc-gateway"

# Default environment variables
export MACHINE_ID=${MACHINE_ID:-"CNC001"}
export SWEEP_MS=${SWEEP_MS:-1000}
export CNC_PORT=${CNC_PORT:-8193}
export CNC_TIMEOUT=${CNC_TIMEOUT:-3}
export BROKER_PORT=${BROKER_PORT:-1883}
export ENABLE_DISCOVERY=${ENABLE_DISCOVERY:-true}
export DISCOVERY_TIMEOUT_MS=${DISCOVERY_TIMEOUT_MS:-10000}

# Required environment variables
if [ -z "$BROKER_IP" ]; then
    echo "Error: BROKER_IP environment variable is required"
    echo "Please set BROKER_IP to your MQTT broker address"
    exit 1
fi

if [ -z "$BROKER_USER" ]; then
    echo "Error: BROKER_USER environment variable is required"
    echo "Please set BROKER_USER to your MQTT broker username"
    exit 1
fi

if [ -z "$BROKER_PASSWORD" ]; then
    echo "Error: BROKER_PASSWORD environment variable is required"
    echo "Please set BROKER_PASSWORD to your MQTT broker password"
    exit 1
fi

# Optional CNC IP override
if [ -n "$CNC_IP" ]; then
    echo "Using configured CNC IP: $CNC_IP"
    export ENABLE_DISCOVERY=false
else
    echo "CNC IP not specified, enabling automatic discovery"
    export CNC_IP="auto-discover"
fi

echo ""
echo "=== Deployment Configuration ==="
echo "Machine ID: $MACHINE_ID"
echo "CNC IP: $CNC_IP"
echo "CNC Port: $CNC_PORT"
echo "CNC Timeout: ${CNC_TIMEOUT}s"
echo "Sweep Interval: ${SWEEP_MS}ms"
echo "Broker: $BROKER_IP:$BROKER_PORT"
echo "Discovery Enabled: $ENABLE_DISCOVERY"
echo "Discovery Timeout: ${DISCOVERY_TIMEOUT_MS}ms"
echo ""

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    echo "Error: Docker is not running or not accessible"
    echo "Please start Docker and try again"
    exit 1
fi

# Check if image exists
if ! docker image inspect $DOCKER_IMAGE > /dev/null 2>&1; then
    echo "Error: Docker image $DOCKER_IMAGE not found"
    echo "Please ensure the image is available"
    exit 1
fi

# Stop and remove existing container if it exists
if docker ps -a --format "table {{.Names}}" | grep -q "^${CONTAINER_NAME}$"; then
    echo "Stopping existing container..."
    docker stop $CONTAINER_NAME > /dev/null 2>&1 || true
    echo "Removing existing container..."
    docker rm $CONTAINER_NAME > /dev/null 2>&1 || true
fi

# Create deployment directory
DEPLOY_DIR="/opt/fanuc-gateway"
sudo mkdir -p $DEPLOY_DIR
sudo chown $USER:$USER $DEPLOY_DIR

# Create environment file
cat > $DEPLOY_DIR/gateway.env << EOF
# Fanuc Gateway Environment Configuration
MACHINE_ID=$MACHINE_ID
SWEEP_MS=$SWEEP_MS
CNC_IP=$CNC_IP
CNC_PORT=$CNC_PORT
CNC_TIMEOUT=$CNC_TIMEOUT
BROKER_IP=$BROKER_IP
BROKER_PORT=$BROKER_PORT
BROKER_USER=$BROKER_USER
BROKER_PASSWORD=$BROKER_PASSWORD
BROKER_TLS=true
ENABLE_DISCOVERY=$ENABLE_DISCOVERY
DISCOVERY_TIMEOUT_MS=$DISCOVERY_TIMEOUT_MS
EOF

echo "Environment configuration saved to $DEPLOY_DIR/gateway.env"
echo ""

# Deploy the gateway
echo "=== Deploying Fanuc Gateway ==="
docker run -d \
    --name $CONTAINER_NAME \
    --restart unless-stopped \
    --env-file $DEPLOY_DIR/gateway.env \
    --network host \
    -v $DEPLOY_DIR:/app/config \
    $DOCKER_IMAGE

if [ $? -eq 0 ]; then
    echo "✅ Gateway deployed successfully!"
    echo ""
    echo "=== Deployment Complete ==="
    echo "Container Name: $CONTAINER_NAME"
    echo "Container ID: $(docker ps -q --filter name=$CONTAINER_NAME)"
    echo "Status: $(docker ps --format 'table {{.Status}}' --filter name=$CONTAINER_NAME | tail -n +2)"
    echo ""
    echo "=== Useful Commands ==="
    echo "View logs: docker logs -f $CONTAINER_NAME"
    echo "Stop gateway: docker stop $CONTAINER_NAME"
    echo "Start gateway: docker start $CONTAINER_NAME"
    echo "Restart gateway: docker restart $CONTAINER_NAME"
    echo "Remove gateway: docker rm -f $CONTAINER_NAME"
    echo ""
    echo "=== Monitoring ==="
    echo "The gateway will automatically discover CNC machines and start collecting data."
    echo "Check the logs to see the discovery process and data collection status."
    echo ""
    echo "Gateway is now running and collecting data!"
else
    echo "❌ Gateway deployment failed!"
    echo "Check the logs for more information: docker logs $CONTAINER_NAME"
    exit 1
fi
