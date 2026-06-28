import { test as setup } from '@playwright/test'

const authFile = 'e2e/.auth/testadmin.json'
const apiBaseUrl = process.env.E2E_API_BASE_URL ?? 'http://localhost:5000'

setup('authenticate as testadmin', async ({ page, request }) => {
  // Login via API directly — avoids form-submit quirks with Nuxt UI v4's
  // UButton → ULink → Primitive abstraction chain which doesn't reliably
  // trigger <form> submit event from <button type="submit">.
  const resp = await request.post(`${apiBaseUrl}/api/Auth/login`, {
    data: { username: 'testadmin', password: 'TestAdmin123!' }
  })
  const body = await resp.json() as { accessToken: string }
  const token = body.accessToken

  // Set the auth cookie via JS so the browser sends it on subsequent requests
  await page.goto('/login') // must be on same origin to set cookie
  await page.evaluate((t) => {
    document.cookie = `auth_token=${t}; Path=/; Max-Age=3600; SameSite=Lax`
  }, token)

  await page.context().storageState({ path: authFile })
})
