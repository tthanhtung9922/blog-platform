# add-frontend-page

Create a Next.js 16.1 page in either blog-web (public reader with SSG/ISR) or blog-admin (CMS dashboard) with proper routing, data fetching, Tailwind CSS v4 styling, and authorization.

## Arguments

- `app` (required) — Target app: `blog-web` or `blog-admin`
- `route` (required) — The page route path (e.g., `/posts/[slug]`, `/dashboard/posts`, `/tags/[slug]`)
- `title` (required) — Page title for metadata
- `rendering` (optional) — `ssg`, `isr`, `ssr`, `client` (defaults: `isr` for blog-web, `client` for blog-admin)
- `revalidate` (optional) — ISR revalidation interval in seconds (default: 60 for blog-web)
- `protected` (optional) — Whether the page requires authentication (default: true for blog-admin, false for blog-web)

## Instructions

You are creating a Next.js 16.1 page using the App Router. The project has TWO separate Next.js apps with different purposes and patterns.

### App Differences

| Aspect | blog-web | blog-admin |
|---|---|---|
| Purpose | Public reader | CMS dashboard |
| Rendering | SSG/ISR (performance-first) | Client-side (interactive) |
| Auth | Optional (for comments/likes) | Required (all pages) |
| Routing | `/posts/[slug]`, `/tags/[slug]` | `/dashboard/*` |
| Location | `apps/blog-web/` | `apps/blog-admin/` |

### File Structure (App Router)

```
apps/{app}/src/app/
├── layout.tsx          ← Root layout
├── page.tsx            ← Home page
├── posts/
│   ├── page.tsx        ← Post list
│   └── [slug]/
│       └── page.tsx    ← Post detail
├── tags/
│   └── [slug]/
│       └── page.tsx    ← Posts by tag
└── dashboard/          ← (blog-admin only)
    ├── layout.tsx      ← Dashboard layout with sidebar
    ├── page.tsx        ← Dashboard home
    ├── posts/
    │   ├── page.tsx    ← Manage posts
    │   ├── new/
    │   │   └── page.tsx ← Create post (Tiptap editor)
    │   └── [id]/
    │       └── edit/
    │           └── page.tsx ← Edit post
    └── users/
        └── page.tsx    ← Manage users (Admin only)
```

### Pattern: SSG/ISR Page (blog-web)

```tsx
// apps/blog-web/src/app/posts/[slug]/page.tsx
import { Metadata } from 'next';
import { notFound } from 'next/navigation';

interface Props {
  params: Promise<{ slug: string }>;
}

// ISR: revalidate every 60 seconds
export const revalidate = 60;

// Generate static paths for published posts
export async function generateStaticParams() {
  const res = await fetch(`${process.env.API_URL}/api/v1/posts?pageSize=100`);
  const data = await res.json();
  return data.items.map((post: { slug: string }) => ({ slug: post.slug }));
}

// Dynamic metadata for SEO
export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { slug } = await params;
  const post = await getPost(slug);
  if (!post) return { title: 'Not Found' };

  return {
    title: post.title,
    description: post.excerpt,
    openGraph: {
      title: post.title,
      description: post.excerpt ?? undefined,
      images: post.coverImageUrl ? [post.coverImageUrl] : [],
    },
  };
}

async function getPost(slug: string) {
  const res = await fetch(`${process.env.API_URL}/api/v1/posts/${slug}`, {
    next: { revalidate: 60 },
  });
  if (!res.ok) return null;
  return res.json();
}

export default async function PostPage({ params }: Props) {
  const { slug } = await params;
  const post = await getPost(slug);

  if (!post) notFound();

  return (
    <article className="mx-auto max-w-3xl px-4 py-8">
      <header className="mb-8">
        <h1 className="text-4xl font-bold tracking-tight text-gray-900">
          {post.title}
        </h1>
        <div className="mt-4 flex items-center gap-4 text-sm text-gray-500">
          <span>{post.authorDisplayName}</span>
          <time dateTime={post.publishedAt}>
            {new Date(post.publishedAt).toLocaleDateString('vi-VN')}
          </time>
          <span>{post.readingTimeMinutes} min read</span>
        </div>
      </header>

      {/* Tiptap content rendered in read-only mode — see render-tiptap-content skill */}
      <div className="prose prose-lg max-w-none">
        {/* Use TiptapRenderer component here */}
      </div>
    </article>
  );
}
```

