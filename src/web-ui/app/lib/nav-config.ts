import type { NavigationMenuItem } from '@nuxt/ui'
import { UiRoutes } from '~/lib/routes'

/**
 * Sidebar nav structure. Each group is an array whose first item is a
 * `type: 'label'` header, followed by real/disabled items. Disabled items
 * have no `to` — they render greyed and non-navigable until their feature
 * ships (System Settings: Plans 10-12; everything else: backlog).
 */
export function getNavGroups(isAdmin: boolean): NavigationMenuItem[][] {
  const groups: NavigationMenuItem[][] = [
    [
      { label: 'Workspace', type: 'label' },
      { label: 'Chats', icon: 'i-lucide-message-circle', to: UiRoutes.Chats },
      { label: 'Projects', icon: 'i-lucide-layout-dashboard', to: UiRoutes.Projects.List }
    ],
    [
      { label: 'AI Tools', type: 'label' },
      { label: 'Deep Research', icon: 'i-lucide-search', disabled: true },
      { label: 'Compare', icon: 'i-lucide-columns-2', disabled: true },
      { label: 'Cookbook', icon: 'i-lucide-book-open', disabled: true },
      { label: 'Automations', icon: 'i-lucide-workflow', disabled: true },
      { label: 'Prompt Library', icon: 'i-lucide-library', disabled: true }
    ],
    [
      { label: 'Creative', type: 'label' },
      { label: 'Gallery', icon: 'i-lucide-image', disabled: true },
      { label: 'Image Generator', icon: 'i-lucide-wand-2', disabled: true }
    ],
    [
      { label: 'Personal', type: 'label' },
      { label: 'Brain / Memory', icon: 'i-lucide-brain', disabled: true },
      { label: 'Tasks', icon: 'i-lucide-check-square', disabled: true },
      { label: 'Calendar', icon: 'i-lucide-calendar', disabled: true },
      { label: 'Documents / Library', icon: 'i-lucide-folder', disabled: true },
      { label: 'Theme', icon: 'i-lucide-palette', disabled: true },
      { label: 'Voice Notes', icon: 'i-lucide-mic', disabled: true }
    ]
  ]

  if (isAdmin) {
    groups.push([
      { label: 'Admin', type: 'label' },
      { label: 'Users', icon: 'i-lucide-users', to: UiRoutes.Admin.Users },
      { label: 'System Settings', icon: 'i-lucide-settings', to: UiRoutes.Admin.Settings },
      { label: 'Audit Log', icon: 'i-lucide-scroll-text', to: UiRoutes.Admin.AuditLog },
      { label: 'Reports', icon: 'i-lucide-bar-chart-3', disabled: true }
    ])
  }

  return groups
}
