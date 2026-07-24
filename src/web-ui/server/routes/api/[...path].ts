export default defineEventHandler((event) => {
  const config = useRuntimeConfig()
  return proxyRequest(event, `${config.apiBaseUrl}${event.path}`)
})
