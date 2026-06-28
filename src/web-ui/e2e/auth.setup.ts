import { test as setup, expect } from '@playwright/test'

const authFile = 'e2e/.auth/testadmin.json'

setup('authenticate as testadmin', async ({ page }) => {
  page.on('console', msg => console.log(`[CONSOLE ${msg.type()}]: ${msg.text()}`))
  page.on('pageerror', err => console.log(`[PAGE ERROR]: ${err.message}`))

  await page.goto('/login')
  await page.waitForLoadState('networkidle')

  await page.locator('input[autocomplete="username"]').fill('testadmin')
  await page.locator('input[autocomplete="current-password"]').fill('TestAdmin123!')

  await page.getByRole('button', { name: 'Sign in' }).click()

  // Wait for either navigation or an error alert
  await Promise.race([
    page.waitForURL(/\/projects/, { timeout: 10000 }),
    page.waitForSelector('.text-error, [role="alert"]', { timeout: 10000 }).catch(() => null)
  ])

  console.log('Current URL:', page.url())

  // Check for error display
  const errorAlert = page.locator('.text-error, [role="alert"]').first()
  if (await errorAlert.isVisible({ timeout: 1000 }).catch(() => false)) {
    console.log('Error alert text:', await errorAlert.textContent())
  }

  await expect(page).toHaveURL(/\/projects/)
  await page.context().storageState({ path: authFile })
})