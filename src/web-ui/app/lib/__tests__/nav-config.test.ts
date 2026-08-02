import { describe, it, expect } from 'vitest'
import { getNavGroups } from '~/lib/nav-config'

describe('nav-config', () => {
  it('includes Workspace, AI Tools, Creative, Personal groups for a non-admin user', () => {
    const groups = getNavGroups(false)
    const groupLabels = groups.map(g => g[0]!.label)
    expect(groupLabels).toEqual(['Workspace', 'AI Tools', 'Creative', 'Personal'])
  })

  it('adds an Admin group only for admin users', () => {
    const nonAdminGroups = getNavGroups(false)
    const adminGroups = getNavGroups(true)
    expect(nonAdminGroups.some(g => g[0]!.label === 'Admin')).toBe(false)
    expect(adminGroups.some(g => g[0]!.label === 'Admin')).toBe(true)
  })

  it('Chats and Projects are enabled with real routes', () => {
    const [workspace] = getNavGroups(false)
    const chats = workspace!.find(i => i.label === 'Chats')!
    const projects = workspace!.find(i => i.label === 'Projects')!
    expect(chats.disabled).toBeUndefined()
    expect(chats.to).toBe('/chats')
    expect(projects.disabled).toBeUndefined()
    expect(projects.to).toBe('/projects')
  })

  it('backlog items are disabled with no route', () => {
    const groups = getNavGroups(false)
    const aiTools = groups.find(g => g[0]!.label === 'AI Tools')!
    const deepResearch = aiTools.find(i => i.label === 'Deep Research')!
    expect(deepResearch.disabled).toBe(true)
    expect(deepResearch.to).toBeUndefined()
  })

  it('Admin group exposes Dashboard, Users, System Settings, Audit Log, Providers, Provider Models, Routing and Usage with real routes', () => {
    const groups = getNavGroups(true)
    const admin = groups.find(g => g[0]!.label === 'Admin')!
    const dashboard = admin.find(i => i.label === 'Dashboard')!
    const users = admin.find(i => i.label === 'Users')!
    const settings = admin.find(i => i.label === 'System Settings')!
    const auditLog = admin.find(i => i.label === 'Audit Log')!
    const providers = admin.find(i => i.label === 'Providers')!
    const providerModels = admin.find(i => i.label === 'Provider Models')!
    const routing = admin.find(i => i.label === 'Routing')!
    const usage = admin.find(i => i.label === 'Usage')!
    expect(dashboard.to).toBe('/admin')
    expect(dashboard.disabled).toBeUndefined()
    expect(dashboard.icon).not.toBe(users.icon)
    expect(users.to).toBe('/admin/users')
    expect(users.disabled).toBeUndefined()
    expect(settings.to).toBe('/admin/settings')
    expect(settings.disabled).toBeUndefined()
    expect(auditLog.to).toBe('/admin/audit-log')
    expect(auditLog.disabled).toBeUndefined()
    expect(providers.to).toBe('/admin/providers')
    expect(providers.disabled).toBeUndefined()
    expect(providerModels.to).toBe('/admin/provider-models')
    expect(providerModels.disabled).toBeUndefined()
    expect(routing.to).toBe('/admin/routing')
    expect(routing.disabled).toBeUndefined()
    expect(usage.to).toBe('/admin/usage')
    expect(usage.disabled).toBeUndefined()
    expect(admin.find(i => i.label === 'Reports')).toBeUndefined()
  })

  it('Dashboard and Projects icons do not collide', () => {
    const groups = getNavGroups(true)
    const workspace = groups.find(g => g[0]!.label === 'Workspace')!
    const admin = groups.find(g => g[0]!.label === 'Admin')!
    const projects = workspace.find(i => i.label === 'Projects')!
    const dashboard = admin.find(i => i.label === 'Dashboard')!
    expect(projects.icon).not.toBe(dashboard.icon)
  })

  it('Projects stays active while viewing a project board (sibling route, not nested)', () => {
    const onBoard = getNavGroups(false, '/projects/abc-123/board')
    const projectsOnBoard = onBoard[0]!.find(i => i.label === 'Projects')!
    expect(projectsOnBoard.active).toBe(true)

    const onList = getNavGroups(false, '/projects')
    const projectsOnList = onList[0]!.find(i => i.label === 'Projects')!
    expect(projectsOnList.active).toBe(true)

    const elsewhere = getNavGroups(false, '/chats')
    const projectsElsewhere = elsewhere[0]!.find(i => i.label === 'Projects')!
    expect(projectsElsewhere.active).toBe(false)
  })
})
