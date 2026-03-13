# add-k8s-resource

Create a Kubernetes manifest with Kustomize base + overlays (dev/staging/prod) following the project's deployment patterns.

## Arguments

- `resource` (required) — Resource type: `deployment`, `service`, `ingress`, `statefulset`, `configmap`, `secret`, `hpa`, `cronjob`, `pvc`
- `name` (required) — Resource name (e.g., `blog-api`, `redis`, `postgres`)
- `overlay` (optional) — Specific overlay to create/update: `dev`, `staging`, `prod`, or `all` (default: `all`)

## Instructions

You are creating Kubernetes manifests for the blog-platform using Kustomize with base + overlays pattern.

### Directory Structure

```
deploy/k8s/
├── base/
│   ├── kustomization.yaml
│   ├── namespace.yaml
│   ├── blog-api/
│   │   ├── deployment.yaml
│   │   ├── service.yaml
│   │   └── hpa.yaml
│   ├── blog-web/
│   │   ├── deployment.yaml
│   │   ├── service.yaml
│   │   └── hpa.yaml
│   ├── blog-admin/
│   │   ├── deployment.yaml
│   │   └── service.yaml
│   ├── postgres/
│   │   ├── statefulset.yaml
│   │   ├── service.yaml
│   │   └── pvc.yaml
│   ├── redis/
│   │   ├── statefulset.yaml
│   │   ├── service.yaml
│   │   └── pvc.yaml
│   ├── minio/
│   │   ├── statefulset.yaml
│   │   ├── service.yaml
│   │   └── pvc.yaml
│   └── ingress.yaml
└── overlays/
    ├── dev/
    │   ├── kustomization.yaml
    │   └── patches/
    ├── staging/
    │   ├── kustomization.yaml
    │   └── patches/
    └── prod/
        ├── kustomization.yaml
        └── patches/
```

### Application Deployment (blog-api)

```yaml
# deploy/k8s/base/blog-api/deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: blog-api
  labels:
    app: blog-api
    tier: backend
spec:
  replicas: 2
  selector:
    matchLabels:
      app: blog-api
  template:
    metadata:
      labels:
        app: blog-api
        tier: backend
    spec:
      containers:
        - name: blog-api
          image: ghcr.io/blog-platform/blog-api:latest
          ports:
            - containerPort: 8080
              name: http
          env:
            - name: ASPNETCORE_ENVIRONMENT
              valueFrom:
                configMapKeyRef:
                  name: blog-config
                  key: environment
            - name: ConnectionStrings__BlogDb
              valueFrom:
                secretKeyRef:
                  name: blog-secrets
                  key: db-connection-string
            - name: ConnectionStrings__Redis
              valueFrom:
                secretKeyRef:
                  name: blog-secrets
                  key: redis-connection-string
          resources:
            requests:
              cpu: 250m
              memory: 256Mi
            limits:
              cpu: 1000m
              memory: 512Mi
          readinessProbe:
            httpGet:
              path: /health/ready
              port: http
            initialDelaySeconds: 10
            periodSeconds: 5
          livenessProbe:
            httpGet:
              path: /health/live
              port: http
            initialDelaySeconds: 15
            periodSeconds: 10
```

### StatefulSet (PostgreSQL)

```yaml
# deploy/k8s/base/postgres/statefulset.yaml
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: postgres
  labels:
    app: postgres
    tier: database
spec:
  serviceName: postgres
  replicas: 1
  selector:
    matchLabels:
      app: postgres
  template:
    metadata:
      labels:
        app: postgres
        tier: database
    spec:
      containers:
        - name: postgres
          image: postgres:18
          ports:
            - containerPort: 5432
          env:
            - name: POSTGRES_DB
              value: blog_db
            - name: POSTGRES_USER
              valueFrom:
                secretKeyRef:
                  name: blog-secrets
                  key: db-username
            - name: POSTGRES_PASSWORD
              valueFrom:
                secretKeyRef:
                  name: blog-secrets
                  key: db-password
            - name: PGDATA
              value: /var/lib/postgresql/data/pgdata
          resources:
            requests:
              cpu: 500m
              memory: 1Gi
            limits:
              cpu: 2000m
              memory: 2Gi
          volumeMounts:
            - name: postgres-data
              mountPath: /var/lib/postgresql/data
          readinessProbe:
            exec:
              command: ['pg_isready', '-U', '$(POSTGRES_USER)']
            initialDelaySeconds: 5
            periodSeconds: 10
  volumeClaimTemplates:
    - metadata:
        name: postgres-data
      spec:
        accessModes: ['ReadWriteOnce']
        resources:
          requests:
            storage: 20Gi
```

### HPA (Horizontal Pod Autoscaler)

```yaml
# deploy/k8s/base/blog-api/hpa.yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: blog-api-hpa
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: blog-api
  minReplicas: 2
  maxReplicas: 4
  metrics:
    - type: Resource
      resource:
        name: cpu
        target:
          type: Utilization
          averageUtilization: 70
    - type: Resource
      resource:
        name: memory
        target:
          type: Utilization
          averageUtilization: 80
```

### Kustomize Overlays

```yaml
# deploy/k8s/overlays/dev/kustomization.yaml
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
namespace: blog-dev

resources:
  - ../../base

patches:
  - target:
      kind: Deployment
      name: blog-api
    patch: |
      - op: replace
        path: /spec/replicas
        value: 1
  - target:
      kind: HorizontalPodAutoscaler
      name: blog-api-hpa
    patch: |
      - op: replace
        path: /spec/minReplicas
        value: 1
      - op: replace
        path: /spec/maxReplicas
        value: 2

configMapGenerator:
  - name: blog-config
    literals:
      - environment=Development
```

```yaml
# deploy/k8s/overlays/prod/kustomization.yaml
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
namespace: blog-prod

resources:
  - ../../base

patches:
  - target:
      kind: Deployment
      name: blog-api
    patch: |
      - op: replace
        path: /spec/replicas
        value: 3
  - target:
      kind: HorizontalPodAutoscaler
      name: blog-api-hpa
    patch: |
      - op: replace
        path: /spec/maxReplicas
        value: 8
  - target:
      kind: StatefulSet
      name: postgres
    patch: |
      - op: replace
        path: /spec/template/spec/containers/0/resources/requests/memory
        value: 2Gi
      - op: replace
        path: /spec/template/spec/containers/0/resources/limits/memory
        value: 4Gi

configMapGenerator:
  - name: blog-config
    literals:
      - environment=Production
```

### Resource Limits Reference (Phase 1)

| Service | CPU Req | CPU Limit | Mem Req | Mem Limit | Replicas |
|---|---|---|---|---|---|
| blog-api | 250m | 1000m | 256Mi | 512Mi | 2-4 (HPA) |
| blog-web | 100m | 500m | 128Mi | 256Mi | 2-3 (HPA) |
| blog-admin | 100m | 500m | 128Mi | 256Mi | 1-2 (HPA) |
| PostgreSQL | 500m | 2000m | 1Gi | 2Gi | 1 (StatefulSet) |
| Redis | 100m | 500m | 256Mi | 512Mi | 1 (StatefulSet) |

### Key Rules

1. **StatefulSet for databases** — PostgreSQL, Redis, MinIO must use StatefulSet with PVC
2. **Deployment for stateless apps** — blog-api, blog-web, blog-admin
3. **Secrets via SecretKeyRef** — Never hardcode credentials in manifests
4. **Health probes** — All Deployments must have readiness + liveness probes
5. **Resource limits** — Always set both requests and limits
6. **Namespace per environment** — `blog-dev`, `blog-staging`, `blog-prod`
