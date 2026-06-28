import { test as setup, expect } from '@playwright/test'

const authFile = 'e2e/.auth/testadmin.json'

setup('authenticate as testadmin', async ({ page }) => {
  await page.goto('/login')
  await page.locator('input[autocomplete="username"]').fill('testadmin')
  await page.locator('input[autocomplete="current-password"]').fill('TestAdmin123!')
  await page.getByRole('button', { name: 'Sign in' }).click()
  await expect(page).toHaveURL(/\/projects/)
  await page.context().storageState({ path: authFile })
})
