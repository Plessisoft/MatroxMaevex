# MatroxMaevex

Programme SIMPL# Pro pour CP4 (production) ou DIN-AP4 (test) qui exécute des requêtes HTTP(S) vers l'API REST des
Matrox Maevex, à la demande d'un programme SIMPL qui tourne sur le même processeur.

- **Slot 1** : programme SIMPL existant
- **Slot 2** : ce programme (C#)

Les deux programmes communiquent par un **EISC** (Ethernet Intersystem Communications).

## Fonctionnement

1. SIMPL renseigne les paramètres de la requête sur les serials de l'EISC.
2. SIMPL envoie une impulsion sur le digital 1.
3. Le programme C# lit les serials, envoie la requête, puis renvoie le résultat à SIMPL :
   code HTTP, corps de la réponse, message d'erreur et impulsion de succès ou d'erreur.

Exemple, pour l'équivalent de :

```
curl -k --basic -u JohnDoe:123456 -H "Accept:application/json" -H "Content-type:application/json" \
     -X POST https://192.168.1.100/api/usb/msd1/eject -d "{\"Id\":\"msd1\"}"
```

| Serial | Valeur |
|---|---|
| 1 – Host | `192.168.1.100` |
| 2 – Login | `JohnDoe` |
| 3 – Password | `123456` |
| 4 – Method | `POST` |
| 5 – Path | `/api/usb/msd1/eject` |
| 6 – Body | `{"Id":"msd1"}` |

Puis une impulsion sur le digital 1.

### Règles

- **Protocole :** HTTPS par défaut. Pour utiliser HTTP, mettez une URL complète dans le Host (`http://…`).
- **Certificat :** le certificat auto-signé du Maevex est accepté, comme avec `curl -k`.
- **Authentification :** Basic, envoyée seulement si le Login n'est pas vide.
- **Méthode :** `GET` si le serial Method est vide.
- **Corps :** envoyé en `application/json` seulement s'il n'est pas vide.
- **Délai maximal :** 10 secondes.
- **Requêtes simultanées :** une seule à la fois. Une impulsion reçue pendant une requête en cours est ignorée.
- **Succès :** il faut un code HTTP 2xx **et** que `Result.IsSuccess` ne soit pas `false` dans la réponse. Un HTTP 200 avec `IsSuccess: false` compte comme une erreur.
- **Ordre des mises à jour :** le code HTTP, la réponse et le message d'erreur sont mis à jour **avant** l'impulsion de succès ou d'erreur. Ils peuvent donc être lus directement sur ce front.

## Configuration du programme SIMPL (slot 1)

1. Dans SIMPL Windows, ouvrez le programme du slot 1.
2. Dans la vue **Configure**, ajoutez le symbole **Ethernet Intersystem Communications**
   (Ethernet → Intersystem Communications) sous le contrôleur CP4.
3. Configurez le symbole :
   - **IP-ID** : `A0`
   - **IP Address** : `127.0.0.2`

   `127.0.0.2` est l'adresse interne du processeur. Elle permet à deux slots du même
   processeur de communiquer entre eux.
4. Dans la vue **Program**, câblez le symbole EISC selon la table des joins ci-dessous.
5. Compilez et chargez le programme SIMPL dans le slot 1.

Si l'IP-ID `A0` est déjà utilisé dans le programme SIMPL, choisissez-en un autre et
reportez-le dans la constante `EiscIpId` de `ControlSystem.cs`. La valeur doit être
identique des deux côtés.

### Table des joins (vue depuis SIMPL)

Entrées du symbole EISC (SIMPL → C#) :

| Type | Join | Rôle |
|---|---|---|
| Digital | 1 | Impulsion : envoie la requête |
| Serial | 1 | Host : `192.168.1.100`, ou URL complète `http://…` |
| Serial | 2 | Login |
| Serial | 3 | Password |
| Serial | 4 | Méthode : `GET`, `POST`, `PUT` ou `DELETE` (`GET` si vide) |
| Serial | 5 | Chemin, par exemple `/api/usb/msd1/eject` |
| Serial | 6 | Corps JSON, par exemple `{"Id":"msd1"}` (vide = pas de corps) |

Sorties du symbole EISC (C# → SIMPL) :

| Type | Join | Rôle |
|---|---|---|
| Digital | 1 | Busy : haut pendant la requête |
| Digital | 2 | Impulsion de succès |
| Digital | 3 | Impulsion d'erreur |
| Analog | 1 | Code HTTP (0 s'il n'y a pas eu de réponse : timeout, hôte injoignable…) |
| Serial | 1 | Corps de la réponse |
| Serial | 2 | Message d'erreur (vide en cas de succès) |

La table est définie dans `Joins.cs`.

> À vérifier : certaines réponses Maevex peuvent être longues. Si le serial de réponse
> arrive tronqué côté SIMPL, il faudra extraire côté C# seulement les champs utiles.

## Compilation

Ouvrez `MatroxMaevex.slnx` dans Visual Studio et compilez. Vous pouvez aussi lancer :

```powershell
dotnet build MatroxMaevex.csproj
```

Le fichier à charger est produit dans `bin\Debug\net8.0\MatroxMaevex.cpz`.

Une version compilée prête à charger est aussi fournie dans [`release/MatroxMaevex.cpz`](release/MatroxMaevex.cpz).

## Déploiement

Les scripts PowerShell recompilent le projet et envoient le `.cpz` par SFTP dans
`/program02`. Ils relancent ensuite le programme via SSH avec `progload -p:2`. Le
programme SIMPL du slot 1 n'est pas touché.

Le module PowerShell **Posh-SSH** est installé automatiquement s'il est absent.

Les IP et les identifiants ne sont pas dans les scripts. Copiez
`deploy.config.example.psd1` en `deploy.config.psd1`, qui n'est pas versionné, puis renseignez-le :

```powershell
@{
    TestIpAddress = "192.168.1.50"   # processeur de test
    ProdIpAddress = ""               # processeur de production
    Login         = "admin"          # identifiant SSH/SFTP
    Password      = "..."            # mot de passe SSH/SFTP
}
```

| Script | Cible |
|---|---|
| `deploy-test.ps1` | Processeur de test (`TestIpAddress`) |
| `deploy-prod.ps1` | Processeur de production (`ProdIpAddress`) |
| `deploy.ps1` | Script commun, `-IpAddress` obligatoire |

Paramètres communs. Une valeur passée en ligne de commande est prioritaire sur `deploy.config.psd1` :

| Paramètre | Défaut | Rôle |
|---|---|---|
| `-IpAddress` | selon le script | IP du processeur |
| `-Slot` | `2` | Slot du programme |
| `-Login` | `Login`, sinon `admin` | Identifiant SSH/SFTP |
| `-Password` | `Password` | Mot de passe SSH/SFTP (obligatoire) |
| `-SkipBuild` | — | Ne recompile pas avant l'envoi |

Exemples :

```powershell
.\deploy-test.ps1
.\deploy-prod.ps1 -IpAddress 10.0.0.10 -Password "MotDePasse"
.\deploy.ps1 -IpAddress 192.168.1.50 -SkipBuild
```

## Vérification

Dans la console du processeur (Toolbox → Text Console, ou SSH) :

- Au démarrage du slot 2, la ligne `EISC IP-ID A0 : online` indique que la liaison avec SIMPL est établie.
- Chaque signal reçu de SIMPL est affiché, par exemple `EISC serial 5 = /api/usb/msd1/eject`.
- Chaque requête affiche `HTTP POST https://…`, puis `HTTP 200 : OK` ou le message d'erreur.

Si l'EISC reste offline, vérifiez :
- que l'IP-ID et l'adresse `127.0.0.2` sont identiques des deux côtés ;
- que les deux programmes sont bien chargés et démarrés (`progcomments:1` et `progcomments:2`).

## Tests

### Banc de test sans SIMPL : `Tests\MaevexTester`

`Tests\MaevexTester` est un petit programme SIMPL# Pro qui remplace le programme SIMPL
du slot 1. Il déclare le même EISC (IP-ID `A0`, `127.0.0.2`) et ajoute deux commandes console :

| Commande | Rôle |
|---|---|
| `maevexstatus` | Affiche l'état de l'EISC (`online = True` si la liaison avec le slot 2 est établie) |
| `maevextest host\|login\|password\|method\|path\|body` | Remplit les serials 1 à 6, envoie l'impulsion sur le digital 1 et attend l'impulsion de succès ou d'erreur (15 s max) |

Au front de succès ou d'erreur, la commande affiche :
- le résultat ;
- le code HTTP ;
- le message d'erreur ;
- la réponse, tronquée à 300 caractères à l'affichage ;
- la durée ;
- la séquence des signaux reçus.

Exemple :

```
maevextest 192.168.1.100|JohnDoe|123456|POST|/api/usb/msd1/eject|{"Id":"msd1"}
```

Mise en place sur l'automate de test. Ce banc **remplace le programme du slot 1** :

```powershell
dotnet build Tests\MaevexTester\MaevexTester.csproj
# Envoyer Tests\MaevexTester\bin\Debug\net8.0\MaevexTester.cpz par SFTP dans /program01, puis en SSH :
#   progload -p:1
```

Le projet est exclu de `MatroxMaevex.csproj` (`DefaultItemExcludes`), il ne fait pas partie du programme livré.

### Résultats du 16/10/2026

- **Automate :** DIN-AP4, firmware 2.8006.00110.
- **Slot 1 :** `MaevexTester`.
- **Slot 2 :** `MatroxMaevex`.
- **EISC A0 :** online dès le chargement des deux programmes.

Faute de Maevex disponible, les requêtes visaient la page web de l'automate (certificat
auto-signé), httpbin.org et des adresses injoignables.

| # | Scénario | Résultat |
|---|---|---|
| 1 | GET sur la page web de l'automate (certificat auto-signé) | ✅ Succès, HTTP 200 : l'équivalent de `curl -k` fonctionne |
| 2 | POST avec Basic auth et corps JSON (exemple ci-dessus, envoyé à httpbin.org/anything) | ✅ Succès, HTTP 200. Corps, `Content-Type: application/json`, `Accept: application/json` et en-tête `Authorization: Basic` bien reçus |
| 3 | Méthode `get` en minuscules, identifiants corrects | ✅ Succès, HTTP 200, `authenticated: true` |
| 4 | Mauvais mot de passe, méthode vide (envoyée en GET) | ✅ Erreur, HTTP 401, `HTTP 401 UNAUTHORIZED` |
| 5 | HTTP 200 avec `{"Result":{"IsSuccess":false,"Value":"test"}}` | ✅ Erreur, HTTP 200, `Result.IsSuccess = false (test)` |
| 6 | HTTP 500 | ✅ Erreur, HTTP 500, `HTTP 500 INTERNAL SERVER ERROR` |
| 7 | Hôte injoignable | ✅ Erreur, HTTP 0, `No route to host (…:443)` |
| 8 | Host vide | ✅ Erreur, HTTP 0, `Host non renseigné` |
| 9 | Serveur qui répond en 12 s | ✅ Erreur après 10,3 s, HTTP 0, `Timeout` |
| 10 | Réponse de 20 000 caractères | ✅ Reçue en entier |
| 11 | Réponse de 70 000 caractères | ✅ Reçue en entier |

Dans tous les cas, l'ordre des signaux est conforme :

```
Busy=1 → Response="" → ErrorMessage="" → StatusCode → Response → ErrorMessage → impulsion Success/Error → Busy=0
```

Les valeurs sont donc lisibles sur le front de l'impulsion.

### Remarques

- **Réponses longues :** elles passent entières entre deux programmes C#. Un programme
  SIMPL peut quand même tronquer un serial long : la remarque de la table des joins reste valable.
- **Accents :** ils s'affichent mal dans la console et le journal d'erreurs de l'automate
  (`démarré` devient `d�marr�`). C'est uniquement visuel.

### Test avec SIMPL et un Epiphan Pearl Nano (16/10/2026)

Un programme SIMPL de test est chargé dans le slot 1, avec l'EISC en IP-ID `A0` sur
`127.0.0.2`. L'EISC est online des deux côtés et la requête fonctionne depuis SIMPL.

| Serial | Valeur |
|---|---|
| 1 – Host | `http://<IP du Pearl>` (le Pearl ne répond qu'en HTTP) |
| 2 – Login | identifiant du Pearl |
| 3 – Password | mot de passe du Pearl |
| 4 – Method | `GET` |
| 5 – Path | `/api/channels/1/status` (ou `/api/channels`, `/api/recorders`, `/api/channels/1/name`) |
| 6 – Body | vide |

Résultat attendu, puis constaté :
- impulsion Success ;
- HTTP 200 ;
- réponse du type `{"status":"ok","result":{"state":"started",…}}`.

> Le Pearl signale ses échecs par `"status"`, pas par `Result.IsSuccess`. Un HTTP 200 avec
> un statut autre que `"ok"` serait donc compté comme un succès. Il faudra l'adapter si le
> programme doit piloter des Pearl.

### Reste à tester

- Une requête vers un vrai Maevex.
