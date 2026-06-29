import { defineConfig, devices } from '@playwright/test'

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? 'github' : 'list',
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:3000',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure'
  },
  projects: [
    {
      name: 'setup',
      testMatch: /.*\.setup\.ts/
    },
    {
      name: 'chromium',
      use: {
        ...devices['Desktop Chrome'],
        storageState: 'e2e/.auth/testadmin.json'
      },
      dependencies: ['setup']
    }
  ],
  webServer: process.env.CI
    ? undefined
    : [
        {
          command: 'dotnet run --project ../../src/HydraForge.Server',
          url: 'http://localhost:5000/health',
          reuseExistingServer: true,
          timeout: 60000
        },
        {
          command: 'pnpm dev',
          url: 'http://localhost:3000',
          reuseExistingServer: true,
          timeout: 30000
        }
      ]
})
