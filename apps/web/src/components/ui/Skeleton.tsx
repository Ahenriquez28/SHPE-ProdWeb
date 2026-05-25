/*
 * Skeleton — animated placeholder shown while content is loading.
 *
 * Pass width/height via className (e.g. className="w-32 h-4").
 * Defaults to full width, 1rem tall.
 */

export default function Skeleton({ className = '' }: { className?: string }) {
  return (
    <div
      className={[
        'animate-pulse rounded bg-cream-dark',
        className || 'w-full h-4',
      ].join(' ')}
    />
  );
}
