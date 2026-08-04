import { addCollection } from '@iconify/vue'
import lucide from '@iconify-json/lucide/icons.json'
import simpleIcons from '@iconify-json/simple-icons/icons.json'

/**
 * @nuxt/icon's dev-mode `local` server bundle resolves icons via a self-fetch
 * back to /api/_nuxt_icon/{collection}.json on first use. The very first SSR
 * render of a page with many <Icon> components (e.g. the sidebar) fires that
 * self-fetch for every icon at once and loses the race intermittently,
 * logging "[Icon] failed to load icon" even though the icon exists. Loading
 * both collections into iconify's registry up front means every icon lookup
 * hits the in-memory cache instead, so the self-fetch path is never taken.
 * Server-only: this never ships to the client bundle.
 */
export default defineNuxtPlugin(() => {
  addCollection(lucide)
  addCollection(simpleIcons)
})
