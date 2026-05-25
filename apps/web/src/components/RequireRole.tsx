/*
 * RequireRole — AUTH STUBBED. Always renders children regardless of role.
 * Restore role check via useMe() once Clerk auth is re-enabled.
 */

import type { ReactNode } from 'react';
import type { Role } from '@/types/api';

interface Props {
  roles: Role[];
  children: ReactNode;
}

export default function RequireRole({ roles: _roles, children }: Props) {
  return <>{children}</>;
}
