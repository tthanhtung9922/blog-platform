# Coding Progress — Blog Platform

**Cập nhật lần cuối**: 2026-03-15

## Tiến độ tổng quan

| Phase | Tên | Trạng thái | Plans | Tài liệu |
|-------|-----|-----------|-------|---------|
| 1 | Monorepo Foundation + Domain Layer | ✅ Hoàn thành | 4/4 | [phase-01/](./phase-01/_index.md) |
| 2 | Infrastructure + Application Pipeline | ⏳ Chưa bắt đầu | 0/TBD | — |
| 3 | Authentication + RBAC + Tags | ⏳ Chưa bắt đầu | 0/TBD | — |
| 4 | Post Backend API | ⏳ Chưa bắt đầu | 0/TBD | — |
| 5 | Public Blog Frontend | ⏳ Chưa bắt đầu | 0/TBD | — |
| 6 | Social Features | ⏳ Chưa bắt đầu | 0/TBD | — |
| 7 | Admin Features + Media Upload | ⏳ Chưa bắt đầu | 0/TBD | — |
| 8 | Search | ⏳ Chưa bắt đầu | 0/TBD | — |
| 9 | CI/CD + Kubernetes | ⏳ Chưa bắt đầu | 0/TBD | — |
| 10 | Observability + Rate Limiting | ⏳ Chưa bắt đầu | 0/TBD | — |

---

## Phase 1 — Tóm tắt nhanh

**Hoàn thành**: 2026-03-15 (~24 phút, 56 files)

Kết quả chính:
- Nx 22 monorepo với 9 projects đăng ký trong project graph
- `Blog.Domain`: 4 aggregates (Post, Comment, User, Tag), 4 value objects (Slug, Email, ReadingTime, TagReference), 12 domain events, 4 repository interfaces, 21 unit tests
- `Blog.Infrastructure`: BlogDbContext + 6 EF Core configurations + 2 migrations (unaccent extension + 7-table schema)
- `Blog.API`: Minimal startup với MigrateAsync + /healthz
- `Blog.ArchTests`: 9 architecture tests — layer boundaries + domain model integrity
- Docker Compose: PostgreSQL 18 + Redis 8 + MinIO

**Tests**: 9 arch tests ✅ + 21 unit tests ✅

[→ Xem chi tiết Phase 1](./phase-01/_index.md)
