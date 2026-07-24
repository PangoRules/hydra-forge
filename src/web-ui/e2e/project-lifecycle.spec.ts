import { test, expect } from '@playwright/test'

// See SMOKE_FLOW.md for the human-readable version of this flow and why
// it's one test instead of several: one seeded project, one linear pass,
// ending in archiving everything — not a pile of throwaway CI data.

test.use({ viewport: { width: 1280, height: 800 } })

test('Website Revamp: full project lifecycle smoke flow', async ({ page }) => {
  const suffix = `${Date.now()}-${Math.floor(Math.random() * 10000)}`
  const projectName = `Website Revamp ${suffix}`

  const goalTitle = 'Launch redesigned marketing site'
  const ideaTitle = 'Add dark mode toggle'
  const issueTitle = 'Checkout page throws 500 on Safari'
  const heroTaskTitle = 'Update hero image assets'
  const emailTaskTitle = 'Write launch announcement email'

  // ---- 1. Create project ----
  await page.goto('/projects')
  await page.getByRole('button', { name: 'New Project' }).click()

  await page.getByPlaceholder('My Project').fill(projectName)
  await page.getByPlaceholder('Optional description').fill('Redesign the marketing site and ship it.')
  // Template left on its default (General) — see SMOKE_FLOW.md.
  await page.getByRole('button', { name: 'Create' }).click()

  await expect(page.getByText('Project created', { exact: true }).first()).toBeVisible()

  // ---- 2. Open the new project's board ----
  // Both the desktop table and the mobile card list render simultaneously
  // (CSS-hidden by breakpoint, not v-if). UTable renders each selectable
  // row with role="button", not role="row" — scope to that to avoid
  // matching the mobile copy's <h3> too.
  await page.getByRole('button', { name: new RegExp(projectName) }).click()
  await expect(page).toHaveURL(/\/board$/)
  await expect(page.getByTestId('add-card-btn')).toBeVisible({ timeout: 15000 })

  async function createCard(opts: { title: string, type: string, column?: string, parent?: string, dueDate?: string }) {
    await page.getByTestId('add-card-btn').click()
    // Scope to the open dialog — ColumnHeader.vue renders its own native
    // <select> "Type:" filter (one per board column, always in the DOM),
    // which also has an option labelled "Task" and would otherwise be
    // picked up by an unscoped `page.locator('select')` filter.
    const createModal = page.getByRole('dialog')
    await createModal.getByPlaceholder('Card title').fill(opts.title)
    await createModal.locator('select').filter({ has: page.locator('option', { hasText: 'Task' }) }).selectOption({ label: opts.type })
    await createModal.locator('select').filter({ has: page.locator('option', { hasText: opts.column ?? 'Backlog' }) }).selectOption({ label: opts.column ?? 'Backlog' })
    if (opts.dueDate) {
      await createModal.locator('input[type="date"]').fill(opts.dueDate)
    }
    if (opts.parent) {
      await createModal.locator('select').filter({ has: page.locator('option', { hasText: opts.parent }) }).selectOption({ label: opts.parent })
    }
    await createModal.getByRole('button', { name: 'Create', exact: true }).click()
    await expect(page.getByText('Card created', { exact: true }).first()).toBeVisible()
    await expect(page.getByRole('heading', { name: opts.title, exact: true })).toBeVisible()
  }

  // ---- 3. Create all five cards ----
  await createCard({ title: goalTitle, type: 'Goal' })
  await createCard({ title: ideaTitle, type: 'Idea' })
  await createCard({ title: issueTitle, type: 'Issue' })
  await createCard({ title: heroTaskTitle, type: 'Task', parent: goalTitle })
  await createCard({ title: emailTaskTitle, type: 'Task', dueDate: '2026-08-15' })

  const desktop = page.getByTestId('card-modal-desktop')

  async function openCard(title: string) {
    await page.getByRole('heading', { name: title, exact: true }).click()
    await expect(desktop).toBeVisible()
  }

  async function closeCard() {
    await page.keyboard.press('Escape')
    await expect(desktop).not.toBeVisible()
  }

  // ---- 4. Idea card: write a Concept spec ----
  await openCard(ideaTitle)
  await desktop.getByRole('tab', { name: 'Docs' }).click()
  await desktop.getByPlaceholder('Spec title').fill('Dark mode concept')
  await desktop.locator('.ProseMirror').click()
  await page.keyboard.type('Add a toggle in settings that flips a CSS class on <html>.')
  await desktop.getByRole('button', { name: 'Create', exact: true }).click()
  await expect(page.getByText('Spec saved', { exact: true }).first()).toBeVisible()

  // ---- 5. Flip Idea -> Task, confirm the Spec-hiding warning ----
  const typeSelect = desktop.getByRole('combobox').nth(0)
  await expect(typeSelect).toHaveText('Idea')
  await typeSelect.click()
  await page.getByRole('option', { name: 'Task', exact: true }).click()

  await expect(page.getByRole('heading', { name: 'Change card type?' })).toBeVisible()
  await expect(page.getByText(/This card has a Spec/)).toBeVisible()
  await page.getByRole('button', { name: 'Change Type' }).click()

  await expect(typeSelect).toHaveText('Task')
  await desktop.getByRole('tab', { name: 'Docs' }).click()
  await expect(desktop.getByPlaceholder('Spec title')).not.toBeVisible()
  await closeCard()

  // ---- 6. "Write launch announcement email": checklist, attachments, dependency, column move ----
  await openCard(emailTaskTitle)
  await desktop.getByRole('tab', { name: 'Checklist', exact: true }).click()

  // CardChecklist.vue is mounted twice inside card-modal-desktop: once as the
  // full "Checklist" tab body (.flex-1.pr-4), once as a visible-limit=4
  // condensed copy in the sidebar. Scope to the tab body to avoid matching
  // both live instances of the same aria-labels/placeholders.
  const checklistTab = desktop.locator('.flex-1.pr-4')

  async function addChecklistItem(text: string) {
    await checklistTab.getByPlaceholder('Add item...').fill(text)
    await checklistTab.getByRole('button', { name: 'Add', exact: true }).click()
    await expect(checklistTab.getByText(text, { exact: true })).toBeVisible()
  }

  await addChecklistItem('Draft copy')
  await addChecklistItem('Get approval from marketing')
  await addChecklistItem('Schedule send')

  await checklistTab.getByRole('button', { name: "Mark 'Draft copy' complete" }).click()
  await expect(checklistTab.getByRole('button', { name: "Mark 'Draft copy' incomplete" })).toBeVisible()

  // Move "Get approval from marketing" above "Draft copy"
  await checklistTab.locator('li', { hasText: 'Get approval from marketing' }).getByRole('button', { name: 'Move up' }).click()

  // Delete "Schedule send"
  await checklistTab.locator('li', { hasText: 'Schedule send' }).getByRole('button', { name: 'Delete item' }).click()
  await expect(checklistTab.getByText('Schedule send', { exact: true })).not.toBeVisible()

  // Attachments (sidebar, desktop only)
  await desktop.locator('input[type="file"]').setInputFiles({
    name: 'launch-notes.txt',
    mimeType: 'text/plain',
    buffer: Buffer.from('Launch checklist notes for the marketing site revamp.')
  })
  await expect(desktop.getByText('launch-notes.txt', { exact: true })).toBeVisible()

  // Dependencies: Blocked by -> "Update hero image assets"
  await desktop.getByRole('button', { name: 'Link card' }).click()
  await desktop.getByPlaceholder('Search cards...').fill('Update hero image')
  await desktop.getByRole('button', { name: new RegExp(heroTaskTitle) }).click()
  // Relationship type defaults to "Blocked by" — leave as-is.
  await desktop.getByRole('button', { name: 'Link', exact: true }).click()
  await expect(desktop.getByText('Blocked by', { exact: true })).toBeVisible()
  await expect(desktop.getByText(new RegExp(heroTaskTitle))).toBeVisible()

  // Column move: Backlog -> In Progress (avoids simulating native HTML5 drag-and-drop)
  const columnSelect = desktop.getByRole('combobox').nth(1)
  await expect(columnSelect).toHaveText('Backlog')
  await columnSelect.click()
  await page.getByRole('option', { name: 'In Progress', exact: true }).click()
  await expect(columnSelect).toHaveText('In Progress')

  await closeCard()

  // ---- 7. Parent/child visibility both directions ----
  await openCard(heroTaskTitle)
  await expect(desktop.getByText(new RegExp(`— ${goalTitle}`))).toBeVisible()
  await closeCard()

  await openCard(goalTitle)
  // Children chips render as "{Type} #{cardNumber}" only — no title (unlike
  // the Parent chip, which is "#{cardNumber} — {title}"). One child exists
  // here, so matching the type+number is enough to prove the link is live.
  await expect(desktop.getByText(/Task #\d+/)).toBeVisible()
  await closeCard()

  // ---- 8. Archive with dependents, then restore ----
  await openCard(emailTaskTitle)
  // Archive/Restore live in AppModal's #header-trailing slot — a sibling of
  // card-modal-desktop/-mobile, not inside either — so this stays unscoped.
  await page.getByTitle('Archive card').click()
  await expect(page.getByRole('heading', { name: 'Archive Card' })).toBeVisible()
  await expect(page.getByText('This card has relationships with other cards')).toBeVisible()
  await expect(page.getByText(heroTaskTitle).first()).toBeVisible()
  await page.getByRole('button', { name: 'Archive', exact: true }).click()
  await expect(page.getByText('Card archived', { exact: true }).first()).toBeVisible()
  await expect(page.getByRole('heading', { name: emailTaskTitle, exact: true })).not.toBeVisible()

  const archivedLabel = page.locator('label').filter({ hasText: 'Archived' }).first()
  await archivedLabel.locator('input[type="checkbox"]').check()
  await expect(page.getByRole('heading', { name: emailTaskTitle })).toBeVisible({ timeout: 10000 })
  const archivedOnlyLabel = page.locator('label').filter({ hasText: 'Archived only' }).first()
  await archivedOnlyLabel.locator('input[type="checkbox"]').check()

  await page.getByRole('heading', { name: emailTaskTitle }).click()
  await expect(desktop).toBeVisible()
  await page.getByTitle('Restore card').click()
  await expect(page.getByText('Card restored', { exact: true }).first()).toBeVisible()
  await closeCard()

  await archivedLabel.locator('input[type="checkbox"]').uncheck()

  // ---- 9. Description save (manual path) ----
  await openCard(issueTitle)
  const saveButton = desktop.getByRole('button', { name: 'Save', exact: true })
  await expect(saveButton).toBeDisabled()
  await desktop.locator('.ProseMirror').click()
  await page.keyboard.type('Repro: Safari 17, checkout throws 500 after applying a discount code.')
  await expect(saveButton).toBeEnabled()
  await saveButton.click()
  await expect(saveButton).toBeDisabled({ timeout: 10000 })
  await closeCard()

  // ---- 10. Cleanup: archive the whole project ----
  // Client-side nav (not page.goto/reload) — a hard reload this late in a
  // long session hits a pre-existing, unrelated bug where the client
  // re-checks auth on full navigation and gets redirected to /login (also
  // reproduces on plain `card-description-save.spec.ts`'s reload-based
  // test, so it's not something introduced here). Not part of what this
  // flow is meant to cover; worth its own investigation separately.
  await page.getByRole('link', { name: 'HydraForge' }).click()
  await expect(page).toHaveURL(/\/projects$/)
  await page.getByTestId('project-search-input').fill(projectName)
  const projectRow = page.getByRole('button', { name: new RegExp(projectName) })
  await expect(projectRow).toBeVisible({ timeout: 10000 })
  await projectRow.locator('button').last().click()
  await expect(page.getByRole('heading', { name: 'Archive project' })).toBeVisible()
  await page.getByRole('button', { name: 'Archive', exact: true }).click()
  await expect(page.getByText('Project archived', { exact: true }).first()).toBeVisible()
  await expect(projectRow).not.toBeVisible()
})
