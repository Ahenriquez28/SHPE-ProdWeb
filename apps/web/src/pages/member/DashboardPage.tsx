/*
 * DashboardPage — member home screen after sign-in.
 *
 * Phase 1 stub: shows the user's name and roles from /api/me.
 * Real stats (events attended, upcoming, points) added in Phase 1.
 */

import { useMe } from '@/hooks/useMe';
import Skeleton from '@/components/ui/Skeleton';
import Badge from '@/components/ui/Badge';
import Card from '@/components/ui/Card';
import type { Role } from '@/types/api';

function roleBadgeVariant(role: Role) {
  if (role === 'super_admin' || role === 'admin') return 'admin' as const;
  if (role === 'eboard' || role === 'dev_team')   return 'brand' as const;
  return 'default' as const;
}

export default function DashboardPage() {
  const { data: me, isLoading } = useMe();

  return (
    <div>
      <h1 className="text-2xl font-bold text-navy mb-6">
        {isLoading ? (
          <Skeleton className="w-48 h-7" />
        ) : (
          `Welcome, ${me?.fullName ?? 'Member'} 👋`
        )}
      </h1>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <Card title="Your Roles">
          {isLoading ? (
            <Skeleton className="w-24 h-5" />
          ) : (
            <div className="flex flex-wrap gap-2">
              {me?.roles.map((r) => (
                <Badge key={r} label={r.replace('_', ' ')} variant={roleBadgeVariant(r)} />
              ))}
            </div>
          )}
        </Card>

        <Card title="Quick Links">
          <ul className="text-sm text-text-muted space-y-1">
            <li>📅 <a href="/events" className="hover:text-brand transition-colors">Upcoming Events</a></li>
            <li>👤 <a href="/profile" className="hover:text-brand transition-colors">Edit Profile</a></li>
            <li>📋 <a href="/directory" className="hover:text-brand transition-colors">Member Directory</a></li>
          </ul>
        </Card>
      </div>
    </div>
  );
}
