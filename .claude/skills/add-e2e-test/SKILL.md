# add-e2e-test

Write a Playwright 1.58 end-to-end test following the project's conventions for both blog-web and blog-admin applications.

## Arguments

- `app` (required) — Target app: `blog-web` or `blog-admin`
- `feature` (required) — Feature being tested (e.g., `post-reading`, `post-creation`, `login`, `comment`)
- `scenario` (optional) — Specific scenario description

## Instructions

You are writing Playwright E2E tests for the blog-platform. Tests verify complete user flows across the frontend applications.

### Test File Location

```
tests/e2e/
├── blog-web/
│   ├── post-reading.spec.ts
│   ├── post-search.spec.ts
│   ├── comment-flow.spec.ts
│   └── tag-navigation.spec.ts
├── blog-admin/
│   ├── post-management.spec.ts
│   ├── post-editor.spec.ts
│   ├── comment-moderation.spec.ts
│   └── user-management.spec.ts
├── fixtures/
│   ├── auth.fixture.ts
│   └── test-data.fixture.ts
├── playwright.config.ts
└── playwright.staging.config.ts
```

### Playwright Configuration

```typescript
// tests/e2e/playwright.config.ts
import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: '.',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: [['html'], ['github']],
  use: {
    baseURL: process.env.BASE_URL || 'http://localhost:3000',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
    { name: 'firefox', use: { ...devices['Desktop Firefox'] } },
    { name: 'mobile', use: { ...devices['iPhone 14'] } },
  ],
  webServer: [
    {
      command: 'nx serve blog-web',
      port: 3000,
      reuseExistingServer: !process.env.CI,
    },
    {
      command: 'nx serve blog-admin',
      port: 3001,
      reuseExistingServer: !process.env.CI,
    },
  ],
});
```

### Auth Fixture

```typescript
// tests/e2e/fixtures/auth.fixture.ts
import { test as base, Page } from '@playwright/test';

type AuthFixture = {
  adminPage: Page;
  editorPage: Page;
  authorPage: Page;
  readerPage: Page;
};

export const test = base.extend<AuthFixture>({
  adminPage: async ({ browser }, use) => {
    const context = await browser.newContext({
      storageState: 'tests/e2e/.auth/admin.json',
    });
    const page = await context.newPage();
    await use(page);
    await context.close();
  },
  editorPage: async ({ browser }, use) => {
    const context = await browser.newContext({
      storageState: 'tests/e2e/.auth/editor.json',
    });
    const page = await context.newPage();
    await use(page);
    await context.close();
  },
  // ... similar for author, reader
});

export { expect } from '@playwright/test';
```

### Blog-Web Test Example

```typescript
// tests/e2e/blog-web/post-reading.spec.ts
import { test, expect } from '@playwright/test';

test.describe('Post Reading', () => {
  test('reader can view published post list', async ({ page }) => {
    await page.goto('/');

    // Verify post list loads
    const posts = page.locator('article');
    await expect(posts.first()).toBeVisible();
    await expect(page.getByText('min read')).toBeVisible();
  });

  test('reader can navigate to post detail via slug', async ({ page }) => {
    await page.goto('/');

    // Click first post
    const firstPost = page.locator('article').first();
    const title = await firstPost.locator('h2').textContent();
    await firstPost.locator('a').first().click();

    // Verify post detail page
    await expect(page.locator('h1')).toHaveText(title!);
    await expect(page.locator('article')).toBeVisible();
  });

  test('post detail shows author info and reading time', async ({ page }) => {
    await page.goto('/posts/test-post-slug');

    await expect(page.getByText('min read')).toBeVisible();
    await expect(page.locator('[data-testid="author-name"]')).toBeVisible();
  });

  test('post renders Tiptap content correctly', async ({ page }) => {
    await page.goto('/posts/test-post-slug');

    // Verify prose content is rendered
    const content = page.locator('.prose');
    await expect(content).toBeVisible();
    await expect(content.locator('h2').first()).toBeVisible();
    await expect(content.locator('p').first()).toBeVisible();
  });

  test.describe('SEO', () => {
    test('post has correct meta tags', async ({ page }) => {
      await page.goto('/posts/test-post-slug');

      const title = await page.title();
      expect(title).toBeTruthy();

      const ogTitle = await page.locator('meta[property="og:title"]').getAttribute('content');
      expect(ogTitle).toBeTruthy();
    });
  });
});
```

### Blog-Admin Test Example

```typescript
// tests/e2e/blog-admin/post-management.spec.ts
import { test, expect } from '../fixtures/auth.fixture';

test.describe('Post Management', () => {
  test('author can create a new draft post', async ({ authorPage: page }) => {
    await page.goto('/dashboard/posts/new');

    // Fill in post form
    await page.getByLabel('Title').fill('My New Post');
    await page.getByLabel('Excerpt').fill('A brief description');

    // Type in Tiptap editor
    const editor = page.locator('.ProseMirror');
    await editor.click();
    await editor.type('This is the post content');

    // Save as draft
    await page.getByRole('button', { name: 'Save Draft' }).click();

    // Verify redirect to post list
    await expect(page).toHaveURL(/\/dashboard\/posts/);
    await expect(page.getByText('My New Post')).toBeVisible();
  });

  test('editor can publish a draft post', async ({ editorPage: page }) => {
    await page.goto('/dashboard/posts');

    // Find draft post and click publish
    const row = page.getByRole('row').filter({ hasText: 'Draft' }).first();
    await row.getByRole('button', { name: 'Publish' }).click();

    // Confirm dialog
    await page.getByRole('button', { name: 'Confirm' }).click();

    // Verify status changed
    await expect(row.getByText('Published')).toBeVisible();
  });

  test('author cannot publish posts (forbidden)', async ({ authorPage: page }) => {
    await page.goto('/dashboard/posts');

    // Publish button should not be visible for authors
    const row = page.getByRole('row').filter({ hasText: 'Draft' }).first();
    await expect(row.getByRole('button', { name: 'Publish' })).not.toBeVisible();
  });
});
```

### Running E2E Tests

```bash
# Run all E2E tests
npx playwright test

# Run specific app tests
npx playwright test tests/e2e/blog-web/
npx playwright test tests/e2e/blog-admin/

# Run with UI mode
npx playwright test --ui

# Run smoke tests only (for staging deploy)
npx playwright test --grep @smoke

# Run against staging
BASE_URL=https://staging.blog-platform.dev npx playwright test
```

### Test Tagging Convention

```typescript
test('critical flow @smoke', async ({ page }) => { ... });
test('edge case @regression', async ({ page }) => { ... });
```

### Key Rules

1. **Use data-testid for selectors** — Prefer `data-testid` over CSS classes for stability
2. **Auth via storage state** — Pre-authenticate using saved browser state, not UI login per test
3. **Parallel by default** — Tests should be independent and run in parallel
4. **Visual regression** — Use `expect(page).toHaveScreenshot()` for critical UI components
5. **Mobile viewport** — Include mobile tests for blog-web (reader app)
6. **Smoke tag** — Tag critical path tests with `@smoke` for staging deploy verification
