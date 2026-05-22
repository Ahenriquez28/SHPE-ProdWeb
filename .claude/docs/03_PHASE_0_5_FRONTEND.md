# Phase 0.5 — Frontend Infrastructure

> **Goal:** Set up the React + TypeScript + Vite + Tailwind frontend with shared types, an API client, layouts, UI primitives, and role-gated routing — so Phase 1 verticals have a place to plug in.

---

## 0.5.1 — Vite + React + TypeScript + Tailwind setup

### Create the web app

```bash
cd ~/Desktop/SHPE-ProdWeb/apps/web
pnpm create vite . --template react-ts
pnpm install
```

### Install dependencies

```bash
pnpm add react-router-dom @clerk/clerk-react @tanstack/react-query lucide-react clsx
pnpm add -D tailwindcss@^4 @tailwindcss/vite @types/node
```

### Configure Tailwind v4

`vite.config.ts`:

```typescript
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import path from 'path'

// Vite config — bundles the React app for dev (HMR) and prod.
// Tailwind v4 plugin processes utility classes at build time.
// Path alias '@' lets us import from anywhere as '@/components/Button'
// instead of '../../../components/Button'.
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    port: 5173,
    // Proxy /api requests to the .NET backend running in Docker.
    // Avoids CORS hassle during development.
    proxy: {
      '/api': 'http://localhost:5001',
    },
  },
})
```

`src/index.css`:

```css
@import "tailwindcss";

/* Brand variables — tweak once for the whole app */
@theme {
  --color-brand: #f48024;        /* SHPE orange */
  --color-brand-dark: #c8651c;
  --font-sans: ui-sans-serif, system-ui, -apple-system, sans-serif;
}

/* Base body styles */
body {
  font-family: var(--font-sans);
}
```

`tsconfig.json` paths:

```json
{
  "compilerOptions": {
    "baseUrl": ".",
    "paths": {
      "@/*": ["src/*"]
    }
  }
}
```

### Verify

```bash
pnpm dev
# → http://localhost:5173 shows the Vite welcome page
```

### Definition of done

App boots at `localhost:5173`. Tailwind utility classes render. `@/` imports work.

---

## 0.5.2 — Shared types

### Create `src/types/api.ts`

```typescript
// Hand-written TypeScript types that mirror the C# DTOs.
//
// Why hand-written instead of OpenAPI codegen?
//   - Simpler to debug in the first 4 weeks
//   - Switch to codegen in week 5 if drift becomes a problem
//   - Comments on each type help devs learn the data model
//
// RULE: when you add a field to a C# DTO, you MUST update the matching type here.
// CI doesn't enforce this yet — discipline only.

// Branded primitive types
export type Uuid = string;
export type IsoDate = string;

// ─── Roles ────────────────────────────────────────────
export type Role =
  | 'member'
  | 'admin'
  | 'super_admin'
  | 'eboard'
  | 'alumni'
  | 'panelist'
  | 'sponsor_contact'
  | 'dev_team';

// ─── Person ───────────────────────────────────────────
export type Person = {
  id: Uuid;
  fullName: string;
  email: string | null;
  gsuEmail: string | null;
  phone: string | null;
  // Free-text like "May 2027" — not a year integer
  gradYear: string | null;
  linkedinUrl: string | null;
  roles: Role[];
  shareContact: boolean;
  smsOptIn: boolean;
  photoOptOut: boolean;
  createdAt: IsoDate;
};

// ─── Events ───────────────────────────────────────────
export type EventPublic = {
  id: Uuid;
  title: string;
  description: string | null;
  flyerUrl: string | null;
  startAt: IsoDate;
  endAt: IsoDate | null;
  location: string | null;
  isPublic: true;
};

// Admin sees more fields — budget, goals, attendance counts
export type EventAdmin = EventPublic & {
  budgetCents: number | null;
  goalText: string | null;
  goalMet: boolean | null;
  attendeeCount: number;
  todoCount: number;
  todoCompleted: number;
};

// ─── E-Board ──────────────────────────────────────────
export type EBoardMember = {
  id: Uuid;
  personId: Uuid;
  fullName: string;
  linkedInUrl: string | null;
  groupName: 'eboard' | 'dev_team';
  title: string;
  headshotUrl: string | null;
  sortOrder: number;
};

// ─── Sponsors ─────────────────────────────────────────
export type Sponsor = {
  id: Uuid;
  name: string;
  logoUrl: string | null;
  websiteUrl: string | null;
  tier: string | null;
  sortOrder: number;
};

export type SponsorsResponse = {
  sponsors: Sponsor[];
  currentPacketUrl: string | null;
};

// ─── RFC 7807 error shape ─────────────────────────────
// All API errors return this shape via ProblemDetailsResults
export type ApiError = {
  type: string;        // Stable identifier the frontend can branch on
  title: string;       // Human-readable summary
  status: number;
  detail?: string;
  traceId?: string;    // For bug reports
  errors?: Record<string, string[]>;  // Validation errors keyed by field
};
```

