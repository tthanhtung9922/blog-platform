---
description: >
  Apply these rules whenever writing, editing, or reviewing frontend code in
  blog-web, blog-admin, shared-ui, or shared-contracts. Triggers on: React
  components, Next.js pages/routes, Tailwind CSS, Tiptap editor/renderer,
  CASL permissions, TypeScript types, API client code.
---

# Frontend Rules

## MUST

- **Two separate Next.js 16.1 apps** with distinct purposes:
  - `blog-web` — Public reader. Optimized for SSG/ISR, SEO, Core Web Vitals. No admin features.
  - `blog-admin` — CMS dashboard. Interactive, Tiptap v3 editor, CASL-gated routes and UI.
- **Tailwind CSS v4 uses CSS-first configuration** — all theme customization goes in `src/styles/globals.css` using `@theme` and `@plugin` directives.
  ```css
  /* globals.css — Tailwind v4 */
  @import "tailwindcss";
  @theme {
    --color-primary: #3b82f6;
  }
  ```
  ```
  tailwind.config.ts  // NEVER — this file must not exist
  ```
- **Tiptap v3 content is ProseMirror JSON**, stored in `body_json` (JSONB). Two rendering approaches:
  - **Option A (HTML):** Sanitize `body_html` with DOMPurify, render via `dangerouslySetInnerHTML`
  - **Option B (Recommended):** Use `@tiptap/react` `EditorContent` with `useEditor({ editable: false, content: jsonContent })`
- **Sanitize all Tiptap HTML output with DOMPurify** before rendering — prevents XSS from user-generated content.
- **TypeScript types are generated from the OpenAPI spec** via `scripts/gen-types.sh` → `libs/shared-contracts/src/api.types.ts`. Never hand-write types that exist in the API contract.
- **CASL permission checks** in blog-admin use `PermissionGate` component for conditional UI and `usePermission` hook for programmatic checks. CASL version must be >= 6.8.0.
- **Permission definitions** (`ability.ts`, `roles.ts`) in blog-admin must match the backend permission matrix in `shared-contracts/permissions.ts`.
- **App Router structure** uses route groups for auth boundaries:
  - `(public)/` — no auth required (blog-web)
  - `(auth)/` — login/register pages
  - `(dashboard)/` — protected, requires authentication (blog-admin)

## SHOULD

- Use `shadcn/ui` for base UI components — installed in each app's `components/ui/` directory.
- Share reusable components across apps via `libs/shared-ui/`.
- Use Next.js `generateMetadata` and JSON-LD structured data for SEO in blog-web.
- Target LCP <= 2.5s for blog-web pages (Core Web Vitals).

## NEVER

- Never create a `tailwind.config.ts` or `tailwind.config.js` file — Tailwind v4 is CSS-first.
- Never render Tiptap content as Markdown or MDX — it is ProseMirror JSON.
  ```tsx
  // NEVER
  <ReactMarkdown>{post.content}</ReactMarkdown>

  // CORRECT
  const editor = useEditor({ editable: false, content: post.bodyJson, extensions: [...] });
  return <EditorContent editor={editor} />;
  ```
- Never use `dangerouslySetInnerHTML` with unsanitized Tiptap HTML:
  ```tsx
  // NEVER
  <div dangerouslySetInnerHTML={{ __html: post.bodyHtml }} />

  // CORRECT
  import DOMPurify from 'dompurify';
  <div dangerouslySetInnerHTML={{ __html: DOMPurify.sanitize(post.bodyHtml) }} />
  ```
- Never rely solely on frontend CASL checks for security — they are UX helpers. The API enforces the real security boundary.
- Never hand-write API response types that should come from `shared-contracts`.
