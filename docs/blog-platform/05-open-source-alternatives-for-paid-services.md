# Open-Source Alternatives for Paid Services

Bảng dưới đây tổng hợp các dịch vụ trả phí được đề cập trong tài liệu và phương án thay thế **free / open source** tương ứng. Ưu tiên self-hosted để kiểm soát dữ liệu và chi phí.

| Paid Service | Mục đích | Open Source Alternative | License | Ghi chú |
|---|---|---|---|---|
| **SendGrid** | Transactional email | **Postal** | MIT | Self-hosted, SMTP + HTTP API, hỗ trợ inbound/outbound. Cần server riêng với IP reputation tốt |
| **SendGrid** | Transactional email | **useSend** | MIT | Wrapper trên AWS SES, self-hosted dashboard + analytics |
| **HashiCorp Vault** | Secrets management | **OpenBao** | MPL 2.0 | Fork từ Vault bởi Linux Foundation (v2.5.0). API-compatible với Vault. Namespaces miễn phí (Vault cần Enterprise) |
| **HashiCorp Vault** | Secrets management | **Infisical** | MIT Expat | UI hiện đại, CLI tốt, rotation + scanning built-in |
| **TestRail** | Test case management | **Kiwi TCMS** | GPLv2 | Active development (v15.0+), manual + automated test tracking |
| **TestRail** | Test case management | **SquashTM** | LGPLv3 | Popular nhất trong OSS test management, suite đầy đủ |
| **Elasticsearch** | Full-text search (Phase 3) | **Meilisearch** | MIT | Viết bằng Rust, nhanh, hỗ trợ tiếng Việt, typo tolerance. Phù hợp blog search |
| **Elasticsearch** | Full-text search (Phase 3) | **OpenSearch** | Apache 2.0 | Fork từ Elasticsearch bởi AWS → Linux Foundation. API-compatible, phù hợp large-scale |
| **Hotjar** | Heatmaps & session recording | **PostHog** | MIT | All-in-one: analytics + heatmaps + session replay + feature flags + A/B testing |
| **Hotjar** | Heatmaps & session recording | **Microsoft Clarity** | Free (not OSS) | Hoàn toàn miễn phí, không giới hạn sessions, heatmaps + rage click detection |
| **Stripe** | Payment / billing (Phase 3) | **Lago** | AGPLv3 | Usage-based billing, invoicing, self-hosted. Phù hợp cho subscription/paywall |
| **Stripe** | Payment gateway (Phase 3) | **Hyperswitch** | Apache 2.0 | Payment orchestration, multi-PSP routing. Dùng kèm Lago cho billing |
| **BigQuery** | Data warehouse | **ClickHouse** | Apache 2.0 | Columnar DB, cực nhanh cho analytical queries, self-hosted |
| **Nx Cloud** | Remote cache & DTE | **Nx self-hosted cache** | — | Tự host remote cache qua S3/MinIO. Không cần Nx Cloud cho team nhỏ |
| **Figma** (nếu cần OSS) | UI Design | **Penpot** | MPL 2.0 | Open source design tool, self-hosted, real-time collaboration |

**Lưu ý quan trọng:**

- Các tool đã **open source sẵn** trong stack hiện tại (không cần thay thế): MinIO, Metabase, GrowthBook, Redash, Grafana, Prometheus, Loki, Tempo, ClickHouse, Apache Kafka, dbt
- **Microsoft Clarity** tuy không phải open source nhưng hoàn toàn **miễn phí** và không giới hạn — phù hợp làm baseline analytics
- Khi chọn OSS alternative, cần đánh giá: **community activity**, **release frequency**, **breaking changes**, **migration path**
- Một số OSS có **open-core model** (core miễn phí, enterprise features trả phí) — kiểm tra kỹ feature matrix trước khi commit

---