---

## 0.5.3 — Typed API client

### Create `src/lib/api.ts`

```typescript
import { useAuth } from '@clerk/clerk-react';
import type { ApiError } from '@/types/api';

// Typed wrapper around fetch().
//
// Every request:
//   - Resolves the Clerk JWT and attaches it as Bearer
//   - Throws a typed ApiException on non-2xx so callers can handle errors
//   - Returns the parsed JSON body or undefined for 204 No Content
//
// Usage:
//   const api = useApi();
//   const me = await api.get<Person>('/api/me');
//   await api.post<Person>('/api/people', { fullName: 'Alex' });

export class ApiException extends Error {
  status: number;
  problem: ApiError;

  constructor(status: number, problem: ApiError) {
    super(problem.title || `HTTP ${status}`);
    this.status = status;
    this.problem = problem;
  }
}

const BASE_URL = import.meta.env.VITE_API_URL || '';

async function request<T>(
  method: string,
  path: string,
  body?: unknown,
  getToken?: () => Promise<string | null>,
): Promise<T> {
  const headers: Record<string, string> = {};
  if (body) headers['Content-Type'] = 'application/json';

  if (getToken) {
    const token = await getToken();
    if (token) headers['Authorization'] = `Bearer ${token}`;
  }

  const res = await fetch(`${BASE_URL}${path}`, {
    method,
    headers,
    body: body ? JSON.stringify(body) : undefined,
  });

  if (!res.ok) {
    const problem = await res.json().catch(() => ({
      type: 'unknown',
      title: res.statusText,
      status: res.status,
    }));
    throw new ApiException(res.status, problem as ApiError);
  }

  // 204 No Content — no body to parse
  if (res.status === 204) return undefined as T;

  return res.json();
}

// React hook returning a bound API client.
// Use this inside components/hooks where useAuth() is available.
export function useApi() {
  const { getToken } = useAuth();

  // Clerk's getToken with template 'default' — see Clerk dashboard → JWT Templates
  const auth = () => getToken({ template: 'default' });

  return {
    get:    <T,>(path: string)              => request<T>('GET',    path, undefined, auth),
    post:   <T,>(path: string, body?: any) => request<T>('POST',   path, body,      auth),
    patch:  <T,>(path: string, body?: any) => request<T>('PATCH',  path, body,      auth),
    delete: <T,>(path: string)              => request<T>('DELETE', path, undefined, auth),
  };
}
```

### Create `src/lib/upload.ts`

