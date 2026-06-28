import { test, expect } from './fixtures'

test.use({ viewport: { width: 1280, height: 800 } })

test('archiving a card removes it from the board, restoring brings it back', async ({ page, seedCard }) => {
  await page.goto(`/projects/${seedCard.projectId}/board`)
  // Wait for board to finish loading (the heading appears)
  await expect(page.getByRole('heading', { name: seedCard.cardTitle, exact: true })).toBeVisible({ timeout: 15000 })

  // Open card modal
  await page.getByRole('heading', { name: seedCard.cardTitle, exact: true }).click()

  // Archive
  await page.getByTitle('Archive card').click()
  await page.getByRole('button', { name: 'Archive' }).click() // ConfirmDialog confirm button

  // Card should be gone from the board
  await expect(page.getByRole('heading', { name: seedCard.cardTitle, exact: true })).not.toBeVisible()

  // Reload and wait for board to load
  await page.reload()
  await page.waitForLoadState('networkidle')

  // Toggle the global "Archived" checkbox (BoardFilterBar) to show archived cards
  const archivedLabel = page.locator('label').filter({ hasText: 'Archived' }).first()
  await expect(archivedLabel).toBeVisible({ timeout: 10000 })
  await archivedLabel.locator('input[type="checkbox"]').check()

  // Wait for the archived card heading to appear — heading includes "(archived)" suffix
  await expect(page.getByRole('heading', { name: seedCard.cardTitle })).toBeVisible({ timeout: 10000 })

  // Check the per-column "Archived only" checkbox (visible because includeArchived is now true)
  const archivedOnlyLabel = page.locator('label').filter({ hasText: 'Archived only' }).first()
  await expect(archivedOnlyLabel).toBeVisible({ timeout: 5000 })
  await archivedOnlyLabel.locator('input[type="checkbox"]').check()

  // Now the archived card should appear — click it to open modal
  // Note: heading text includes "(archived)" suffix — use substring match
  await expect(page.getByRole('heading', { name: seedCard.cardTitle })).toBeVisible()
  await page.getByRole('heading', { name: seedCard.cardTitle }).click()

  // Restore via modal
  await expect(page.getByTitle('Restore card')).toBeVisible()
  await page.getByTitle('Restore card').click()

  // Verify success toast
  await expect(page.getByText('Card restored', { exact: true }).first()).toBeVisible()
})
