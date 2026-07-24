import { test, expect } from './fixtures'

test.use({ viewport: { width: 1280, height: 800 } })

test('a stale save from a second tab is rejected, not silently merged', async ({ browser, seedCard }) => {
  const contextA = await browser.newContext({ storageState: 'e2e/.auth/testadmin.json' })
  const contextB = await browser.newContext({ storageState: 'e2e/.auth/testadmin.json' })
  const pageA = await contextA.newPage()
  const pageB = await contextB.newPage()

  for (const page of [pageA, pageB]) {
    await page.goto(`/projects/${seedCard.projectId}/board`)
    await page.getByRole('heading', { name: seedCard.cardTitle, exact: true }).click()
  }

  // Tab A saves first and succeeds.
  const descA = pageA.getByTestId('card-modal-desktop')
  await descA.locator('.ProseMirror').click()
  await pageA.keyboard.type('Saved from tab A')
  await descA.getByRole('button', { name: 'Save' }).click()
  await expect(descA.getByRole('button', { name: 'Save' })).toBeDisabled({ timeout: 10000 })

  // Tab B still holds the version it loaded with — its save must fail, not overwrite tab A's change.
  const descB = pageB.getByTestId('card-modal-desktop')
  await descB.locator('.ProseMirror').click()
  await pageB.keyboard.type('Saved from tab B (stale)')
  await descB.getByRole('button', { name: 'Save' }).click()

  await expect(descB.locator('[role="alert"]')).toBeVisible({ timeout: 10000 })

  await contextA.close()
  await contextB.close()
})