```typescript
import type { Uuid } from '@/types/api';

// File upload helper.
// Three-step flow (see backend 0.8):
//   1. Get presigned PUT URL from /api/files/presigned
//   2. PUT file directly to DO Spaces
//   3. POST metadata to /api/files to register the FileRecord
//
// Other devs just call:
//   const { fileId, cdnUrl } = await uploadFile(file, 'resume_review');

type Purpose = 'resume_review' | 'flyer' | 'photo' | 'headshot' | 'logo' | 'doc';

export async function uploadFile(
  file: File,
  purpose: Purpose,
  apiFetch: <T>(path: string, body?: any) => Promise<T>,
): Promise<{ fileId: Uuid; cdnUrl: string }> {
  // Step 1: ask backend for presigned URL
  const presigned = await apiFetch<{
    uploadUrl: string;
    spacesKey: string;
    expiresInSeconds: number;
  }>('/api/files/presigned', {
    filename: file.name,
    mimeType: file.type,
    purpose,
  });

  // Step 2: PUT file bytes directly to Spaces (browser → DO, API not involved)
  const putRes = await fetch(presigned.uploadUrl, {
    method: 'PUT',
    body: file,
    headers: { 'Content-Type': file.type },
  });
  if (!putRes.ok) throw new Error('Upload to Spaces failed');

  // Step 3: register the metadata row
  const registered = await apiFetch<{ id: Uuid; cdnUrl: string }>('/api/files', {
    spacesKey: presigned.spacesKey,
    mimeType: file.type,
    sizeBytes: file.size,
  });

  return { fileId: registered.id, cdnUrl: registered.cdnUrl };
}
```

---

## 0.5.4 — Layout components

Three top-level layouts. Each wraps a `<Outlet />` for child routes.

### `src/layouts/PublicLayout.tsx`

```typescript
// PublicLayout — used for routes anyone can see: /, /eboard, /sponsors, /login, /signup
// Header has logo + login button. Footer has social links.
import { Outlet, Link } from 'react-router-dom';
import { SignedIn, SignedOut, UserButton } from '@clerk/clerk-react';

export function PublicLayout() {
  return (
    <div className="min-h-screen flex flex-col">
      <header className="border-b">
        <nav className="container mx-auto flex items-center justify-between p-4">
          <Link to="/" className="text-xl font-bold text-brand">SHPE @ GSU</Link>
          <div className="flex items-center gap-4">
            <SignedOut>
              <Link to="/login" className="text-sm">Sign in</Link>
              <Link to="/signup" className="px-4 py-2 bg-brand text-white rounded text-sm">
                Become a member
              </Link>
            </SignedOut>
            <SignedIn>
              <Link to="/portal" className="text-sm">Member portal</Link>
              <UserButton />
            </SignedIn>
          </div>
        </nav>
      </header>

      <main className="flex-1">
        <Outlet />
      </main>

      <footer className="border-t mt-12">
        <div className="container mx-auto p-4 text-sm text-gray-600">
          {/* TODO Dev 2: socials, contact email, copyright */}
        </div>
      </footer>
    </div>
  );
}
```

### `src/layouts/MemberLayout.tsx`

```typescript
// MemberLayout — used for /portal/* routes
// Top nav with member sections + user menu
import { Outlet, NavLink } from 'react-router-dom';
import { UserButton } from '@clerk/clerk-react';

const navItems = [
  { to: '/portal/dashboard', label: 'Dashboard' },
  { to: '/portal/events', label: 'Events' },
  { to: '/portal/resume-reviews', label: 'Resume Reviews' },
  { to: '/portal/panelists', label: 'Panelists' },
  { to: '/portal/alumni', label: 'Alumni' },
  { to: '/portal/photos', label: 'Photos' },
  { to: '/portal/profile', label: 'Profile' },
];

export function MemberLayout() {
  return (
    <div className="min-h-screen flex flex-col">
      <header className="border-b">
        <nav className="container mx-auto flex items-center justify-between p-4">
          <div className="flex items-center gap-6">
            <span className="text-xl font-bold text-brand">SHPE @ GSU</span>
            <div className="flex gap-4 text-sm">
              {navItems.map(item => (
                <NavLink
                  key={item.to}
                  to={item.to}
                  className={({ isActive }) =>
                    isActive ? 'text-brand font-semibold' : 'text-gray-600 hover:text-gray-900'
                  }
                >
                  {item.label}
                </NavLink>
              ))}
            </div>
          </div>
          <UserButton />
        </nav>
      </header>
      <main className="flex-1 container mx-auto p-6">
        <Outlet />
      </main>
    </div>
  );
}
```

