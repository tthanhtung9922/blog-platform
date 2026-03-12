# Team Structure

## 1.1 Overview

Ở cấp độ **Production với kế hoạch dài hạn**, team cần được tổ chức theo mô hình **cross-functional** — mỗi nhóm có đủ năng lực độc lập để deliver tính năng từ đầu đến cuối. Cấu trúc dưới đây phù hợp cho scale từ **15 đến 40+ người**, được chia thành các giai đoạn phát triển rõ ràng.

```
blog-platform/
├── 🎯  Product & Strategy
│   ├── Head of Product
│   ├── Product Manager (×2)
│   └── Business Analyst (×1)
│
├── 🎨  Design
│   ├── Lead UX Designer
│   ├── UI Designer (×2)
│   └── UX Researcher (×1)
│
├── 💻  Engineering
│   ├── Engineering Manager
│   ├── Frontend (×4)
│   ├── Backend (×4)
│   └── Full-stack / Feature Lead (×2)
│
├── 🚀  Platform & DevOps
│   ├── Platform Engineer (×2)
│   └── Site Reliability Engineer / SRE (×1)
│
├── 🧪  Quality Assurance
│   ├── QA Lead
│   ├── QA Engineer — Manual (×1)
│   └── QA Engineer — Automation (×2)
│
├── 🔐  Security & Compliance
│   ├── Security Engineer (×1)
│   └── Compliance Officer (×1, part-time or shared)
│
├── 📊  Data & Analytics
│   ├── Data Engineer (×1)
│   └── Analytics Engineer (×1)
│
└── 🌐  Go-to-Market & Operations
    ├── Technical Writer (×1)
    ├── Customer Success / Community Manager (×1)
    └── SEO / Growth Engineer (×1)
```

---

## 1.2 Product & Strategy

### Head of Product

- Định hướng vision sản phẩm dài hạn (1–3 năm)
- Cân bằng giữa business goals, user needs và technical constraints
- Làm việc trực tiếp với CTO / CEO để align strategy

### Product Manager (×2)

- **PM 1 — Reader Experience:** Tối ưu hóa trải nghiệm đọc, SEO, discovery, comment & reaction
- **PM 2 — Creator & CMS:** Quản lý workflow viết bài, editor experience, phân quyền, analytics cho author
- Viết PRD chi tiết, acceptance criteria, và quản lý backlog theo từng domain
- Tổ chức sprint planning, review với Engineering và Design

### Business Analyst (×1)

- Phân tích metrics, user feedback, và competitive landscape
- Hỗ trợ PM viết requirement và mapping business processes
- Theo dõi KPI: DAU/MAU, retention, churn, content performance

---

## 1.3 Design

### Lead UX Designer

- Xây dựng và duy trì **Design System** (tokens, components, patterns)
- Đảm bảo consistency giữa `blog-web` và `blog-admin`
- Mentor cho UI Designers, review design trước khi handoff

### UI Designer (×2)

- **Designer 1 — Public Web:** Giao diện đọc bài, responsive, dark mode, typography
- **Designer 2 — Admin CMS:** Editor interface, dashboard, table/form patterns
- Thiết kế trong Figma, tạo interactive prototype, maintain component library

### UX Researcher (×1)

- Thực hiện user interviews, usability testing định kỳ
- Phân tích heatmaps (PostHog — open source / Microsoft Clarity — free), session recordings
- Cung cấp insight để PM và Designer ra quyết định dựa trên data

---

## 1.4 Engineering — Frontend

### Engineering Manager (EM)

- Quản lý toàn bộ engineering team (FE + BE)
- 1-on-1 định kỳ, career development, performance review
- Technical direction, architecture decisions, hiring

### Senior Frontend Engineer — Tech Lead (×1)

- Kiến trúc tổng thể cho `blog-web` và `blog-admin`
- Duy trì `shared-ui` component library
- Code review, mentoring junior/mid engineers
- **Stack:** Next.js 16.1 · React 19 · TypeScript 6.0 · Tailwind v4

### Frontend Engineer (×3)

- **FE 1 — Public Blog:** SSG/ISR performance, SEO, Core Web Vitals, reading experience
- **FE 2 — Admin CMS:** Rich text editor (Tiptap v3 stable), media upload, dashboard UI
- **FE 3 — Auth & RBAC:** NextAuth v5, role-based routing, permission guards, session management

---

## 1.5 Engineering — Backend

### Senior Backend Engineer — Tech Lead (×1)

- Kiến trúc Clean Architecture + DDD cho `blog-api`
- Design domain model (Post, Comment, User aggregates)
- Định nghĩa API contracts (OpenAPI 3.1), review Pull Requests
- **Stack:** ASP.NET Core 10 · EF Core 10 · PostgreSQL 18 · Redis 8

### Backend Engineer (×3)

