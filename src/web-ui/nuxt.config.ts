// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  modules: [
    '@nuxt/eslint',
    '@nuxt/ui',
    '@pinia/nuxt'
  ],

  devtools: {
    enabled: true
  },

  css: ['~/assets/css/main.css'],

  runtimeConfig: {
    // Private — server-side only. Used by the /api/[...path] proxy route.
    apiBaseUrl: process.env.NUXT_API_BASE_URL ?? 'http://localhost:5000',
    public: {
      // Empty string → browser sends relative /api/... requests to Nuxt server,
      // which proxies them via server/routes/api/[...path].ts. No CORS ever.
      apiBaseUrl: '',
      authCookieMaxAge: parseInt(process.env.NUXT_PUBLIC_AUTH_COOKIE_MAX_AGE ?? '3600', 10),
      authCookieSecure: process.env.NUXT_PUBLIC_AUTH_COOKIE_SECURE === 'true'
    }
  },

  routeRules: {
    '/': { prerender: true }
  },

  compatibilityDate: '2025-01-15',

  nitro: {
    devProxy: {
      '/hubs': {
        target: process.env.NUXT_API_BASE_URL ?? 'http://localhost:5000',
        ws: true,
        changeOrigin: true
      }
    }
  },

  eslint: {
    config: {
      stylistic: {
        commaDangle: 'never',
        braceStyle: '1tbs'
      }
    }
  }
})
