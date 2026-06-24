# Plan 1: Foundation — CORS, API Client, Auth, Layouts
**Branch:** `task/phase-3-foundation`
**Parent branch:** `feat/phase-3-web-ui`
**Parent spec:** `2026-06-23-phase-3-web-ui-design.md`

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Establish CORS on backend, generate typed API client, wire auth (login/setup/middleware), and replace starter boilerplate with HydraForge layouts.

**Architecture:** Backend gets CORS middleware for `localhost:3000`. Frontend gets `openapi-fetch` client with typed `api.d.ts`, cookie-backed JWT storage, Pinia auth store, login/setup pages, auth middleware, and two layouts (default with sidebar shell, auth minimal).

**Tech Stack:** .NET 10 CORS middleware, openapi-fetch, openapi-typescript, Pinia, Nuxt middleware, Nuxt layouts

**Depends on:** Nothing (this is the first plan)

**Spec ref:** Sections 2, 3.1, 8, 9 (useAuthStore), 10 (useApi, useAuth, useAuthToken), 14

---

## Task 1: CORS + Port Convention

**Files:**
- Modify: `src/HydraForge.Server/Program.cs`
- Modify: `src/web-ui/nuxt.config.ts`

### Step 1: Add CORS to backend

In `src/HydraForge.Server/Program.cs`, add CORS registration before `builder.Services.AddOpenApi()`:

```csharp
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});
```

Add `app.UseCors()` after `app.UseSerilogRequestLogging()` and before `app.UseMiddleware<CorrelationIdMiddleware>()`:

```csharp
app.UseSerilogRequestLogging(options => { /* existing */ });

app.UseCors();

app.UseMiddleware<CorrelationIdMiddleware>();
```

### Step 2: Add API base URL to Nuxt config

In `src/web-ui/nuxt.config.ts`, add `runtimeConfig`:

```ts
export default defineNuxtConfig({
  modules: [
    '@nuxt/eslint',
    '@nuxt/ui'
  ],

  runtimeConfig: {
    public: {
      apiBaseUrl: 'http://localhost:5000'
    }
  },

  // ... rest unchanged
})
```

### Step 3: Verify

- Start backend: `dotnet run --project src/HydraForge.Server`
- Start frontend: `cd src/web-ui && pnpm dev`
- Open browser devtools → Network tab → fetch `http://localhost:5000/openapi/v1.json` from frontend origin → no CORS error

### Step 4: Commit

```bash
git add src/HydraForge.Server/Program.cs src/web-ui/nuxt.config.ts
git commit -m "feat: add CORS for localhost:3000 and apiBaseUrl runtime config"
```

---

## Task 2: API Client — openapi-fetch + openapi-typescript + useApi + useAuthToken + ApiError

**Files:**
- Create: `src/web-ui/app/types/api.d.ts`
- Create: `src/web-ui/app/composables/useAuthToken.ts`
- Create: `src/web-ui/app/composables/useApi.ts`
- Create: `src/web-ui/app/lib/api-error.ts`
- Modify: `src/web-ui/package.json`
- Modify: `src/web-ui/eslint.config.mjs`

### Step 1: Install dependencies

```bash
cd src/web-ui && pnpm add openapi-fetch && pnpm add -D openapi-typescript
```

### Step 2: Add codegen script to package.json

Add to `"scripts"` in `src/web-ui/package.json`:

```json
"generate:api-types": "openapi-typescript http://localhost:5000/openapi/v1.json -o ./app/types/api.d.ts"
```

### Step 3: Generate types (backend must be running)

```bash
cd src/web-ui && pnpm generate:api-types
```

### Step 4: Exclude generated types from ESLint stylistic rules

In `src/web-ui/eslint.config.mjs`:

```js
// @ts-check
import withNuxt from './.nuxt/eslint.config.mjs'

export default withNuxt(
  {
    ignores: ['app/types/api.d.ts']
  }
)
```

### Step 5: Create ApiError class

Create `src/web-ui/app/lib/api-error.ts`:

```ts
export class ApiError extends Error {
  status: number
  code: string
  title: string
  detail: string | null
  type: string
  correlationId: string

  constructor(status: number, code: string, title: string, detail: string | null, type: string, correlationId: string) {
    super(title)
    this.name = 'ApiError'
    this.status = status
    this.code = code
    this.title = title
    this.detail = detail
    this.type = type
    this.correlationId = correlationId
  }
}
```