- **BE 1 — Content Domain:** Post CRUD, draft/publish workflow, versioning, slug generation
- **BE 2 — User & Auth Domain:** ASP.NET Identity, JWT/Refresh Token, RBAC policies, OAuth2 (Google, GitHub)
- **BE 3 — Interaction Domain:** Comment system, Reaction (like/bookmark), Notification, Search (PostgreSQL Full-text Search — Phase 1)

### Full-stack / Feature Lead (×2)

- Đảm nhận end-to-end feature delivery (FE + BE + DB migration)
- Phù hợp cho tính năng cross-cutting như: Search, Analytics Dashboard, Email Notification

---

## 1.6 Platform & DevOps

### Platform Engineer (×2)

- **Platform 1 — Infrastructure:** Kubernetes cluster management, Helm charts, auto-scaling, resource optimization
- **Platform 2 — CI/CD & Tooling:** GitHub Actions pipelines, Docker image builds, registry (GHCR), secrets management (OpenBao — open source Vault fork by Linux Foundation / Sealed Secrets)
- Duy trì `deploy/docker/` và `deploy/k8s/` manifests
- Monitoring stack: Prometheus + Grafana + Loki + Tempo

### Site Reliability Engineer — SRE (×1)

- Định nghĩa và theo dõi SLO/SLA (availability, latency, error rate)
- On-call rotation, incident response và post-mortem
- Capacity planning, database performance tuning (PostgreSQL 18)
- Quản lý CDN (Cloudflare), caching strategy (Redis 8)

---

## 1.7 Quality Assurance

### QA Lead

- Xây dựng QA strategy, test plan và risk-based testing approach
- Coordinate testing với Product và Engineering trước mỗi release

### QA Engineer — Manual (×1)

- Exploratory testing, regression testing, UX validation
- Test case management (Kiwi TCMS — open source / Notion)
- UAT coordination với stakeholders

### QA Engineer — Automation (×2)

- **Automation 1 — E2E:** Playwright 1.58 · Test toàn bộ user flows (reader, author, admin)
- **Automation 2 — API & Integration:** xUnit 3 · Testcontainers · Contract testing (Pact)
- Maintain test suite, CI integration, flaky test detection

---

## 1.8 Security & Compliance

### Security Engineer (×1)

- Thực hiện threat modeling và penetration testing định kỳ (ít nhất 2 lần/năm)
- Quản lý dependency vulnerabilities (Dependabot, OWASP Dependency Check)
- Đảm bảo authentication security: JWT best practices, token rotation, rate limiting
- Security review cho tính năng liên quan đến auth và user data

### Compliance Officer (×1, part-time hoặc shared)

- Đảm bảo tuân thủ **GDPR / PDPA** (Personal Data Protection Act — Vietnam)
- Quản lý Privacy Policy, Terms of Service, Cookie consent
- Data retention policy, right to erasure, data export

---

## 1.9 Data & Analytics

### Data Engineer (×1)

- Xây dựng và duy trì data pipeline (event tracking → data warehouse)
- Thiết kế event schema cho: page_view, post_read, comment_created, reaction_added
- Stack gợi ý: ClickHouse (open source) + dbt + Apache Kafka (khi scale)
  - *Lưu ý:* ClickHouse thay thế BigQuery (GCP paid) — self-hosted, hiệu suất cao cho analytical queries

### Analytics Engineer (×1)

- Xây dựng dashboards cho từng stakeholder:
  - **Author Dashboard:** views, read time, engagement rate, top posts
  - **Admin Dashboard:** platform-wide metrics, content moderation queue
  - **Growth Dashboard:** acquisition, retention, funnel analysis
- Tool: Metabase / Grafana / Redash

---

## 1.10 Go-to-Market & Operations

### Technical Writer (×1)

- Viết và duy trì tài liệu API (Swagger / Scalar UI)
- Documentation cho Admin CMS: hướng dẫn sử dụng cho Author, Editor
- Changelog, release notes, onboarding guides

### Customer Success / Community Manager (×1)

- Hỗ trợ users (Authors, Editors) khi gặp vấn đề
- Thu thập feedback, tổng hợp báo cáo cho PM
- Quản lý community channel (Discord / Slack cho contributors)

### SEO / Growth Engineer (×1)

- Technical SEO: structured data (JSON-LD), sitemap, robots.txt, Core Web Vitals
- Tối ưu hóa Open Graph, Twitter Card, canonical URLs
- A/B testing (GrowthBook), conversion funnel optimization

---

## 1.11 Team Size by Phase

| Phase | Timeline | Team Size | Focus |
|---|---|---|---|
| **Production Launch** | Month 0–6 | 12–16 | Core features stable, CI/CD solid |
| **Growth** | Month 6–18 | 18–25 | Scale, analytics, SEO, performance |
| **Scale** | Month 18–36 | 28–40 | Multi-tenant, microservices nếu cần |
| **Enterprise** | 36+ | 40+ | Compliance, SLA, enterprise features |

---
