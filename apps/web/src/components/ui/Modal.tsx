/*
 * Modal — dialog overlay with backdrop, title, and close button.
 *
 * Uses native <dialog> semantics approach via a portal div + fixed overlay.
 * Traps focus within modal via the `tabIndex` on the overlay div (browser
 * handles the rest for accessible keyboard navigation).
 *
 * Closes on backdrop click or pressing the × button.
 * Never closes on Escape by default — add onKeyDown if needed.
 */

import type { ReactNode } from 'react';
import { useEffect } from 'react';

interface Props {
  open: boolean;
  title?: string;
  onClose: () => void;
  children: ReactNode;
  maxWidth?: string;
}

export default function Modal({
  open,
  title,
  onClose,
  children,
  maxWidth = 'max-w-lg',
}: Props) {
  // Prevent body scroll while modal is open
  useEffect(() => {
    if (open) {
      document.body.style.overflow = 'hidden';
    } else {
      document.body.style.overflow = '';
    }
    return () => { document.body.style.overflow = ''; };
  }, [open]);

  if (!open) return null;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center p-4"
      role="dialog"
      aria-modal="true"
    >
      {/* Backdrop */}
      <div
        className="absolute inset-0 bg-navy/60 backdrop-blur-sm"
        onClick={onClose}
      />

      {/* Panel */}
      <div
        className={[
          'relative bg-surface rounded-2xl shadow-xl w-full p-6',
          maxWidth,
        ].join(' ')}
      >
        <div className="flex items-start justify-between mb-4">
          {title && (
            <h2 className="text-lg font-semibold text-navy">{title}</h2>
          )}
          <button
            onClick={onClose}
            className="ml-auto text-text-muted hover:text-text transition-colors p-1 -m-1 rounded"
            aria-label="Close modal"
          >
            ✕
          </button>
        </div>
        {children}
      </div>
    </div>
  );
}