### Pattern: Dashboard Page (blog-admin)

```tsx
// apps/blog-admin/src/app/dashboard/posts/page.tsx
'use client';

import { useEffect, useState } from 'react';
import { ProtectedRoute } from '@/components/auth/ProtectedRoute';
import { PermissionGate } from '@/components/auth/PermissionGate';

export default function ManagePostsPage() {
  return (
    <ProtectedRoute action="read" subject="Post">
      <div className="space-y-6">
        <div className="flex items-center justify-between">
          <h1 className="text-2xl font-semibold">Posts</h1>
          <PermissionGate action="create" subject="Post">
            <a
              href="/dashboard/posts/new"
              className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
            >
              New Post
            </a>
          </PermissionGate>
        </div>

        {/* Post list with DataTable */}
        <PostsDataTable />
      </div>
    </ProtectedRoute>
  );
}
```

### Tailwind CSS v4 — CSS-First Configuration

Tailwind CSS v4 uses CSS-first configuration. There is NO `tailwind.config.ts` file.

```css
/* apps/{app}/src/app/globals.css */
@import "tailwindcss";

/* Custom theme via CSS variables */
@theme {
  --color-primary: #2563eb;
  --color-primary-hover: #1d4ed8;
  --font-sans: 'Inter', system-ui, sans-serif;
  --font-mono: 'JetBrains Mono', monospace;
}
```

### Data Fetching Patterns

**Server Components (blog-web — preferred):**
```tsx
// Direct fetch in server component — no useEffect needed
async function getPostList(page: number) {
  const res = await fetch(`${process.env.API_URL}/api/v1/posts?page=${page}&pageSize=10`, {
    next: { revalidate: 300 },  // 5 min ISR
  });
  if (!res.ok) throw new Error('Failed to fetch posts');
  return res.json();
}
```

**Client Components (blog-admin):**
```tsx
'use client';

import useSWR from 'swr';

const fetcher = (url: string) =>
  fetch(url, {
    headers: { Authorization: `Bearer ${getAccessToken()}` },
  }).then(r => r.json());

function usePostList(page: number) {
  const { data, error, isLoading } = useSWR(
    `/api/v1/posts?page=${page}&pageSize=20`,
    fetcher
  );
  return { posts: data, error, isLoading };
}
```

### Authentication Integration

**blog-web (optional auth):**
```tsx
// NextAuth v5 session check
import { auth } from '@/lib/auth';

export default async function Layout({ children }: { children: React.ReactNode }) {
  const session = await auth();
  // session is null for anonymous users — that's OK
  return <SessionProvider session={session}>{children}</SessionProvider>;
}
```

**blog-admin (required auth):**
```tsx
// All dashboard routes require authentication
// apps/blog-admin/src/app/dashboard/layout.tsx
import { auth } from '@/lib/auth';
import { redirect } from 'next/navigation';

export default async function DashboardLayout({ children }: { children: React.ReactNode }) {
  const session = await auth();
  if (!session) redirect('/login');

  return (
    <div className="flex min-h-screen">
      <Sidebar />
      <main className="flex-1 p-6">{children}</main>
    </div>
  );
}
```

### Shared UI Components

Use shadcn/ui components from the shared library:

```tsx
import { Button } from '@blog-platform/shared-ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@blog-platform/shared-ui/card';
import { DataTable } from '@blog-platform/shared-ui/data-table';
```

### SEO Best Practices (blog-web only)

- Always implement `generateMetadata()` for dynamic pages
- Include Open Graph tags (`title`, `description`, `images`)
- Use semantic HTML (`<article>`, `<header>`, `<main>`, `<nav>`)
- Add structured data (JSON-LD) for blog posts
- Set canonical URLs
- Vietnamese locale: use `vi-VN` for date formatting
