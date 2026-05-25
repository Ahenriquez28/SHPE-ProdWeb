/*
 * Card — white elevated surface with optional title and padding.
 *
 * Used for content sections, form panels, stat blocks.
 * Navy left border variant (bordered) is used for highlight callouts.
 */

import type { ReactNode } from 'react';

interface Props {
  title?: string;
  children: ReactNode;
  bordered?: boolean;
  className?: string;
}

export default function Card({ title, children, bordered = false, className = '' }: Props) {
  return (
    <div
      className={[
        'bg-surface rounded-xl shadow-sm p-5',
        bordered ? 'border-l-4 border-brand' : 'border border-border',
        className,
      ].join(' ')}
    >
      {title && (
        <h3 className="text-base font-semibold text-navy mb-3">{title}</h3>
      )}
      {children}
    </div>
  );
}
