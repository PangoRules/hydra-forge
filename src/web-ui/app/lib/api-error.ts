export class ApiError extends Error {
  status: number
  code: string
  title: string
  detail: string | null
  type: string
  correlationId: string

  constructor(status: number, code: string, title: string, detail: string | null, type: string, correlationId: string) {
    super(title)
    this.name = 'ApiError'
    this.status = status
    this.code = code
    this.title = title
    this.detail = detail
    this.type = type
    this.correlationId = correlationId
  }
}
