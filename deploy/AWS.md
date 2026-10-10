# Family OS on AWS Free Tier — Portainer + Traefik

Deploy path: **EC2 → Docker → Traefik + Portainer → Family OS stack**.

## Reality check (memory)

| Instance | RAM | Fits this stack? |
|----------|-----|------------------|
| **t2.micro / t3.micro** (free tier) | 1 GB | Tight. Keycloak + Postgres + RabbitMQ + API often OOM. Use **2 GB swap** or upgrade. |
| **t3.small** | 2 GB | Comfortable for MVP |
| **t3.medium** | 4 GB | Comfortable |

Free tier still covers the **account** (12 months / always-free limits); paying ~$15/mo for small is often worth it.

## 1. Create the server (AWS console)

1. **EC2 → Launch instance**
   - AMI: **Ubuntu 24.04 LTS**
   - Type: `t3.micro` (or `t3.small` if not free-only)
   - Storage: 20–30 GB gp3
   - Key pair: create/download `.pem`
2. **Security group** (inbound):
   - `22` SSH — **your IP only** (or VPN egress IP)
   - `80` HTTP — `0.0.0.0/0` (Let’s Encrypt + redirect)
   - `443` HTTPS — `0.0.0.0/0`
   - Do **not** open 5432, 5672, 8080, 5080, 9000 publicly
3. **Elastic IP** (optional but recommended) → associate to the instance
4. **DNS** (Route 53 or any DNS):
   - `A` `@` or host → Elastic IP  
   - `A` `api` → same IP  
   - `A` `auth` → same IP  
   - `A` `portainer` → same IP  
   - `A` `traefik` → same IP (optional dashboard)

Example domain: `familyos.example.com` → hosts `api.`, `auth.`, `portainer.`

## 2. Connect over VPN (your setup)

1. Connect to your VPN first so SSH source IP matches the SG rule.
2. SSH:

```bash
chmod 400 your-key.pem
ssh -i your-key.pem ubuntu@YOUR_ELASTIC_IP
```

## 3. Server bootstrap (Docker + swap)

```bash
# Update
sudo apt-get update && sudo apt-get upgrade -y

# Docker (official)
sudo apt-get install -y ca-certificates curl
sudo install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo $VERSION_CODENAME) stable" | sudo tee /etc/apt/sources.list.d/docker.list
sudo apt-get update
sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-compose-plugin
sudo usermod -aG docker ubuntu
# re-login for docker group

# 2GB swap (critical on 1GB micro)
sudo fallocate -l 2G /swapfile
sudo chmod 600 /swapfile
sudo mkswap /swapfile
sudo swapon /swapfile
echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab
```

## 4. Clone repo and edge stack (Traefik + Portainer)

```bash
git clone https://github.com/tmassey1979/FamilyOS.git
cd FamilyOS

# Edit ACME email in static config
nano deploy/traefik/traefik.yml
# set: email: you@yourdomain.com

export DOMAIN=yourdomain.com
export ACME_EMAIL=you@yourdomain.com

docker network create proxy
docker compose -f deploy/traefik/docker-compose.yml up -d
```

Open (after DNS propagates):

- https://portainer.yourdomain.com — create admin user on first visit  
- https://traefik.yourdomain.com — dashboard (optional)

Portainer UI is the preferred way to manage containers after this.

## 5. Deploy Family OS stack

```bash
cp deploy/familyos/.env.example deploy/familyos/.env
nano deploy/familyos/.env   # DOMAIN + strong passwords

docker compose -f deploy/familyos/docker-compose.yml --env-file deploy/familyos/.env up -d --build
```

First API build can take 5–10 minutes on micro.

Check:

```bash
docker compose -f deploy/familyos/docker-compose.yml ps
curl -fsS https://api.yourdomain.com/health
```

Endpoints:

| Host | Service |
|------|---------|
| `https://api.DOMAIN` | Family OS API |
| `https://auth.DOMAIN` | Keycloak |
| `https://portainer.DOMAIN` | Portainer |
| `https://traefik.DOMAIN` | Traefik dashboard |

## 6. Portainer workflow (ongoing)

1. Open Portainer → local environment  
2. **Stacks** → see `edge` and `familyos`  
3. Update: edit stack / re-pull / rebuild  
4. Logs and console without SSH when needed  

## 7. Mobile / API URL

Point the app at the public API:

```text
EXPO_PUBLIC_API_URL=https://api.yourdomain.com
```

Keycloak authority becomes `https://auth.yourdomain.com/realms/familyos` (already set in compose).

## 8. Hardening checklist

- [ ] SSH key only; disable password auth  
- [ ] SG: SSH locked to VPN/home IP  
- [ ] Postgres/RabbitMQ not published to host  
- [ ] Strong `.env` passwords; never commit `.env`  
- [ ] Let’s Encrypt certificates issued (check Traefik logs if HTTPS fails)  
- [ ] Turn `SEED_ENABLED=false` after first successful seed if you don’t want re-seed noise  
- [ ] Regular `apt upgrade` + Docker updates  

## 9. Cost / free tier tips

- Stop the instance when idle (EBS still costs a little)  
- Use one region close to you  
- CloudWatch free tier is enough for basic alarms  
- Prefer **one** small always-on box over many micro services on micro RAM  

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| OOM / containers restart | Add swap or move to t3.small |
| ACME / TLS fail | DNS A records must point to this host; ports 80/443 open |
| API unhealthy | `docker logs familyos-api-1` — often DB not ready or bad connection string |
| Keycloak hostname | Must match `auth.DOMAIN` and public HTTPS URL |
| Can’t reach Portainer | Wait for cert; try `http://IP:9000` only temporarily if you publish 9000 (not recommended long-term) |