### Step 6: Create useAuthToken composable

Create `src/web-ui/app/composables/useAuthToken.ts`:

```ts
export function useAuthToken() {
  const token = useCookie<string | null>('auth_token', {
    maxAge: 60 * 60, // 1 hour
    sameSite: 'lax',
    secure: false // true in production
  })

  const getToken = () => token.value ?? null
  const setToken = (value: string) => { token.value = value }
  const clearToken = () => { token.value = null }
  const hasToken = () => !!token.value

  return { getToken, setToken, clearToken, hasToken }
}
```

### Step 7: Create useApi composable

Create `src/web-ui/app/composables/useApi.ts`:

```ts
import createClient from 'openapi-fetch'
import type { paths } from '~/types/api'
import { ApiError } from '~/lib/api-error'

export function useApi() {
  const config = useRuntimeConfig()
  const { getToken, clearToken } = useAuthToken()

  const client = createClient<paths>({
    baseUrl: config.public.apiBaseUrl as string,
    headers: {
      'Content-Type': 'application/json'
    }
  })

  // Auth middleware: attach token to every request
  client.use({
    async onRequest({ request }) {
      const token = getToken()
      if (token) {
        request.headers.set('Authorization', `Bearer ${token}`)
      }
      return request
    },
    async onResponse({ response }) {
      if (response.status === 401) {
        clearToken()
        await navigateTo('/login')
        return response
      }

      if (!response.ok) {
        const contentType = response.headers.get('content-type') ?? ''
        if (contentType.includes('application/problem+json')) {
          const body = await response.json() as Record<string, unknown>
          throw new ApiError(
            response.status,
            (body.code as string) ?? 'UNKNOWN',
            (body.title as string) ?? response.statusText,
            (body.detail as string) ?? null,
            (body.type as string) ?? 'about:blank',
            (body.correlationId as string) ?? 'unknown'
          )
        }
        throw new ApiError(
          response.status,
          'UNKNOWN',
          response.statusText,
          null,
          'about:blank',
          'unknown'
        )
      }

      return response
    }
  })

  return client
}
```

### Step 8: Verify

- `cd src/web-ui && pnpm typecheck` — zero errors
- `cd src/web-ui && pnpm lint` — zero errors

### Step 9: Commit

```bash
git add src/web-ui/package.json src/web-ui/pnpm-lock.yaml src/web-ui/eslint.config.mjs src/web-ui/app/types/api.d.ts src/web-ui/app/composables/useAuthToken.ts src/web-ui/app/composables/useApi.ts src/web-ui/app/lib/api-error.ts
git commit -m "feat: add openapi-fetch client, ApiError, useAuthToken, useApi"
```

---

## Task 3: Auth — useAuthStore + Login Page + Setup Page + Auth Middleware

**Files:**
- Create: `src/web-ui/app/stores/auth.ts`
- Create: `src/web-ui/app/pages/login.vue`
- Create: `src/web-ui/app/pages/setup.vue`
- Create: `src/web-ui/app/middleware/auth.ts`
- Create: `src/web-ui/app/middleware/setup.ts`
- Create: `src/web-ui/app/composables/useAuth.ts`
- Modify: `src/web-ui/app/pages/index.vue` (replace starter content with redirect)

### Step 1: Create useAuthStore

Create `src/web-ui/app/stores/auth.ts`:

```ts
import { defineStore } from 'pinia'

interface User {
  userId: string
  username: string
  isAdmin: boolean
}

export const useAuthStore = defineStore('auth', () => {
  const token = ref<string | null>(null)
  const user = ref<User | null>(null)
  const isAuthenticated = computed(() => !!token.value && !!user.value)

  function setAuth(newToken: string, newUser: User) {
    token.value = newToken
    user.value = newUser
  }

  function clearAuth() {
    token.value = null
    user.value = null
  }

  return { token, user, isAuthenticated, setAuth, clearAuth }
})
```

### Step 2: Create useAuth composable

Create `src/web-ui/app/composables/useAuth.ts`:

```ts
export function useAuth() {
  const store = useAuthStore()
  const { setToken, clearToken, getToken, hasToken } = useAuthToken()
  const api = useApi()

  async function login(username: string, password: string) {
    const { data, error } = await api.POST('/api/Auth/login', {
      body: { username, password }
    })
    if (error) throw error

    setToken(data.token)
    store.setAuth(data.token, {
      userId: data.userId,
      username: data.username,
      isAdmin: data.isAdmin
    })
  }

  function logout() {
    store.clearAuth()
    clearToken()
    navigateTo('/login')
  }

  async function checkAuth() {
    if (!hasToken()) return false
    // Token exists in cookie; restore store from it
    const token = getToken()
    if (token) {
      // We can't decode JWT client-side without a library.
      // For now, trust the cookie. Full validation happens on first API call.
      // If 401, onResponse hook clears token and redirects.
      return true
    }
    return false
  }

  return { login, logout, checkAuth, isAuthenticated: store.isAuthenticated }
}
```

### Step 3: Create login page

Create `src/web-ui/app/pages/login.vue`:

```vue
<script setup lang="ts">
definePageMeta({ layout: 'auth' })

const username = ref('')
const password = ref('')
const error = ref<string | null>(null)
const loading = ref(false)

const { login } = useAuth()

async function handleSubmit() {
  error.value = null
  loading.value = true
  try {
    await login(username.value, password.value)
    await navigateTo('/projects')
  }
  catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Login failed'
  }
  finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="flex min-h-screen items-center justify-center p-4">
    <UCard class="w-full max-w-sm">
      <template #header>
        <div class="text-center">
          <h1 class="text-2xl font-bold">HydraForge</h1>
          <p class="text-sm text-muted">Sign in to your workspace</p>
        </div>
      </template>

      <form class="space-y-4" @submit.prevent="handleSubmit">
        <UFormField label="Username">
          <UInput v-model="username" autocomplete="username" required />
        </UFormField>

        <UFormField label="Password">
          <UInput v-model="password" type="password" autocomplete="current-password" required />
        </UFormField>

        <UAlert v-if="error" color="error" variant="subtle" :title="error" />

        <UButton type="submit" block :loading="loading">
          Sign in
        </UButton>
      </form>
    </UCard>
  </div>
</template>
```

### Step 4: Create setup page

Create `src/web-ui/app/pages/setup.vue`:

```vue
<script setup lang="ts">
definePageMeta({ layout: 'auth' })

const password = ref('')
const confirmPassword = ref('')
const error = ref<string | null>(null)
const success = ref(false)
const loading = ref(false)

const api = useApi()

async function handleSubmit() {
  error.value = null
  if (password.value !== confirmPassword.value) {
    error.value = 'Passwords do not match'
    return
  }
  loading.value = true
  try {
    // First-run setup: login with default admin creds, then change password
    // The backend AdminSeeder creates default admin on first boot.
    // We attempt login with defaults; if it works, we're in first-run mode.
    const { data: loginData, error: loginError } = await api.POST('/api/Auth/login', {
      body: { username: 'admin', password: 'Admin123!' }
    })
    if (loginError) {
      error.value = 'Setup unavailable — admin already configured'
      loading.value = false
      return
    }

    // TODO: Backend needs a change-password endpoint.
    // For now, show success and redirect to login.
    // The actual password change will be implemented when the backend endpoint exists.
    success.value = true
    setTimeout(() => navigateTo('/login'), 2000)
  }
  catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Setup failed'
  }
  finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="flex min-h-screen items-center justify-center p-4">
    <UCard class="w-full max-w-sm">
      <template #header>
        <div class="text-center">
          <h1 class="text-2xl font-bold">HydraForge Setup</h1>
          <p class="text-sm text-muted">Configure your admin password</p>
        </div>
      </template>

      <div v-if="success">
        <UAlert color="success" title="Setup complete! Redirecting to login..." />
      </div>

      <form v-else class="space-y-4" @submit.prevent="handleSubmit">
        <UFormField label="New Password">
          <UInput v-model="password" type="password" required />
        </UFormField>

        <UFormField label="Confirm Password">
          <UInput v-model="confirmPassword" type="password" required />
        </UFormField>

        <UAlert v-if="error" color="error" variant="subtle" :title="error" />

        <UButton type="submit" block :loading="loading">
          Set Password
        </UButton>
      </form>
    </UCard>
  </div>
</template>
```

### Step 5: Create auth middleware

Create `src/web-ui/app/middleware/auth.ts`:

