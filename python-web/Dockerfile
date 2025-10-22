FROM python:3.9-slim

# Intentionally include useful tools for attacker
RUN apt-get update && apt-get install -y curl wget netcat-openbsd procps

WORKDIR /app
COPY requirements.txt .
RUN pip install -r requirements.txt

COPY app.py .

# Run as root (bad practice)
CMD ["python", "app.py"]
