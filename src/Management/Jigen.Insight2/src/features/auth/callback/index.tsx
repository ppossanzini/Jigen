import { useEffect, useRef, useState } from 'react'
import { useNavigate, useSearch } from '@tanstack/react-router'
import { Loader2 } from 'lucide-react'
import { useAuthStore } from '@/stores/auth-store'
import { exchangeAuthorizationCode } from '@/lib/oidc'
import { AuthLayout } from '../auth-layout'

export function AuthCallback() {
  const search = useSearch({ from: '/auth/callback' })
  const navigate = useNavigate()
  const persistSession = useAuthStore((state) => state.auth.persistSession)

  // Derived synchronously from the URL, not fetched — doesn't belong in an effect.
  const immediateError = search.error
    ? search.error_description || search.error
    : !search.code || !search.state
      ? 'Missing authorization code.'
      : null

  const [exchangeError, setExchangeError] = useState<string | null>(null)
  const hasRun = useRef(false)

  useEffect(() => {
    // Guards against React StrictMode's double-invoke in dev, which would
    // otherwise consume the one-time authorization code twice.
    if (hasRun.current) return
    hasRun.current = true

    if (immediateError || !search.code || !search.state) return

    exchangeAuthorizationCode(search.code, search.state)
      .then((result) => {
        persistSession(result.token, result.userName, result.rememberMe, result.roles)
        navigate({ to: result.redirectTo || search.redirect || '/', replace: true })
      })
      .catch((err: unknown) => {
        setExchangeError(err instanceof Error ? err.message : 'Sign-in failed.')
      })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const error = immediateError || exchangeError

  return (
    <AuthLayout>
      <div className='flex flex-col items-center gap-4 text-center'>
        {error ? (
          <>
            <p className='text-destructive text-sm'>{error}</p>
            <button
              className='text-sm underline underline-offset-4'
              onClick={() => navigate({ to: '/sign-in' })}
            >
              Back to sign in
            </button>
          </>
        ) : (
          <>
            <Loader2 className='text-muted-foreground size-6 animate-spin' />
            <p className='text-muted-foreground text-sm'>Completing sign-in...</p>
          </>
        )}
      </div>
    </AuthLayout>
  )
}
