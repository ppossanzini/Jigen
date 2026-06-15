import axios, { AxiosError, type AxiosInstance } from 'axios'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api'

class BaseRestService {
  protected readonly client: AxiosInstance

  constructor() {
    this.client = axios.create({
      baseURL: API_BASE_URL,
      withCredentials: true,
      timeout: 15000,
      headers: {
        'Content-Type': 'application/json',
      },
    })

    this.client.interceptors.request.use((config) => {
      const token = localStorage.getItem('jigen.auth.token')
      if (token) {
        config.headers.Authorization = `Bearer ${token}`
      }
      return config
    })
  }

  protected normalizeError(error: unknown): Error {
    const axiosError = error as AxiosError<{ detail?: string; message?: string }>
    const message = axiosError.response?.data?.detail || axiosError.response?.data?.message || axiosError.message || 'Request failed'

    return new Error(message)
  }
}

export { API_BASE_URL }
export default BaseRestService
