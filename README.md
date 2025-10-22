# Kubernetes Security CTF

A hands-on Capture The Flag environment for learning Kubernetes security concepts through practical exploitation of common misconfigurations. Both red and blue team scenarios are supported with the red team attacking to trivially vulnerable webapps and searching for a number of flag strings hidden in the environment and the blue team having access to the underlying host where Suricata is set up as a simple IDS.

## Scenario Overview (Red Team)

You are a security researcher who has been granted external access to two web applications running in a production Kubernetes cluster. Your objective is to exploit vulnerabilities in these applications to:

1. Gain initial access to the containers
2. Escalate privileges within the cluster
3. Extract sensitive secrets and flags
4. Achieve cluster administrator access
(optional) Move laterally through the environment and exfiltrate data (this is good for seeing if the Blue Team can detect you)

## Scenario Overview (Blue Team)

You are a cybersecurity professional who has recently taken over this kubernetes deployment. You are given the sample Suricata deployment and have access to the unerlying kubernets host. You must detect and try to intercept the incoming attack while maintaining availability of your applications.


## Prerequisites

- Docker (for building images and running minikube)
- minikube
- kubectl
- 8GB+ RAM available for VM
- Linux/Mac (Windows with WSL2)
- Python 3 (with requests, json, and urllib for the exploit scripts)
- netcat
- curl
- ssh (for the blue team)

## Quick Start

```bash
# 1. Clone and navigate to directory
cd k8s_ctf

# 2. Run setup script (builds images and deploys cluster)
chmod +x setup.sh
./setup.sh

# 3. Wait for deployment (3-5 minutes)
kubectl wait --for=condition=ready pod --all -n production --timeout=300s

# 4. Get service URLs
echo "Python Web App:"
minikube service python-web -n production --url

echo "C# Web App:"
minikube service csharp-web -n production --url
```

## Architecture

```
production namespace:
├── python-web (NodePort 30080)
│   ├── Vulnerability: Server-Side Template Injection (SSTI)
│   ├── Service Account: python-web-sa (with secrets read permissions)
│   └── Mounted secrets with insecure permissions
├── csharp-web (NodePort 30081)
│   ├── Vulnerability: Insecure JSON Deserialization (Newtonsoft.Json)
│   ├── Privileged container with docker.sock mounted
│   └── Default service account
├── postgres-db
    └── PostgreSQL database with secrets
```

### Sample Network Deployment
```
Students (Unrouted LAN)
       |
       v
Host Machine (VirtualBox Debian VM with minikube)
       |
       v
minikube (Docker driver) --> Suricata IDS
       |
       +-- python-web:30080
       +-- csharp-web:30081
```
Port forwarding may be required for the lab LAN to be able to access the intentionally vulnerable apps on minikube:

```bash
kubectl port-forward -n production --address 0.0.0.0 svc/python-web 30080:5000 &
kubectl port-forward -n production --address 0.0.0.0 svc/csharp-web 30081:80 &
```

## Hints

Need help? Check `solutions/hints.md` for progressive hints organized by phase.

## Solutions

Complete walkthrough available in `solutions/WALKTHROUGH.md` after attempting the challenge.

## Network Monitoring with Suricata (Blue Team)

This CTF includes an optional Suricata deployment for detecting attacks and monitoring internal cluster traffic on the minikube host itself. The blue team is encouraged to experiment with manipulating the sample Suricata rules and modify the kubernetes environment to try to mitigate the attacks. 

The file suricata/INSTALL.md provides setup instructions for the VM running minikube with the provided Suricata config.

## Student Handout Template

Provide this to students:

```
=== Kubernetes Security CTF - Student Instructions RED TEAM ===

Target Applications:
  Python Web:  http://192.168.1.100:30080
  C# Web:      http://192.168.1.100:30081

Objective: Find 5 flags hidden in the Kubernetes environment

Tools Needed:
  - curl or web browser
  - netcat (nc)
  - Python 3
  - Text editor

Setup Your Attack Environment:
  1. Set up reverse shell listener:
     nc -lvnp 4444

  2. Set target URLs:
     export PYTHON_URL=http://192.168.1.100:30080
     export CSHARP_URL=http://192.168.1.100:30081

  3. Test connectivity:
     curl $PYTHON_URL
     curl $CSHARP_URL/vulnerable

Hints: Available in the hints/ directory

Good luck!
```

### Monitoring Student Activity

Track who's doing what (optional):

```bash
# Enable audit logging
minikube start --driver=docker --cpus=2 --memory=8192 \
  --extra-config=apiserver.audit-log-path=/var/log/kubernetes/audit.log

# View logs
kubectl logs -n kube-system $(kubectl get pods -n kube-system -l component=kube-apiserver -o name)
```

## Cleanup After Class

```bash
# remove the namespaces where the containers ran
kubectl delete namespace production --ignore-not-found=true
kubectl delete namespace monitoring --ignore-not-found=true

# (optional full reset) Destroy the environment
minikube delete

# (optional full reset) Remove all data
rm -rf ~/.minikube/
docker system prune -af
```

## Questions?

Common student questions:

**Q: How do I get a reverse shell back to my laptop?**
A: Use your laptop's IP on the LAN. Set up listener: `nc -lvnp 4444`, then use that IP in the exploit payload.

**Q: The C# deserialization doesn't work**
A: Ensure you're sending JSON with `{"data": "BASE64_PAYLOAD"}` and Content-Type: application/json header.

**Q: I can access Kubernetes API but can't read kube-system secrets**
A: You need to escalate privileges first (via docker.sock in C# container).

**Q: Can I reset my environment?**
A: Instructor can restart pods: `kubectl rollout restart deployment/python-web deployment/csharp-web -n production`

## Security Notes

**WARNING**: This environment contains intentionally vulnerable applications and misconfigurations.

- Only deploy in isolated lab environments
- Never expose to production networks
- Do not reuse any code patterns from this CTF in real applications
- All vulnerabilities are for educational purposes only

Created for educational purposes to demonstrate Kubernetes security concepts and common attack patterns.