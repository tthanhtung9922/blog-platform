# Long-term Roadmap

## Phase 1 — Production Launch (Month 0–6)

- [ ] Core CRUD: Post, Comment, Reaction
- [ ] RBAC: Admin / Editor / Author / Reader
- [ ] Auth: Email/password + OAuth (Google, GitHub)
- [ ] Public blog với SSG/ISR, SEO cơ bản
- [ ] Admin CMS với rich text editor (Tiptap v3 stable)
- [ ] Docker + K8S deployment, GitHub Actions CI/CD
- [ ] Monitoring: Prometheus + Grafana + Loki

## Phase 2 — Growth (Month 6–18)

- [ ] Full-text search với PostgreSQL FTS (built-in, không cần Elasticsearch)
- [ ] Email notification system (Postal — open source self-hosted / SendGrid — paid fallback)
- [ ] Newsletter subscription
- [ ] Analytics dashboard cho Author
- [ ] Core Web Vitals optimization (target: LCP < 2.5s)
- [ ] Dark mode, i18n (Tiếng Việt / English)
- [ ] Social sharing, OpenGraph optimization
- [ ] A/B testing framework (GrowthBook)

## Phase 3 — Scale (Month 18–36)

- [ ] Multi-author publication / organization support
- [ ] Paid membership / paywall (Lago — open source billing / Stripe — paid alternative)
- [ ] AI-powered features: auto-tag, reading recommendations, SEO suggestions
- [ ] Advanced media management (video embedding, image CDN)
- [ ] API rate limiting tiers
- [ ] Webhook system cho third-party integrations
- [ ] Migrate search từ PostgreSQL FTS sang Meilisearch (open source, MIT license — xem ADR-009)

## Phase 4 — Enterprise (Month 36+)

- [ ] Multi-tenant architecture (nếu SaaS)
- [ ] SSO / SAML integration
- [ ] Custom domain per publication
- [ ] SOC 2 / ISO 27001 compliance
- [ ] SLA 99.9% uptime guarantee
- [ ] Dedicated infrastructure per enterprise client
- [ ] Advanced analytics: cohort analysis, LTV, churn prediction

---
