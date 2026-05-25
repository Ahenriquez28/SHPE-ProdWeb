/*
 * AdminLayout — thin wrapper used by admin-only sub-routes.
 *
 * Doesn't duplicate the sidebar (MemberLayout already renders it).
 * Just gates the subtree to admin/super_admin roles and renders Outlet.
 * This layout is nested inside MemberLayout in the router.
 */

import { Outlet } from 'react-router-dom';
import RequireRole from '@/components/RequireRole';

export default function AdminLayout() {
  return (
    <RequireRole roles={['admin', 'super_admin']}>
      <Outlet />
    </RequireRole>
  );
}
