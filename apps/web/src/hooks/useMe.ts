/*
 * useMe — fetches the current user's profile from GET /api/me.
 *
 * Only runs when Clerk has loaded AND the user is signed in.
 * Prevents a 401 flash on page load while Clerk resolves the session.
 */

import { useQuery } from '@tanstack/react-query';
import { useAuth } from '@clerk/react';
import type { Person } from '@/types/api';
import { useApi } from '@/lib/api';

export function useMe() {
  const { isLoaded, isSignedIn } = useAuth();
  const api = useApi();

  return useQuery<Person>({
    queryKey: ['me'],
    queryFn: () => api.get<Person>('/api/me'),
    enabled: isLoaded && isSignedIn === true,
    staleTime: 5 * 60 * 1000,
    retry: false,
  });
}
