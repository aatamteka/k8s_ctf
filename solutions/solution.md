## Flags

There are multiple flags hidden throughout the environment:

1. **flag{n3v3r_us3_3nv_f0r_s3cr3ts}** - Environment variables
2. **flag{m0unt3d_s3cr3ts_w1th_wr0ng_p3rm1ss10ns}** - Mounted secrets
3. **flag{c0nf1gm4ps_ar3nt_f0r_cr3ds}** - ConfigMaps
4. **flag{s3cr3ts_spr34d_l1k3_w1ldf1r3}** - Kubernetes Secrets API
5. **flag{y0u_pwn3d_th3_clust3r_c0ngr4ts}** - kube-system namespace (final flag)

## Attack Path

### Phase 1: Initial Access
Exploit vulnerabilities in the exposed web applications to gain shell access to containers.

**Target 1: Python Web App (SSTI)**
- Identify template injection vulnerability
- Craft payload for remote code execution
- Establish reverse shell

**Target 2: C# Web App (Deserialization)**
- Identify insecure deserialization endpoint
- Generate malicious payload
- Execute arbitrary code

### Phase 2: Container Reconnaissance
Once inside, enumerate the container environment:
- Check environment variables for secrets
- Explore mounted volumes (`/app/secrets`, `/config`)
- Identify service account tokens
- Review container capabilities and privileges

### Phase 3: Kubernetes API Exploitation
Use service account tokens to interact with the Kubernetes API:
- List accessible namespaces
- Enumerate secrets in production namespace
- Check RBAC permissions
- Attempt lateral movement

### Phase 4: Privilege Escalation
Exploit misconfigurations to escalate privileges:
- **Python container**: Use service account with secrets:list permissions
- **C# container**: Exploit privileged mode + docker.sock mount for container escape
- Access node filesystem
- Extract secrets from kube-system namespace

