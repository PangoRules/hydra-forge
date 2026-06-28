import { test, expect } from '@playwright/test'

test.use({ storageState: undefined })

test('login page renders', async ({ page }) => {
  await page.goto('/login')
  await expect(page.getByRole('heading', { name: 'HydraForge' })).toBeVisible()
  await expect(page.getByLabel('Username')).toBeVisible()
  await expect(page.getByLabel('Password')).toBeVisible()
})