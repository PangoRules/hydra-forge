import { test, expect } from './fixtures'

test.use({ viewport: { width: 1280, height: 800 } })

test.describe('Card description save button', () => {
  test('is disabled until the description is dirty, then saves and disables again', async ({ page, seedCard }) => {
    await page.goto(`/projects/${seedCard.projectId}/board`)
    await page.getByRole('heading', { name: seedCard.cardTitle, exact: true }).click()

    const desktop = page.getByTestId('card-modal-desktop')
    const saveButton = desktop.getByRole('button', { name: 'Save' })

    await expect(saveButton).toBeDisabled()

    await desktop.locator('.ProseMirror').click()
    await page.keyboard.type('Updated from Playwright')

    await expect(saveButton).toBeEnabled()
    await saveButton.click()

    await expect(saveButton).toBeDisabled({ timeout: 10000 })
  })

  test('auto-saves after the debounce window without clicking Save', async ({ page, seedCard }) => {
    await page.goto(`/projects/${seedCard.projectId}/board`)
    await page.getByRole('heading', { name: seedCard.cardTitle, exact: true }).click()

    const desktop = page.getByTestId('card-modal-desktop')
    const saveButton = desktop.getByRole('button', { name: 'Save' })

    await desktop.locator('.ProseMirror').click()
    await page.keyboard.type('Auto-saved text')

    await expect(saveButton).toBeEnabled()
    // Debounce is 2s (CardDescription.vue) — wait past it without clicking Save.
    await expect(saveButton).toBeDisabled({ timeout: 5000 })

    await page.reload()
    await page.getByRole('heading', { name: seedCard.cardTitle, exact: true }).click()
    await expect(page.getByTestId('card-modal-desktop')).toContainText('Auto-saved text')
  })
})
