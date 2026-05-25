/*
 * Badge — small pill label for roles, statuses, tags.
 *
 * Variants map to role colors:
 *   default  — gray
 *   admin    — navy
 *   brand    — SHPE orange (active, featured)
 *   success  — green (completed, attended)
 *   warning  — amber (pending)
 *   danger   — red (cancelled, error)
 */

type Variant = 'default' | 'admin' | 'brand' | 'success' | 'warning' | 'danger';

const variantClasses: Record<Variant, string> = {
  default: 'bg-cream-dark text-text-muted',
  admin:   'bg-navy text-cream',
  brand:   'bg-brand text-white',
  success: 'bg-green-100 text-green-800',
  warning: 'bg-amber-100 text-amber-800',
  danger:  'bg-red-100 text-red-800',
};

interface Props {
  label: string;
  variant?: Variant;
}

export default function Badge({ label, variant = 'default' }: Props) {
  return (
    <span
      className={[
        'inline-flex items-center px-2 py-0.5 rounded-full text-xs font-semibold',
        variantClasses[variant],
      ].join(' ')}
    >
      {label}
    </span>
  );
}