### `src/layouts/AdminLayout.tsx`

```typescript
// AdminLayout — used for /admin/* routes
// Sidebar with admin sections (collapsible groups), breadcrumb bar, user menu
import { Outlet, NavLink } from 'react-router-dom';
import { UserButton } from '@clerk/clerk-react';

const sections = [
  {
    name: 'Operations',
    items: [
      { to: '/admin/dashboard', label: 'Dashboard' },
      { to: '/admin/events', label: 'Events' },
      { to: '/admin/people', label: 'People' },
      { to: '/admin/comms', label: 'Comms' },
    ],
  },
  {
    name: 'Internal',
    items: [
      { to: '/admin/meetings', label: 'Meetings' },
      { to: '/admin/history', label: 'History' },
    ],
  },
  {
    name: 'Site',
    items: [
      { to: '/admin/eboard', label: 'E-Board' },
      { to: '/admin/sponsors', label: 'Sponsors' },
    ],
  },
  {
    name: 'Settings',
    items: [
      { to: '/admin/settings', label: 'Settings' },
    ],
  },
];

export function AdminLayout() {
  return (
    <div className="min-h-screen flex">
      <aside className="w-60 border-r bg-gray-50 p-4">
        <div className="text-xl font-bold text-brand mb-6">SHPE Admin</div>
        {sections.map(section => (
          <div key={section.name} className="mb-4">
            <div className="text-xs uppercase text-gray-500 mb-1">{section.name}</div>
            <ul className="space-y-1">
              {section.items.map(item => (
                <li key={item.to}>
                  <NavLink
                    to={item.to}
                    className={({ isActive }) =>
                      `block px-2 py-1 rounded text-sm ${
                        isActive ? 'bg-brand text-white' : 'hover:bg-gray-200'
                      }`
                    }
                  >
                    {item.label}
                  </NavLink>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </aside>

      <div className="flex-1 flex flex-col">
        <header className="border-b p-4 flex justify-end">
          <UserButton />
        </header>
        <main className="flex-1 p-6">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
```

---

## 0.5.5 — UI primitives

Build the smallest reusable component set. Not beautiful — reliable and typed.

Create `src/components/ui/`:

- `Button.tsx` — variants: primary, secondary, danger, ghost
- `Input.tsx`, `Textarea.tsx`, `Select.tsx`, `Checkbox.tsx`
- `Modal.tsx`, `Drawer.tsx`
- `Toast.tsx` + `useToast()` hook
- `Table.tsx` — sortable, paginated
- `Card.tsx`, `Badge.tsx`, `Avatar.tsx`
- `Tabs.tsx` — router-aware (URL state)
- `EmptyState.tsx`
- `Spinner.tsx`, `Skeleton.tsx`

**Style guidance:** Tailwind utility classes only. No custom CSS files except for `index.css`. Use `clsx` for conditional classes.

Sample Button:

```typescript
import { clsx } from 'clsx';
import type { ButtonHTMLAttributes, ReactNode } from 'react';

type Variant = 'primary' | 'secondary' | 'danger' | 'ghost';

type Props = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: Variant;
  children: ReactNode;
};

// Button — variant-driven. Primary by default.
// danger is for delete/destructive actions; ghost is link-style.
export function Button({ variant = 'primary', className, children, ...rest }: Props) {
  return (
    <button
      {...rest}
      className={clsx(
        'px-4 py-2 rounded text-sm font-medium transition disabled:opacity-50 disabled:cursor-not-allowed',
        variant === 'primary' && 'bg-brand text-white hover:bg-brand-dark',
        variant === 'secondary' && 'border border-gray-300 hover:bg-gray-50',
        variant === 'danger' && 'bg-red-600 text-white hover:bg-red-700',
        variant === 'ghost' && 'text-brand hover:underline',
        className,
      )}
    >
      {children}
    </button>
  );
}
```

---

## 0.5.6 — Routing + role-gated routes

### `src/App.tsx`

