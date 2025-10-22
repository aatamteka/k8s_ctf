#!/bin/bash
set -e

#
# Assumes docker, minikube, suricata, kubectl, jq, python3 are installed
#

minikube start --driver=docker --cpus=2 --memory=6144 \
  --extra-config=apiserver.audit-log-path=/var/log/kubeaudit.log \
  --extra-config=apiserver.audit-log-maxage=30

eval $(minikube docker-env)

docker build -t python-web:vulnerable ./python-web/

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

#might be required, depending on the environment
kubectl port-forward -n production --address 0.0.0.0 svc/python-web 30080:5000 &
kubectl port-forward -n production --address 0.0.0.0 svc/csharp-web 30081:80 &