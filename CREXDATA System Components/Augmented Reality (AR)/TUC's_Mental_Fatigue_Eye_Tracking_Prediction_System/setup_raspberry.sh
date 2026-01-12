#!/bin/bash
# Setup script for Raspberry Pi Eye Tracking Client
# Run this script on each Raspberry Pi device

set -e

echo "========================================"
echo "Eye Tracking Client Setup"
echo "========================================"
echo ""

# Check if running on Raspberry Pi
if [ ! -f /proc/cpuinfo ] || ! grep -q "Raspberry Pi" /proc/device-tree/model 2>/dev/null; then
    echo "⚠️  Warning: This doesn't appear to be a Raspberry Pi"
    read -p "Continue anyway? (y/N): " confirm
    if [ "$confirm" != "y" ] && [ "$confirm" != "Y" ]; then
        exit 1
    fi
fi

# Get server IP
read -p "Enter server IP address [192.168.1.100]: " SERVER_IP
SERVER_IP=${SERVER_IP:-192.168.1.100}

# Get optional custom ID
read -p "Enter custom Raspberry Pi ID (leave blank for auto-detect): " CUSTOM_ID

# Update system
echo ""
echo "📦 Updating system packages..."
sudo apt-get update

# Install dependencies
echo ""
echo "📦 Installing dependencies..."
sudo apt-get install -y python3 python3-pip python3-opencv ffmpeg

# Install Python packages
echo ""
echo "🐍 Installing Python packages..."
pip3 install --user opencv-python

# Copy client script to home directory
if [ -f "client.py" ]; then
    echo ""
    echo "📋 Copying client.py to home directory..."
    cp client.py ~/client.py
    chmod +x ~/client.py
    
    # Update server IP in client.py
    sed -i "s/SERVER_HOST = \".*\"/SERVER_HOST = \"$SERVER_IP\"/" ~/client.py
    
    if [ -n "$CUSTOM_ID" ]; then
        sed -i "s/RASPBERRY_ID = None/RASPBERRY_ID = \"$CUSTOM_ID\"/" ~/client.py
    fi
else
    echo "❌ Error: client.py not found in current directory"
    exit 1
fi

# Setup systemd service
echo ""
read -p "Setup auto-start service? (Y/n): " setup_service
if [ "$setup_service" != "n" ] && [ "$setup_service" != "N" ]; then
    echo "🔧 Setting up systemd service..."
    
    # Create service file
    sudo tee /etc/systemd/system/eyetracking-client.service > /dev/null <<EOF
[Unit]
Description=Eye Tracking Client for Raspberry Pi
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=$USER
WorkingDirectory=$HOME
ExecStart=/usr/bin/python3 $HOME/client.py
Restart=always
RestartSec=10
StandardOutput=journal
StandardError=journal

[Install]
WantedBy=multi-user.target
EOF

    # Reload systemd
    sudo systemctl daemon-reload
    
    # Enable service
    sudo systemctl enable eyetracking-client.service
    
    echo "✅ Service installed and enabled"
    
    # Ask to start now
    read -p "Start service now? (Y/n): " start_now
    if [ "$start_now" != "n" ] && [ "$start_now" != "N" ]; then
        sudo systemctl start eyetracking-client.service
        echo ""
        echo "✅ Service started!"
        sleep 2
        echo ""
        echo "Service status:"
        sudo systemctl status eyetracking-client.service --no-pager
    fi
fi

# Test camera
echo ""
read -p "Test camera? (Y/n): " test_camera
if [ "$test_camera" != "n" ] && [ "$test_camera" != "N" ]; then
    echo "📹 Testing camera..."
    python3 -c "import cv2; cap = cv2.VideoCapture(0); ret, frame = cap.read(); cap.release(); print('✅ Camera OK' if ret else '❌ Camera failed')"
fi

echo ""
echo "========================================"
echo "✅ Setup Complete!"
echo "========================================"
echo ""
echo "Configuration:"
echo "  Server IP: $SERVER_IP"
echo "  Client script: ~/client.py"
if [ "$setup_service" != "n" ] && [ "$setup_service" != "N" ]; then
    echo "  Service: eyetracking-client.service"
fi
echo ""
echo "Useful commands:"
echo "  Manual start: python3 ~/client.py"
if [ "$setup_service" != "n" ] && [ "$setup_service" != "N" ]; then
    echo "  Service status: sudo systemctl status eyetracking-client"
    echo "  View logs: sudo journalctl -u eyetracking-client -f"
    echo "  Stop service: sudo systemctl stop eyetracking-client"
    echo "  Disable auto-start: sudo systemctl disable eyetracking-client"
fi
echo ""
