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
└── postgres-db
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

## Blue Team: Layered Defense Strategy

This CTF provides hands-on experience implementing defense-in-depth for Kubernetes environments. The Blue Team should work through multiple defensive layers while maintaining application availability.

### Learning Objectives

- Understand detection vs prevention trade-offs
- Implement network-based monitoring (Suricata IDS)
- Perform security assessments (kube-bench, kube-hunter)
- Apply security hardening incrementally
- Balance security with operational requirements

---

## Defensive Layers

### Layer 1: Visibility & Detection (Start Here)

**Goal:** Establish baseline monitoring without disrupting services

#### 1.1 Network-Level Detection with Suricata

Examine Suricata IDS on the host to detect attack patterns (see separate instructions for deployment steps):

```bash
less /etc/suricata/suricata.yaml
less /etc/suricata/rules/local.rules
```

**Detection Patterns to Watch For:**
- Unauthorized secret access attempts
- ServiceAccount token usage from unexpected IPs
- Privilege escalation via role bindings
- Pod exec/attach operations

---

### Layer 2: Vulnerability Assessment

**Goal:** Identify security weaknesses before Red Team exploits them

#### 2.1 CIS Benchmark Compliance with kube-bench

Run Aqua Security's kube-bench to check against CIS Kubernetes Benchmarks:

```bash
# Run on minikube host
kubectl apply -f https://raw.githubusercontent.com/aquasecurity/kube-bench/main/job.yaml

# View results
kubectl logs -f job/kube-bench

# Or run locally with Docker
docker run --rm -v `pwd`:/host aquasec/kube-bench:latest install
./kube-bench
```

**Learning Activities:**
- Review FAIL items in the report
- Categorize findings by severity
- Identify which findings relate to CTF vulnerabilities
- Attempt to remediate without breaking applications

**Key Findings to Investigate:**
- Privileged containers (csharp-web)
- Insecure pod security policies
- Overly permissive RBAC
- Mounted host paths (docker.sock)

#### 2.2 Attack Surface Discovery with kube-hunter

Run Aqua Security's kube-hunter to identify exposed attack vectors:

```bash
# Passive scanning (safe for production)
docker run -it --rm aquasec/kube-hunter --pod

# Remote scanning (from student LAN perspective)
docker run -it --rm aquasec/kube-hunter --remote <minikube-ip>

# Active hunting (attempts exploits - use carefully)
docker run -it --rm aquasec/kube-hunter --remote <minikube-ip> --active
```

**Learning Activities:**
- Compare passive vs active scan results
- Identify which vulnerabilities match Red Team attack paths
- Prioritize findings based on exploitability
- Document attack vectors discovered

---

### Layer 3: Preventive Controls

**Goal:** Block attacks while maintaining service availability

#### 3.1 Network Policies

Implement microsegmentation with NetworkPolicies:

```bash
# Review existing policies
kubectl get networkpolicies -n production

# Example: Restrict python-web egress
kubectl apply -f - <<EOF
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: python-web-egress
  namespace: production
spec:
  podSelector:
    matchLabels:
      app: python-web
  policyTypes:
  - Egress
  egress:
  - to:
    - podSelector:
        matchLabels:
          app: postgres-db
    ports:
    - protocol: TCP
      port: 5432
  - to:
    - namespaceSelector: {}
    ports:
    - protocol: UDP
      port: 53
EOF
```

**Learning Activities:**
- Block reverse shell egress while allowing database access
- Test that applications still function
- Monitor dropped connections
- Document what attacks are prevented vs detected

#### 3.2 Pod Security Standards

Apply Pod Security Standards to enforce baseline security:

```bash
# Label namespace with pod security level
kubectl label namespace production \
  pod-security.kubernetes.io/enforce=baseline \
  pod-security.kubernetes.io/audit=restricted \
  pod-security.kubernetes.io/warn=restricted

# Observe which pods violate standards
kubectl get pods -n production
```

**Note:** This will break csharp-web due to privileged: true. How can you balance security vs CTF functionality?

