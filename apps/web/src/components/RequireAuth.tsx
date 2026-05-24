/*
 * RequireAuth — enforces authentication on any route it wraps.
 *
 * Uses the hook-based API (useAuth + useClerk) instead of the deprecated
 * <SignedIn> / <RedirectToSignIn> components, which were removed in Clerk v6.
 *
 * Behaviour:
 *   - While Clerk is loading: show a neutral full-screen loader (avoid flash).
 *   - Not signed in: redirect to Clerk's hosted sign-in page, returning here after.
 *   - Signed in: render children normally.
 */

import { useEffect, type ReactNode } from 'react';
import { useAuth, useClerk } from '@clerk/react';

export default function RequireAuth({ children }: { children: ReactNode }) {
  const { isLoaded, isSignedIn } = useAuth();
  const { redirectToSignIn } = useClerk();

  useEffect(() => {
    if (isLoaded && !isSignedIn) {
      // redirectToSignIn sends the user to Clerk's hosted UI and brings them
      // back to the current URL after they authenticate.
      redirectToSignIn({ redirectUrl: window.location.href });
    }
  }, [isLoaded, isSignedIn]);

  if (!isLoaded) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-cream">
        <div className="text-navy/30 text-sm font-medium tracking-wide">Loading…</div>
      </div>
    );
  }

  if (!isSignedIn) return null;

  return <>{children}</>;
}
