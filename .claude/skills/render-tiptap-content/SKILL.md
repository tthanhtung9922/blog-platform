# render-tiptap-content

Render Tiptap v3 ProseMirror JSON content in read-only mode in the blog-web public reader, with DOMPurify sanitization for security.

## Arguments

- `mode` (optional) — `read-only` (default) or `editor` (for blog-admin)
- `features` (optional) — Comma-separated Tiptap extensions to include (e.g., `code-block,image,table,link`)

## Instructions

You are implementing Tiptap v3 content rendering for the blog-platform. The blog stores content as ProseMirror JSON (`body_json` in the `post_contents` table). There are two rendering contexts:

1. **blog-web** (public reader) — Read-only rendering of published posts
2. **blog-admin** (CMS) — Interactive editor for creating/editing posts

### Read-Only Rendering (blog-web)

Use `@tiptap/react` `EditorContent` in read-only mode. Do NOT use `dangerouslySetInnerHTML` with the pre-rendered `body_html` directly — always sanitize.

```tsx
// apps/blog-web/src/components/content/TiptapRenderer.tsx
'use client';

import { useEditor, EditorContent } from '@tiptap/react';
import StarterKit from '@tiptap/starter-kit';
import Image from '@tiptap/extension-image';
import Link from '@tiptap/extension-link';
import CodeBlockLowlight from '@tiptap/extension-code-block-lowlight';
import { common, createLowlight } from 'lowlight';

const lowlight = createLowlight(common);

interface TiptapRendererProps {
  content: object;  // ProseMirror JSON from API
  className?: string;
}

export function TiptapRenderer({ content, className }: TiptapRendererProps) {
  const editor = useEditor({
    extensions: [
      StarterKit.configure({
        codeBlock: false,  // Use CodeBlockLowlight instead
      }),
      Image.configure({
        HTMLAttributes: {
          class: 'rounded-lg max-w-full h-auto',
          loading: 'lazy',
        },
      }),
      Link.configure({
        openOnClick: true,
        HTMLAttributes: {
          class: 'text-blue-600 hover:text-blue-800 underline',
          rel: 'noopener noreferrer',
          target: '_blank',
        },
      }),
      CodeBlockLowlight.configure({ lowlight }),
    ],
    content,
    editable: false,  // READ-ONLY mode
    editorProps: {
      attributes: {
        class: className ?? 'prose prose-lg max-w-none',
      },
    },
  });

  if (!editor) return null;

  return <EditorContent editor={editor} />;
}
```

### Alternative: HTML Rendering with DOMPurify

If using the pre-rendered `body_html` field for performance (SSG/ISR), ALWAYS sanitize:

```tsx
// apps/blog-web/src/components/content/SafeHtmlRenderer.tsx
'use client';

import DOMPurify from 'dompurify';

interface SafeHtmlRendererProps {
  html: string;  // body_html from API
  className?: string;
}

export function SafeHtmlRenderer({ html, className }: SafeHtmlRendererProps) {
  const sanitized = DOMPurify.sanitize(html, {
    ALLOWED_TAGS: [
      'h1', 'h2', 'h3', 'h4', 'h5', 'h6',
      'p', 'br', 'hr',
      'ul', 'ol', 'li',
      'blockquote',
      'pre', 'code',
      'a', 'strong', 'em', 'u', 's', 'sub', 'sup',
      'img', 'figure', 'figcaption',
      'table', 'thead', 'tbody', 'tr', 'th', 'td',
      'div', 'span',
    ],
    ALLOWED_ATTR: [
      'href', 'target', 'rel',
      'src', 'alt', 'width', 'height', 'loading',
      'class', 'id',
      'data-language',  // For code blocks
    ],
    ADD_ATTR: ['target'],
    ALLOW_DATA_ATTR: false,
  });

  return (
    <div
      className={className ?? 'prose prose-lg max-w-none'}
      dangerouslySetInnerHTML={{ __html: sanitized }}
    />
  );
}
```

