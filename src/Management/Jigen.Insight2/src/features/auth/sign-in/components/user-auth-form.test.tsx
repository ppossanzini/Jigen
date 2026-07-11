import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, type RenderResult } from 'vitest-browser-react'
import { type Locator, userEvent } from 'vitest/browser'
import { UserAuthForm } from './user-auth-form'

const FORM_MESSAGES = {
  userNameEmpty: 'Please enter your username.',
  passwordEmpty: 'Please enter your password.',
} as const

const postMock = vi.fn()
const startAuthorizationCodeFlowMock = vi.fn()
const toastErrorMock = vi.fn()

vi.mock('@/lib/api-client', () => ({
  apiClient: { post: (...args: unknown[]) => postMock(...args) },
}))

vi.mock('@/lib/oidc', () => ({
  startAuthorizationCodeFlow: (...args: unknown[]) =>
    startAuthorizationCodeFlowMock(...args),
}))

vi.mock('sonner', () => ({
  toast: { error: (...args: unknown[]) => toastErrorMock(...args) },
}))

describe('UserAuthForm', () => {
  let screen: RenderResult
  let userNameInput: Locator
  let passwordInput: Locator
  let rememberMeCheckbox: Locator
  let signInButton: Locator

  beforeEach(async () => {
    vi.clearAllMocks()
    postMock.mockResolvedValue({ status: 204 })
    screen = await render(<UserAuthForm redirectTo='/databases' />)
    userNameInput = screen.getByRole('textbox', { name: /^Username$/i })
    passwordInput = screen.getByLabelText(/^Password$/i)
    rememberMeCheckbox = screen.getByRole('checkbox', { name: /Remember me/i })
    signInButton = screen.getByRole('button', { name: /^Sign in$/i })
  })

  it('renders username, password, remember me and submit button', async () => {
    await expect.element(userNameInput).toBeInTheDocument()
    await expect.element(passwordInput).toBeInTheDocument()
    await expect.element(rememberMeCheckbox).toBeInTheDocument()
    await expect.element(signInButton).toBeInTheDocument()
  })

  it('defaults remember me to checked', async () => {
    await expect.element(rememberMeCheckbox).toBeChecked()
  })

  it('shows validation messages when submitting an empty form', async () => {
    await userEvent.click(signInButton)

    await expect
      .element(screen.getByText(FORM_MESSAGES.userNameEmpty))
      .toBeInTheDocument()
    await expect
      .element(screen.getByText(FORM_MESSAGES.passwordEmpty))
      .toBeInTheDocument()
  })

  it('logs in and starts the authorization code flow on success', async () => {
    await userEvent.fill(userNameInput, 'guest')
    await userEvent.fill(passwordInput, 'P@ssw0rd!')

    await userEvent.click(signInButton)

    await vi.waitFor(() => expect(postMock).toHaveBeenCalledOnce())
    expect(postMock).toHaveBeenCalledWith('/identity/login', {
      userName: 'guest',
      password: 'P@ssw0rd!',
    })

    await vi.waitFor(() =>
      expect(startAuthorizationCodeFlowMock).toHaveBeenCalledWith({
        userName: 'guest',
        rememberMe: true,
        redirectTo: '/databases',
      })
    )
  })

  it('unchecking remember me is reflected in the authorization request', async () => {
    await userEvent.fill(userNameInput, 'guest')
    await userEvent.fill(passwordInput, 'P@ssw0rd!')
    await userEvent.click(rememberMeCheckbox)

    await userEvent.click(signInButton)

    await vi.waitFor(() =>
      expect(startAuthorizationCodeFlowMock).toHaveBeenCalledWith(
        expect.objectContaining({ rememberMe: false })
      )
    )
  })

  it('shows an error toast and resets loading state when login fails', async () => {
    postMock.mockRejectedValueOnce(new Error('401'))

    await userEvent.fill(userNameInput, 'guest')
    await userEvent.fill(passwordInput, 'wrong-password')
    await userEvent.click(signInButton)

    await vi.waitFor(() =>
      expect(toastErrorMock).toHaveBeenCalledWith('Invalid username or password.')
    )
    expect(startAuthorizationCodeFlowMock).not.toHaveBeenCalled()
    await expect.element(signInButton).not.toBeDisabled()
  })
})
