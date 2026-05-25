/*
 * Button — base button component with variant + size support.
 *
 * Variants:
 *   primary  — SHPE orange, main CTAs
 *   secondary — navy outline, secondary actions
 *   ghost    — no background, subtle actions
 *   danger   — red, destructive actions
 *
 * Passes all native <button> props through (onClick, disabled, type, etc.)
 */

import type { ButtonHTMLAttributes } from 'react';

type Variant = 'primary' | 'secondary' | 'ghost' | 'danger';
type Size    = 'sm' | 'md' | 'lg';

interface Props extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant;
  size?: Size;
  loading?: boolean;
}

const variantClasses: Record<Variant, string> = {
  primary:
    'bg-brand hover:bg-brand-dark text-white border border-transparent',
  secondary:
    'bg-transparent hover:bg-navy/5 text-navy border border-navy',
  ghost:
    'bg-transparent hover:bg-cream-dark text-text-muted border border-transparent',
  danger:
    'bg-red-600 hover:bg-red-700 text-white border border-transparent',
};

const sizeClasses: Record<Size, string> = {
  sm: 'text-xs px-3 py-1.5 rounded',
  md: 'text-sm px-4 py-2 rounded-lg',
  lg: 'text-base px-6 py-3 rounded-xl',
};

export default function Button({
  variant = 'primary',
  size = 'md',
  loading = false,
  disabled,
  children,
  className = '',
  ...props
}: Props) {
  return (
    <button
      {...props}
      disabled={disabled || loading}
      className={[
        'font-semibold transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand focus-visible:ring-offset-2',
        'disabled:opacity-50 disabled:cursor-not-allowed',
        variantClasses[variant],
        sizeClasses[size],
        className,
      ].join(' ')}
    >
      {loading ? (
        <span className="inline-flex items-center gap-2">
          <span className="w-3.5 h-3.5 border-2 border-current border-t-transparent rounded-full animate-spin" />
          {children}
        </span>
      ) : (
        children
      )}
    </button>
  );
}
