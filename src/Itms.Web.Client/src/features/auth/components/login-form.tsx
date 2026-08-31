import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { LogIn } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useLogin } from '@/features/auth/hooks/use-login'
import { ApiError } from '@/lib/api/client'
import { cn } from '@/lib/utils'

/**
 * Deliberately weak, and matched to `LoginValidator` on the server: it asserts that
 * something was entered and nothing absurd. Checking a typed password against the
 * password policy here would tell an attacker which guesses were even eligible.
 */
const loginSchema = z.object({
  userName: z
    .string()
    .min(1, 'Enter your user name or email address.')
    .max(320, 'That is too long to be a user name or an email address.'),
  password: z.string().min(1, 'Enter your password.').max(256, 'That password is too long.'),
})

type LoginFormValues = z.infer<typeof loginSchema>

interface LoginFormProps {
  /** Called once the session cookie is in place. */
  onSignedIn: () => void
}

export function LoginForm({ onSignedIn }: LoginFormProps): React.JSX.Element {
  const signIn = useLogin()
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { userName: '', password: '' },
  })

  // Anything the server said that is not about one field: bad credentials, a locked
  // account, too many attempts. Held outside react-hook-form because it belongs to the
  // form as a whole.
  const formError = toFormError(signIn.error)

  const onSubmit = handleSubmit(async (values) => {
    try {
      await signIn.mutateAsync(values)
      onSignedIn()
    } catch (error) {
      if (error instanceof ApiError) {
        // The server's per-field messages win over the client's: it validated what was
        // actually received (CONVENTIONS.md, Forms).
        for (const [field, messages] of Object.entries(error.fieldErrors)) {
          if (field === 'userName' || field === 'password') {
            setError(field, { type: 'server', message: messages[0] ?? 'That value was rejected.' })
          }
        }
      }
    }
  })

  return (
    <form onSubmit={(event) => void onSubmit(event)} noValidate className="flex flex-col gap-4">
      <div
        // The one place a sign-in failure is announced. aria-live so a screen reader
        // hears it without the focus having to move.
        aria-live="assertive"
        className={cn(
          'rounded-lg border border-danger/30 bg-danger/8 px-3 py-2 text-caption text-danger',
          !formError && 'hidden',
        )}
      >
        {formError}
      </div>

      <div className="flex flex-col gap-1.5">
        <Label htmlFor="userName" className="text-field-label font-medium text-heading">
          User name or email <span className="text-danger">*</span>
        </Label>
        <Input
          id="userName"
          autoComplete="username"
          autoFocus
          aria-invalid={errors.userName ? true : undefined}
          aria-describedby={errors.userName ? 'userName-error' : undefined}
          className="h-10"
          {...register('userName')}
        />
        {errors.userName ? (
          <p id="userName-error" className="text-caption text-danger">
            {errors.userName.message}
          </p>
        ) : null}
      </div>

      <div className="flex flex-col gap-1.5">
        <Label htmlFor="password" className="text-field-label font-medium text-heading">
          Password <span className="text-danger">*</span>
        </Label>
        <Input
          id="password"
          type="password"
          autoComplete="current-password"
          aria-invalid={errors.password ? true : undefined}
          aria-describedby={errors.password ? 'password-error' : undefined}
          className="h-10"
          {...register('password')}
        />
        {errors.password ? (
          <p id="password-error" className="text-caption text-danger">
            {errors.password.message}
          </p>
        ) : null}
      </div>

      <Button type="submit" size="lg" className="mt-2 h-10 w-full" disabled={isSubmitting}>
        <LogIn className="size-4" aria-hidden="true" />
        {isSubmitting ? 'Signing in…' : 'Sign in'}
      </Button>
    </form>
  )
}

function toFormError(error: unknown): string | null {
  if (!(error instanceof ApiError)) {
    return error ? 'Sign-in could not be completed. Check your connection and try again.' : null
  }

  if (error.status === 429) {
    return 'Too many sign-in attempts from this address. Wait a moment and try again.'
  }

  // A validation failure with field errors is rendered on the fields themselves.
  if (error.status === 400 && Object.keys(error.fieldErrors).length > 0) {
    return null
  }

  return error.message
}
