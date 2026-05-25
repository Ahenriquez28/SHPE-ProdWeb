/*
 * Spinner — animated loading indicator.
 *
 * Sizes: sm (16px), md (24px), lg (40px).
 * Color inherits from parent text color via `border-current`.
 */

type Size = 'sm' | 'md' | 'lg';

const sizeClasses: Record<Size, string> = {
  sm: 'w-4 h-4 border-2',
  md: 'w-6 h-6 border-2',
  lg: 'w-10 h-10 border-[3px]',
};

export default function Spinner({ size = 'md' }: { size?: Size }) {
  return (
    <span
      role="status"
      aria-label="Loading"
      className={[
        'inline-block rounded-full border-current border-t-transparent animate-spin',
        sizeClasses[size],
      ].join(' ')}
    />
  );
}