### Interactive Editor (blog-admin)

```tsx
// apps/blog-admin/src/components/editor/TiptapEditor.tsx
'use client';

import { useEditor, EditorContent, BubbleMenu } from '@tiptap/react';
import StarterKit from '@tiptap/starter-kit';
import Image from '@tiptap/extension-image';
import Link from '@tiptap/extension-link';
import Placeholder from '@tiptap/extension-placeholder';
import CodeBlockLowlight from '@tiptap/extension-code-block-lowlight';
import { common, createLowlight } from 'lowlight';

const lowlight = createLowlight(common);

interface TiptapEditorProps {
  content?: object;
  onChange: (json: object) => void;
}

export function TiptapEditor({ content, onChange }: TiptapEditorProps) {
  const editor = useEditor({
    extensions: [
      StarterKit.configure({ codeBlock: false }),
      Image.configure({
        HTMLAttributes: { class: 'rounded-lg max-w-full h-auto' },
      }),
      Link.configure({
        openOnClick: false,
        HTMLAttributes: { class: 'text-blue-600 underline' },
      }),
      Placeholder.configure({
        placeholder: 'Start writing your post...',
      }),
      CodeBlockLowlight.configure({ lowlight }),
    ],
    content,
    editable: true,
    onUpdate: ({ editor }) => {
      onChange(editor.getJSON());
    },
  });

  if (!editor) return null;

  return (
    <div className="rounded-lg border border-gray-200">
      {/* Toolbar */}
      <EditorToolbar editor={editor} />

      {/* Bubble menu for inline formatting */}
      <BubbleMenu editor={editor} tippyOptions={{ duration: 100 }}>
        <div className="flex gap-1 rounded-lg bg-white p-1 shadow-lg border">
          <ToolbarButton
            onClick={() => editor.chain().focus().toggleBold().run()}
            active={editor.isActive('bold')}
          >
            B
          </ToolbarButton>
          <ToolbarButton
            onClick={() => editor.chain().focus().toggleItalic().run()}
            active={editor.isActive('italic')}
          >
            I
          </ToolbarButton>
          <ToolbarButton
            onClick={() => editor.chain().focus().toggleLink({ href: '' }).run()}
            active={editor.isActive('link')}
          >
            Link
          </ToolbarButton>
        </div>
      </BubbleMenu>

      {/* Editor content */}
      <EditorContent editor={editor} className="prose prose-lg max-w-none p-4" />
    </div>
  );
}
```

### Content Schema (ProseMirror JSON)

The `body_json` field stores Tiptap/ProseMirror JSON:

```json
{
  "type": "doc",
  "content": [
    {
      "type": "heading",
      "attrs": { "level": 2 },
      "content": [{ "type": "text", "text": "Introduction" }]
    },
    {
      "type": "paragraph",
      "content": [
        { "type": "text", "text": "This is a " },
        { "type": "text", "marks": [{ "type": "bold" }], "text": "blog post" },
        { "type": "text", "text": " about Vietnamese cuisine." }
      ]
    },
    {
      "type": "image",
      "attrs": {
        "src": "https://media.blog-platform.dev/images/pho.jpg",
        "alt": "A bowl of pho"
      }
    }
  ]
}
```

### Key Rules

1. **Never use `dangerouslySetInnerHTML` without DOMPurify** — XSS prevention is mandatory
2. **Tiptap JSON is the source of truth** — `body_html` is pre-rendered for performance but JSON is canonical
3. **Read-only mode uses `editable: false`** — No cursor, no toolbar, no editing
4. **Images must use `loading="lazy"`** — Performance for long posts
5. **Code blocks use syntax highlighting** — `lowlight` with `common` language set
6. **Links open in new tab** — `target="_blank"` with `rel="noopener noreferrer"`
7. **Vietnamese content** — Ensure fonts support Vietnamese diacritics (Inter, system-ui)
