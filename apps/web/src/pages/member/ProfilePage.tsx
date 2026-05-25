/* Phase 1 stub — member profile editor. */
import { useMe } from '@/hooks/useMe';
import Card from '@/components/ui/Card';
import Skeleton from '@/components/ui/Skeleton';

export default function ProfilePage() {
  const { data: me, isLoading } = useMe();

  return (
    <div>
      <h1 className="text-2xl font-bold text-navy mb-6">My Profile</h1>
      <Card className="max-w-lg">
        {isLoading ? (
          <div className="space-y-3">
            <Skeleton className="w-full h-5" />
            <Skeleton className="w-3/4 h-5" />
          </div>
        ) : (
          <dl className="text-sm space-y-2">
            <div className="flex gap-2">
              <dt className="text-text-muted w-28 shrink-0">Name</dt>
              <dd className="text-text font-medium">{me?.fullName}</dd>
            </div>
            <div className="flex gap-2">
              <dt className="text-text-muted w-28 shrink-0">Email</dt>
              <dd className="text-text">{me?.email ?? '—'}</dd>
            </div>
            <div className="flex gap-2">
              <dt className="text-text-muted w-28 shrink-0">GSU Email</dt>
              <dd className="text-text">{me?.gsuEmail ?? '—'}</dd>
            </div>
            <div className="flex gap-2">
              <dt className="text-text-muted w-28 shrink-0">Grad Year</dt>
              <dd className="text-text">{me?.gradYear ?? '—'}</dd>
            </div>
          </dl>
        )}
        <p className="text-text-muted text-xs mt-4">Full profile editing form coming in Phase 1.</p>
      </Card>
    </div>
  );
}
