/*
 * useAuthSync — calls POST /api/auth/sync once per sign-in session.
 *
 * WHY THIS EXISTS:
 *   Clerk manages authentication but our database manages member data and roles.
 *   On first sign-in, we need to link the Clerk user ID to a Person row.
 *   On every subsequent sign-in, the sync is a fast no-op (returns existing profile).
 *
 * HOW IT WORKS:
 *   1. Waits for Clerk to load and the user to be signed in.
 *   2. Sends { email, fullName } to the backend so it can find/create the Person row.
 *   3. Uses a ref to fire exactly once per component mount — avoids double-calling on
 *      React StrictMode's double-render in development.
 *
 * CALLED FROM: App.tsx via the AuthSync component (inside ClerkProvider).
 */

import { useEffect, useRef } from 'react';
import { useAuth, useUser } from '@clerk/react';
import { useApi } from '@/lib/api';

export function useAuthSync() {
  const { isLoaded, isSignedIn } = useAuth();
  const { user } = useUser();
  const api = useApi();
  const hasSynced = useRef(false);

  useEffect(() => {
    // Wait until Clerk has resolved the session and the user is signed in
    if (!isLoaded || !isSignedIn || !user) return;

    // Prevent duplicate calls — once per mount is enough
    if (hasSynced.current) return;
    hasSynced.current = true;

    const email    = user.primaryEmailAddress?.emailAddress ?? '';
    const fullName = user.fullName ?? user.firstName ?? email;

    api.post('/api/auth/sync', { email, fullName }).catch(() => {
      // Allow retry on next render cycle if the request fails
      hasSynced.current = false;
    });
  // user.id changing means a different user signed in — re-sync
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isLoaded, isSignedIn, user?.id]);
}
