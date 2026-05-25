/*
 * Input — labeled text input with optional error state.
 *
 * Wraps a <label> + <input> pair so the label is always associated
 * (accessibility — clicking the label focuses the input).
 *
 * Pass `error` to show a red border + error message below.
 */

import type { InputHTMLAttributes } from 'react';

interface Props extends InputHTMLAttributes<HTMLInputElement> {
  label?: string;
  error?: string;
}

export default function Input({ label, error, id, className = '', ...props }: Props) {
  const inputId = id ?? label?.toLowerCase().replace(/\s+/g, '-');

  return (
    <div className="flex flex-col gap-1">
      {label && (
        <label
          htmlFor={inputId}
          className="text-sm font-medium text-navy"
        >
          {label}
        </label>
      )}
      <input
        id={inputId}
        {...props}
        className={[
          'w-full rounded-lg border px-3 py-2 text-sm bg-surface text-text',
          'placeholder:text-text-muted',
          'focus:outline-none focus:ring-2 focus:ring-brand focus:ring-offset-1',
          'transition-colors',
          error
            ? 'border-red-500 focus:ring-red-500'
            : 'border-border hover:border-navy-muted',
          className,
        ].join(' ')}
      />
      {error && <p className="text-xs text-red-600">{error}</p>}
    </div>
  );
}