```typescript
import { ClerkProvider } from '@clerk/clerk-react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

// Layouts
import { PublicLayout } from '@/layouts/PublicLayout';
import { MemberLayout } from '@/layouts/MemberLayout';
import { AdminLayout } from '@/layouts/AdminLayout';

// Guards
import { RequireAuth } from '@/components/RequireAuth';
import { RequireRole } from '@/components/RequireRole';

// Pages — these are stubs in Phase 0.5; Phase 1 scaffolds them.
import { HomePage } from '@/pages/public/HomePage';
// ... etc

const queryClient = new QueryClient();
const CLERK_KEY = import.meta.env.VITE_CLERK_PUBLISHABLE_KEY;

export default function App() {
  return (
    <ClerkProvider publishableKey={CLERK_KEY}>
      <QueryClientProvider client={queryClient}>
        <BrowserRouter>
          <Routes>
            {/* Public — anyone */}
            <Route element={<PublicLayout />}>
              <Route path="/" element={<HomePage />} />
              {/* /eboard, /sponsors, /login, /signup, /enrich/:token */}
            </Route>

            {/* Member — requires auth */}
            <Route element={<RequireAuth><MemberLayout /></RequireAuth>}>
              <Route path="/portal" element={<Navigate to="/portal/dashboard" />} />
              {/* /portal/dashboard, /portal/events, ... */}
            </Route>

            {/* Admin — requires admin role */}
            <Route element={<RequireRole role="admin"><AdminLayout /></RequireRole>}>
              <Route path="/admin" element={<Navigate to="/admin/dashboard" />} />
              {/* /admin/dashboard, /admin/events, ... */}
            </Route>
          </Routes>
        </BrowserRouter>
      </QueryClientProvider>
    </ClerkProvider>
  );
}
```

### `src/components/RequireAuth.tsx`

```typescript
import { useAuth } from '@clerk/clerk-react';
import { Navigate } from 'react-router-dom';
import { Spinner } from '@/components/ui/Spinner';
import type { ReactNode } from 'react';

// Redirect unauthenticated users to login.
// While Clerk is still loading, show a spinner — don't flash the login page.
export function RequireAuth({ children }: { children: ReactNode }) {
  const { isSignedIn, isLoaded } = useAuth();
  if (!isLoaded) return <Spinner />;
  if (!isSignedIn) return <Navigate to="/login" replace />;
  return <>{children}</>;
}
```

### `src/components/RequireRole.tsx`

```typescript
import { Navigate } from 'react-router-dom';
import { useMe } from '@/hooks/useMe';
import { Spinner } from '@/components/ui/Spinner';
import type { Role } from '@/types/api';
import type { ReactNode } from 'react';

// Redirect users without the required role.
// super_admin implicitly passes any role check.
export function RequireRole({ role, children }: { role: Role; children: ReactNode }) {
  const { data: me, isLoading } = useMe();
  if (isLoading) return <Spinner />;
  if (!me) return <Navigate to="/login" replace />;

  const ok = me.roles.includes(role) || me.roles.includes('super_admin');
  if (!ok) return <Navigate to="/forbidden" replace />;

  return <>{children}</>;
}
```

### `src/hooks/useMe.ts`

```typescript
import { useQuery } from '@tanstack/react-query';
import { useApi } from '@/lib/api';
import type { Person } from '@/types/api';

// useMe — fetches the current user's Person record + roles
// Cached across the whole app via React Query.
export function useMe() {
  const api = useApi();
  return useQuery({
    queryKey: ['me'],
    queryFn: () => api.get<Person>('/api/me'),
    staleTime: 60_000,  // Don't refetch within 60s of last fetch
  });
}
```

---

## Definition of done for Phase 0.5

- App boots at `localhost:5173` with all 3 layouts rendering
- `useApi()` works against the .NET backend
- `useMe()` resolves the logged-in user's roles
- `RequireAuth` and `RequireRole` correctly gate routes
- Routes exist for every Phase 1 page (even if pages are `<div>TODO</div>` stubs)
- All UI primitives importable from `@/components/ui/*`

Move on to `04_PHASE_1_SCAFFOLDING.md`.
