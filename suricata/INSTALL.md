# Suricata Host Installation for Minikube CTF Monitoring (assumes Debian/Ubuntu)

```bash
# Copy custom config
sudo cp suricata.yaml /etc/suricata/suricata.yaml

# Copy custom rules
sudo mkdir -p /etc/suricata/rules
sudo cp local.rules /etc/suricata/rules/local.rules

# Create log directory
sudo mkdir -p /var/log/suricata
```

```bash
# list all interfaces; configure /etc/suricata/suricata.yaml for the interface(s) you want to monitor
ip -o link show
# default config is for enp0s3
vi /etc/suricata/suricata.yaml
```

```bash
sudo apt-get update
sudo apt-get install -y suricata rsyslog
```

