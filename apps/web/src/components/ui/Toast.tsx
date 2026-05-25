/*
 * Toast — lightweight in-app notification with auto-dismiss.
 *
 * Design decision: no external library. Simple useState + useEffect
 * pattern is enough for Phase 1. Upgrade to react-hot-toast when
 * the app needs queued notifications.
 *
 * Usage:
 *   const [toast, setToast] = useState<ToastState | null>(null);
 *   setToast({ type: 'success', message: 'Saved!' });
 *   <Toast toast={toast} onClose={() => setToast(null)} />
 */

import { useEffect } from 'react';

export type ToastState = {
  type: 'success' | 'error' | 'info';
  message: string;
};

const styles: Record<ToastState['type'], string> = {
  success: 'bg-green-600 text-white',
  error:   'bg-red-600 text-white',
  info:    'bg-navy text-cream',
};

const icons: Record<ToastState['type'], string> = {
  success: '✓',
  error:   '✕',
  info:    'ℹ',
};

interface Props {
  toast: ToastState | null;
  onClose: () => void;
  duration?: number;
}

export default function Toast({ toast, onClose, duration = 3500 }: Props) {
  useEffect(() => {
    if (!toast) return;
    const t = setTimeout(onClose, duration);
    return () => clearTimeout(t);
  }, [toast, onClose, duration]);

  if (!toast) return null;

  return (
    <div className="fixed bottom-6 right-6 z-50 animate-in slide-in-from-bottom-4">
      <div
        className={[
          'flex items-center gap-3 px-4 py-3 rounded-xl shadow-lg text-sm font-medium',
          styles[toast.type],
        ].join(' ')}
      >
        <span className="text-base font-bold">{icons[toast.type]}</span>
        {toast.message}
        <button
          onClick={onClose}
          className="ml-2 opacity-70 hover:opacity-100 transition-opacity"
          aria-label="Dismiss"
        >
          ✕
        </button>
      </div>
    </div>
  );
}
