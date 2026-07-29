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

  it('Admin group only exposes Users with a real route; the rest are disabled', () => {
    const groups = getNavGroups(true)
    const admin = groups.find(g => g[0]!.label === 'Admin')!
    const users = admin.find(i => i.label === 'Users')!
    const settings = admin.find(i => i.label === 'System Settings')!
    const auditLog = admin.find(i => i.label === 'Audit Log')!
    const reports = admin.find(i => i.label === 'Reports')!
    expect(users.to).toBe('/admin/users')
    expect(users.disabled).toBeUndefined()
    expect(settings.disabled).toBe(true)
    expect(auditLog.disabled).toBe(true)
    expect(reports.disabled).toBe(true)
  })
})