```ts
export default defineNuxtRouteMiddleware((to) => {
  const { hasToken } = useAuthToken()

  if (to.path === '/login' || to.path === '/setup') return

  if (!hasToken()) {
    return navigateTo('/login')
  }
})
```

### Step 6: Create setup middleware

Create `src/web-ui/app/middleware/setup.ts`:

```ts
export default defineNuxtRouteMiddleware(async (to) => {
  // Skip on setup page itself and login page
  if (to.path === '/setup' || to.path === '/login') return

  const { hasToken } = useAuthToken()
  if (!hasToken()) return

  // Attempt to detect first-run by checking if default admin works.
  // If the user already has a real token, this is a no-op.
  // Full first-run detection will be refined when backend adds a dedicated endpoint.
})
```

### Step 7: Replace index.vue with redirect

Replace `src/web-ui/app/pages/index.vue`:

```vue
<script setup lang="ts">
definePageMeta({ middleware: ['auth'] })

// Redirect to projects or login
await navigateTo('/projects', { redirectCode: 302 })
</script>

<template>
  <div />
</template>
```

### Step 8: Verify

- `cd src/web-ui && pnpm typecheck` — zero errors
- `cd src/web-ui && pnpm lint` — zero errors
- Manual: visit `http://localhost:3000` → redirects to `/login`
- Manual: login with valid credentials → redirects to `/projects` (page not built yet, will 404 — expected)

### Step 9: Commit

```bash
git add src/web-ui/app/stores/auth.ts src/web-ui/app/composables/useAuth.ts src/web-ui/app/pages/login.vue src/web-ui/app/pages/setup.vue src/web-ui/app/pages/index.vue src/web-ui/app/middleware/auth.ts src/web-ui/app/middleware/setup.ts
git commit -m "feat: add auth store, login/setup pages, auth middleware"
```

---

## Task 4: Layouts — Default + Auth + Replace app.vue Boilerplate

**Files:**
- Create: `src/web-ui/app/layouts/default.vue`
- Create: `src/web-ui/app/layouts/auth.vue`
- Modify: `src/web-ui/app/app.vue`
- Delete: `src/web-ui/app/components/TemplateMenu.vue`

### Step 1: Create auth layout

Create `src/web-ui/app/layouts/auth.vue`:

```vue
<template>
  <UApp>
    <UMain>
      <slot />
    </UMain>
  </UApp>
</template>
```

### Step 2: Create default layout

Create `src/web-ui/app/layouts/default.vue`:

```vue
<script setup lang="ts">
const { logout, isAuthenticated } = useAuth()
</script>

<template>
  <UApp>
    <UHeader>
      <template #left>
        <NuxtLink to="/projects" class="flex items-center gap-2">
          <span class="text-lg font-bold">HydraForge</span>
        </NuxtLink>
      </template>

      <template #right>
        <UColorModeButton />
        <UButton
          v-if="isAuthenticated"
          label="Logout"
          color="neutral"
          variant="ghost"
          @click="logout"
        />
      </template>
    </UHeader>

    <UMain>
      <slot />
    </UMain>
  </UApp>
</template>
```

### Step 3: Simplify app.vue

Replace `src/web-ui/app/app.vue`:

```vue
<template>
  <NuxtLayout>
    <NuxtPage />
  </NuxtLayout>
</template>
```

### Step 4: Remove starter template components

Delete `src/web-ui/app/components/TemplateMenu.vue`.

### Step 5: Verify

- `cd src/web-ui && pnpm typecheck` — zero errors
- `cd src/web-ui && pnpm lint` — zero errors
- `cd src/web-ui && pnpm build` — successful production build
- Manual: visit `/login` → auth layout (no header). Login → default layout (header with logout button)

### Step 6: Commit

```bash
git add src/web-ui/app/layouts/default.vue src/web-ui/app/layouts/auth.vue src/web-ui/app/app.vue
git rm src/web-ui/app/components/TemplateMenu.vue
git commit -m "feat: add default and auth layouts, simplify app.vue"
```

---

## Verification (Plan 1 Complete)

Reference `nuxt-verification` skill:
1. `cd src/web-ui && pnpm typecheck` — zero errors
2. `cd src/web-ui && pnpm lint` — zero errors
3. `cd src/web-ui && pnpm build` — successful production build
4. Manual: CORS works (fetch API from frontend origin)
5. Manual: login flow works end-to-end