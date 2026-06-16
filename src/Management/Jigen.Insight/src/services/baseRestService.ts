import axios, { AxiosHeaders, type AxiosRequestConfig } from 'axios'

export class BaseRestService {
  protected readonly api = axios.create({
    baseURL: '/api',
    headers: {
      Accept: 'application/json',
      'Content-Type': 'application/json',
    },
  })

  constructor() {
    this.api.interceptors.request.use((config) => {
      const token = localStorage.getItem('auth.token') || sessionStorage.getItem('auth.token')

      if (!token) {
        return config
      }

      if (!config.headers) {
        config.headers = new AxiosHeaders()
      }

      config.headers.set('Authorization', `Bearer ${token}`)
      return config
    })
  }

  protected async post<TResponse, TPayload>(
    url: string,
    payload: TPayload,
    config?: AxiosRequestConfig,
  ): Promise<TResponse> {
    const response = await this.api.post<TResponse>(url, payload, config)
    return response.data
  }
}
