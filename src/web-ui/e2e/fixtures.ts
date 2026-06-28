import { test as base, expect, type APIRequestContext } from '@playwright/test'

const API_BASE_URL = process.env.E2E_API_BASE_URL ?? 'http://localhost:5116'

interface SeededCard {
  projectId: string
  columnId: string
  cardId: string
  cardTitle: string
}

async function loginForApiToken(request: APIRequestContext): Promise<string> {
  const response = await request.post(`${API_BASE_URL}/api/Auth/login`, {
    data: { username: 'testadmin', password: 'TestAdmin123!' }
  })
  if (!response.ok()) {
    throw new Error(`E2E API login failed: ${response.status()} ${await response.text()}`)
  }
  const body = await response.json() as { accessToken: string }
  return body.accessToken
}

export const test = base.extend<{ seedCard: SeededCard }>({
  seedCard: async ({ playwright }, use) => {
    const request = await playwright.request.newContext()
    const token = await loginForApiToken(request)
    const headers = { Authorization: `Bearer ${token}` }
    const suffix = `${Date.now()}-${Math.floor(Math.random() * 10000)}`

    const projectResponse = await request.post(`${API_BASE_URL}/api/Projects`, {
      headers,
      data: { name: `E2E Project ${suffix}`, description: 'Created by Playwright' }
    })
    if (!projectResponse.ok()) {
      throw new Error(`E2E project seed failed: ${projectResponse.status()} ${await projectResponse.text()}`)
    }
    const project = await projectResponse.json() as { id: string }

    const columnsResponse = await request.get(`${API_BASE_URL}/api/projects/${project.id}/Columns`, { headers })
    const columns = await columnsResponse.json() as { id: string }[]
    const columnId = columns[0].id

    const cardTitle = `E2E Card ${suffix}`
    const cardResponse = await request.post(`${API_BASE_URL}/api/projects/${project.id}/Cards`, {
      headers,
      data: { columnId, title: cardTitle, description: '', type: 'Task' }
    })
    if (!cardResponse.ok()) {
      throw new Error(`E2E card seed failed: ${cardResponse.status()} ${await cardResponse.text()}`)
    }
    const card = await cardResponse.json() as { id: string }

    await use({ projectId: project.id, columnId, cardId: card.id, cardTitle })
    await request.dispose()
  }
})

export { expect }