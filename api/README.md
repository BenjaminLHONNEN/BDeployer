# BDeployer API

API ASP.NET Core .NET 10 permettant d'enregistrer des projets Git et de déployer leurs environnements sur un hôte Docker.

## Fonctionnement

Pour chaque environnement, le serveur utilise le chemin :

```text
/opt/bdeployer/projects/{projectId}/{environment}
```

Au premier déploiement, le dépôt est cloné depuis la branche `main`. Aux déploiements suivants, l'API :

1. vérifie l'URL du remote `origin` ;
2. refuse le déploiement en présence de modifications locales ;
3. exécute `git pull --ff-only origin main` ;
4. exécute le script avec `/bin/bash -euo pipefail` ;
5. conserve le résultat, les logs, le script et les commits Git dans SQLite.

Deux déploiements du même environnement ne peuvent pas s'exécuter simultanément. Le verrou est local à l'instance : le service doit donc rester à une seule réplique.

## Authentification

Tous les endpoints sauf `/health` exigent :

```http
X-API-Key: votre-cle
```

La clé est fournie à l'application avec `BDEPLOYER_API_KEY`, traduit dans Compose vers `ApiKey__Key`.

## Endpoints

```text
GET    /projects
POST   /projects
GET    /projects/{projectId}
PUT    /projects/{projectId}
DELETE /projects/{projectId}

POST   /projects/{projectId}/environments
GET    /projects/{projectId}/environments/{environmentId}
PUT    /projects/{projectId}/environments/{environmentId}
DELETE /projects/{projectId}/environments/{environmentId}

POST   /projects/{projectId}/environments/{environmentId}/deploy
GET    /projects/{projectId}/environments/{environmentId}/deployments
GET    /deployments/{deploymentId}

GET    /health
```

La suppression en base ne supprime jamais les dépôts présents dans `/opt/bdeployer/projects`.

### Créer un projet avec un environnement

```http
POST /projects
X-API-Key: votre-cle
Content-Type: application/json

{
  "name": "Mon application",
  "gitUrl": "git@github.com:organisation/application.git",
  "enabled": true,
  "environments": [
    {
      "name": "production",
      "deploymentScript": "docker compose pull\ndocker compose --env-file .env -f docker-compose.prod.yml up -d --build",
      "timeoutSeconds": 300,
      "enabled": true
    }
  ]
}
```

Les noms d'environnement acceptent les lettres minuscules, les chiffres et les tirets.

## Préparation du VPS

Créer les dossiers et une clé SSH dédiée :

```bash
sudo mkdir -p /opt/bdeployer/projects /opt/bdeployer/ssh
sudo chown -R 10001:10001 /opt/bdeployer/projects
sudo cp /chemin/vers/id_ed25519 /opt/bdeployer/ssh/id_ed25519
sudo cp /chemin/vers/id_ed25519.pub /opt/bdeployer/ssh/id_ed25519.pub
sudo ssh-keyscan github.com | sudo tee /opt/bdeployer/ssh/known_hosts
sudo chmod 700 /opt/bdeployer/ssh
sudo chmod 600 /opt/bdeployer/ssh/id_ed25519 /opt/bdeployer/ssh/known_hosts
sudo chown -R 10001:10001 /opt/bdeployer/ssh
```

Ajouter la clé publique comme deploy key GitHub. Elle doit avoir un accès en lecture au dépôt.

Récupérer le GID du socket Docker :

```bash
stat -c '%g' /var/run/docker.sock
```

Copier `.env.example` vers `.env`, générer une clé longue et renseigner `DOCKER_GID`. Le réseau indiqué par `TRAEFIK_NETWORK` doit être celui utilisé par Traefik :

```bash
cp .env.example .env
openssl rand -hex 32
docker network ls
```

## Production avec Traefik

Le Compose de production ne publie aucun port sur l'hôte. Il rejoint le réseau externe Traefik et déclare le routeur pour `bdeployer.bquick.dev`.

```bash
docker compose -f docker-compose.prod.yml up --build -d
```

Vérification :

```bash
curl https://bdeployer.bquick.dev/health
curl -H "X-API-Key: $BDEPLOYER_API_KEY" https://bdeployer.bquick.dev/projects
```

## Développement

```powershell
docker compose -f docker-compose.dev.yml up --build
```

L'API écoute alors sur `http://localhost:8080`. Le document OpenAPI est disponible en développement sur `/openapi/v1.json`.

Développement sans Docker :

```powershell
$env:ApiKey__Key = "development-only-key"
$env:Deployment__ProjectsRoot = "$PWD/dev-projects"
dotnet restore BDeployer.slnx --configfile NuGet.Config
dotnet run
```

## Migrations

Les migrations sont appliquées automatiquement au démarrage :

```powershell
dotnet tool restore --configfile NuGet.Config
dotnet tool run dotnet-ef migrations add NomDeLaMigration
```

## Sécurité

Le montage de `/var/run/docker.sock` donne au conteneur un contrôle équivalent à root sur l'hôte. BDeployer doit être considéré comme un service privilégié : clé API forte, HTTPS obligatoire, une seule instance, image maintenue à jour et accès au domaine restreint lorsque possible.