#### 3.3 RBAC Hardening

Review and restrict ServiceAccount permissions:

```bash
# Audit current permissions
kubectl auth can-i --list --as=system:serviceaccount:production:python-web-sa

# Review role bindings
kubectl get rolebindings,clusterrolebindings -A -o wide | grep python-web-sa

# Apply principle of least privilege
kubectl edit role python-web-role -n production
```

**Challenge:** Can you prevent secret access while maintaining app functionality?

---

### Layer 4: Container Security

**Goal:** Harden container runtime and image security

#### 4.1 Image Scanning

Scan container images for vulnerabilities:

```bash
# Using Trivy
docker run --rm -v /var/run/docker.sock:/var/run/docker.sock \
  aquasec/trivy image python-web:vulnerable

docker run --rm -v /var/run/docker.sock:/var/run/docker.sock \
  aquasec/trivy image csharp-web:vulnerable
```

### Layer 5: Incident Response

**Goal:** Practice detection, response, and recovery

#### 5.1 Real-Time Monitoring Dashboard

Create a simple monitoring script:

```bash
#!/bin/bash
# blue-team-monitor.sh
while true; do
  clear
  echo "=== Blue Team Dashboard ==="
  echo "Time: $(date)"
  echo
  echo "--- Suricata Alerts (last 5) ---"
  cat /var/log/suricata/eve.json | tail -n 5 | jq -r 'select(.event_type == "alert") | "\(.timestamp) | \(.alert.signature) | \(.src_ip):\(.src_port) -> \(.dest_ip):\(.dest_port) | \(.http.http_method // "N/A") \(.http.url // "N/A")"'
  echo
  echo "--- Kubernetes Control Plane Logs ---"
  kubectl logs -n kube-system $(kubectl get pods -n kube-system -l component=kube-apiserver -o name) | tail -n5
  echo
  echo "--- Recent API Activity ---"
  kubectl get events -n production --sort-by='.lastTimestamp' | tail -5
  sleep 10
done
```

#### 5.2 Response Procedures

When an attack is detected:

1. **Contain:** Isolate compromised pod with NetworkPolicy
2. **Investigate:** Collect logs, exec history, network connections
3. **Eradicate:** Roll back to known-good state
4. **Recover:** Redeploy with additional hardening
5. **Learn:** Document what was missed and improve defenses

```bash
# Quick isolation
kubectl label pod <pod-name> -n production quarantine=true

# Apply deny-all NetworkPolicy to quarantined pods
kubectl apply -f network-policies/quarantine.yaml

# Collect forensics
kubectl logs <pod-name> -n production > incident-logs.txt
kubectl describe pod <pod-name> -n production > incident-details.txt

# Safe recovery
kubectl rollout restart deployment/<app-name> -n production
```

---

## Blue Team Student Handout

```
=== Kubernetes Security CTF - Student Instructions BLUE TEAM ===

Your Mission: Defend the Kubernetes cluster while maintaining service availability

Environment Access:
  SSH: ssh <username>@<minikube VM>
  Suricata Logs: /var/log/suricata/
  kubectl access: Configured on host

Phase 1: Establish Visibility 
  - Configure Suricata IDS
  - Enable Kubernetes audit logging
  - Create monitoring dashboard

Phase 2: Assess Vulnerabilities 
  - Run kube-bench and analyze findings
  - Run kube-hunter to map attack surface
  - Document top 5 critical issues

Phase 3: Implement Defenses 
  - Apply NetworkPolicies
  - Harden RBAC permissions
  - Enforce Pod Security Standards
  - Balance security vs functionality

Phase 4: Detect & Respond 
  - Monitor for Red Team activity
  - Practice incident response
  - Tune detection rules
  - Document lessons learned

Tool Suggestions:
  - Suricata IDS (network detection)
  - kube-bench (CIS compliance)
  - kube-hunter (vulnerability scanning)
  - kubectl (cluster management)
  - Trivy (image scanning)

Good luck!
```

## Red Team Student Handout

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