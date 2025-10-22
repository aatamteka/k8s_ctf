#!/bin/bash
set -e

minikube start --driver=docker --cpus=2 --memory=6144

eval $(minikube docker-env)

echo "  Building python-web:vulnerable..."
docker build -t python-web:vulnerable ./python-web/

echo "  Building csharp-web:vulnerable..."
docker build -t csharp-web:vulnerable ./csharp-web/

kubectl create namespace production

kubectl apply -f python-web/
kubectl apply -f csharp-web/
kubectl apply -f database/

kubectl apply -f rbac/

kubectl create secret generic postgres-credentials \
  --from-literal=password='flag{s3cr3ts_spr34d_l1k3_w1ldf1r3}' \
  -n production

kubectl create secret generic crown-jewels \
  --from-literal=api-master-key='flag{y0u_pwn3d_th3_clust3r_c0ngr4ts}' \
  -n kube-system
