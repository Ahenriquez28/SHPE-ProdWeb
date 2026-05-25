/*
 * AdminCheckInPage — event check-in via Aztec/QR scan or manual email lookup.
 *
 * Scanner logic ported from PantherScan (PinWorkAround/index.html):
 *  - ZXing BrowserMultiFormatReader handles Aztec, QR, DataMatrix
 *  - Scan → extractIssuanceId (UUID or JSON envelope) → demo lookup
 *  - 4-second debounce prevents duplicate scans of the same code
 *  - Already-checked-in students shown with error state
 *
 * Manual sign-in: asks for student GSU email only (no UUID needed by operator).
 */

import { useState, useRef, useCallback, useEffect } from 'react';
import { BrowserMultiFormatReader } from '@zxing/browser';

/* ─── Types ─────────────────────────────────────────────────────── */

interface StudentInfo {
  firstName: string;
  lastName:  string;
  email:     string;
  issuanceId: string;
}

interface LogEntry extends StudentInfo {
  time: Date;
}

type ResultState = 'idle' | 'loading' | 'found' | 'already_in' | 'confirmed';

/* ─── Demo lookup helpers ────────────────────────────────────────── */

const KNOWN_IDS: Record<string, Omit<StudentInfo, 'issuanceId'>> = {
  '1f9eb204-6113-47cc-8e20-f1cc23a27bf3': {
    firstName: 'Alexander',
    lastName:  'Henriquez',
    email:     'ahenriquez2@student.gsu.edu',
  },
};

function demoLookupById(issuanceId: string): StudentInfo {
  const known = KNOWN_IDS[issuanceId.toLowerCase()];
  if (known) return { ...known, issuanceId };
  const short = issuanceId.slice(0, 6).toUpperCase();
  return { firstName: 'Panther', lastName: short, email: `panther_${issuanceId.slice(0, 8)}@student.gsu.edu`, issuanceId };
}

function demoLookupByEmail(email: string): StudentInfo {
  const known = Object.values(KNOWN_IDS).find(s => s.email === email.trim().toLowerCase());
  if (known) return { ...known, issuanceId: 'manual' };
  // Derive a plausible first/last from email prefix (best-effort)
  const prefix = email.split('@')[0].replace(/[0-9]/g, '');
  const parts   = prefix.split(/[._-]/).filter(Boolean);
  const firstName = parts[0] ? parts[0].charAt(0).toUpperCase() + parts[0].slice(1) : 'GSU';
  const lastName  = parts[1] ? parts[1].charAt(0).toUpperCase() + parts[1].slice(1) : 'Student';
  return { firstName, lastName, email: email.trim().toLowerCase(), issuanceId: 'manual-' + Date.now() };
}

function extractIssuanceId(raw: string): string | null {
  try {
    const parsed = JSON.parse(raw);
    if (parsed.issuanceId) return parsed.issuanceId;
  } catch { /* not JSON */ }
  const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
  if (uuid.test(raw.trim())) return raw.trim();
  return null;
}

/* ─── Component ─────────────────────────────────────────────────── */

export default function AdminCheckInPage() {
  const videoRef   = useRef<HTMLVideoElement>(null);
  // Store scanner controls for cleanup — type inferred from @zxing/browser
  const controlsRef = useRef<{ stop: () => void } | null>(null);
  const lastIdRef   = useRef<string | null>(null);
  const checkedInIds = useRef<Set<string>>(new Set());

  const [scanning,   setScanning]   = useState(false);
  const [camError,   setCamError]   = useState<string | null>(null);
  const [student,    setStudent]    = useState<StudentInfo | null>(null);
  const [resultState, setResultState] = useState<ResultState>('idle');
  const [log,        setLog]        = useState<LogEntry[]>([]);
  const [lastTime,   setLastTime]   = useState<string>('—');
  const [showManual, setShowManual] = useState(false);
  const [manualEmail, setManualEmail] = useState('');
  const [manualLoading, setManualLoading] = useState(false);

  /* Cleanup scanner on unmount */
  useEffect(() => () => { controlsRef.current?.stop(); }, []);

  /* ── Camera control ────────────────────────────────────────────── */

  const startScanner = useCallback(async () => {
    setCamError(null);
    setScanning(true);
    try {
      const devices = await BrowserMultiFormatReader.listVideoInputDevices();
      if (!devices.length) throw new Error('No camera found on this device.');
      // Prefer environment-facing (rear) camera on mobile
      const back = devices.find(d => /back|rear|environment/i.test(d.label));
      const deviceId = back?.deviceId ?? devices[devices.length - 1].deviceId;

      const reader = new BrowserMultiFormatReader();
      controlsRef.current = await reader.decodeFromVideoDevice(
        deviceId,
        videoRef.current!,
        (result) => { if (result) handleScan(result.getText()); }
      );
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Camera unavailable';
      setCamError(msg);
      setScanning(false);
    }
  }, []);

  const stopScanner = useCallback(() => {
    controlsRef.current?.stop();
    controlsRef.current = null;
    if (videoRef.current?.srcObject) {
      (videoRef.current.srcObject as MediaStream).getTracks().forEach(t => t.stop());
      videoRef.current.srcObject = null;
    }
    setScanning(false);
  }, []);

  /* ── Scan handler ──────────────────────────────────────────────── */

  const handleScan = useCallback((raw: string) => {
    const id = extractIssuanceId(raw);
    if (!id) return;
    if (id === lastIdRef.current) return;               // debounce
    lastIdRef.current = id;
    setTimeout(() => { lastIdRef.current = null; }, 4000);

    setResultState('loading');
    setStudent(null);

    // Simulate async lookup (800 ms for UX feel — replace with real API call in Phase 1)
    setTimeout(() => {
      const found = demoLookupById(id);
      const t = new Date().toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
      setLastTime(t.slice(0, 5));
      setStudent(found);
      setResultState(checkedInIds.current.has(id) ? 'already_in' : 'found');
    }, 800);
  }, []);

  /* ── Confirm check-in ──────────────────────────────────────────── */

  const confirmCheckIn = useCallback(() => {
    if (!student || checkedInIds.current.has(student.issuanceId)) return;
    checkedInIds.current.add(student.issuanceId);
    const entry: LogEntry = { ...student, time: new Date() };
    setLog(prev => [entry, ...prev]);
    setResultState('confirmed');
  }, [student]);

  /* ── Manual email lookup ───────────────────────────────────────── */

  const handleManualSubmit = useCallback(async (e: React.FormEvent) => {
    e.preventDefault();
    if (!manualEmail.trim()) return;
    setManualLoading(true);
    // Simulate network delay
    await new Promise(r => setTimeout(r, 700));
    const found = demoLookupByEmail(manualEmail);
    setStudent(found);
    setResultState(checkedInIds.current.has(found.issuanceId) ? 'already_in' : 'found');
    setLastTime(new Date().toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit' }));
    setManualLoading(false);
    setShowManual(false);
    setManualEmail('');
  }, [manualEmail]);

  /* ── Helpers ───────────────────────────────────────────────────── */

  const initials = student
    ? (student.firstName[0] + student.lastName[0]).toUpperCase()
    : '?';

  const scanStatusColor =
    resultState === 'confirmed' || resultState === 'found' ? 'text-green-600' :
    resultState === 'already_in'                           ? 'text-brand-dark' :
    resultState === 'loading'                              ? 'text-shpe-blue'  :
    'text-text-muted';

  const scanStatusText =
    resultState === 'found'     ? '✓ Identity Confirmed'    :
    resultState === 'already_in'? '⚠ Already Checked In'   :
    resultState === 'loading'   ? 'Looking up student...'   :
    resultState === 'confirmed' ? '✓ Checked In Successfully' :
    'Awaiting scan…';

  return (
    <div>
      {/* ── Header ─────────────────────────────────────────────── */}
      <div className="flex items-center justify-between mb-6 flex-wrap gap-3">
        <div>
          <h1 className="text-2xl font-bold text-navy">Event Check-In</h1>
          <p className="text-sm text-text-muted mt-0.5">Scan student ID badge or enter email manually</p>
        </div>
        <button
          onClick={() => setShowManual(true)}
          className="bg-shpe-blue hover:bg-navy text-white text-sm font-semibold px-4 py-2.5 rounded-xl transition-colors flex items-center gap-2"
        >
          ✏ Manual Sign-In
        </button>
      </div>

      {/* ── Stats bar ──────────────────────────────────────────── */}
      <div className="grid grid-cols-3 gap-3 mb-6">
        {[
          { label: 'Checked In', value: log.length.toString() },
          { label: 'Last Scan',  value: lastTime },
          { label: 'Format',     value: 'AZTEC/QR' },
        ].map(s => (
          <div key={s.label} className="bg-surface rounded-xl border border-border p-4 text-center">
            <p className="text-2xl font-extrabold text-navy leading-none">{s.value}</p>
            <p className="text-xs text-text-muted uppercase tracking-wider mt-1 font-semibold">{s.label}</p>
          </div>
        ))}
      </div>

      {/* ── Main grid ─────────────────────────────────────────── */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-4">

        {/* Scanner card */}
        <div className="bg-surface rounded-2xl border border-border overflow-hidden">
          <div className="px-5 py-3 border-b border-border flex items-center justify-between">
            <h2 className="text-xs font-bold uppercase tracking-widest text-text-muted">📷 Aztec / QR Scanner</h2>
            <span className={`text-xs font-mono font-semibold ${scanning ? 'text-green-600' : 'text-text-muted'}`}>
              {scanning ? '● SCANNING' : '○ IDLE'}
            </span>
          </div>

          {/* Video */}
          <div className="relative bg-black aspect-square">
            <video ref={videoRef} autoPlay playsInline muted className="w-full h-full object-cover" />

            {/* Scan frame overlay */}
            {scanning && (
              <div className="absolute inset-0 flex items-center justify-center pointer-events-none">
                <div className="w-2/3 aspect-square relative">
                  {/* Corner brackets */}
                  <span className="absolute top-0 left-0 w-6 h-6 border-t-4 border-l-4 border-brand rounded-tl-sm" />
                  <span className="absolute top-0 right-0 w-6 h-6 border-t-4 border-r-4 border-brand rounded-tr-sm" />
                  <span className="absolute bottom-0 left-0 w-6 h-6 border-b-4 border-l-4 border-brand rounded-bl-sm" />
                  <span className="absolute bottom-0 right-0 w-6 h-6 border-b-4 border-r-4 border-brand rounded-br-sm" />
                  {/* Scan line */}
                  <div
                    className="absolute left-4 right-4 h-0.5 bg-brand/80"
                    style={{ animation: 'scanline 2.5s ease-in-out infinite', boxShadow: '0 0 8px #FD652F' }}
                  />
                </div>
              </div>
            )}

            {/* Idle state */}
            {!scanning && (
              <div className="absolute inset-0 flex flex-col items-center justify-center gap-3 bg-navy/90">
                <span className="text-5xl opacity-30">⬡</span>
                <p className="text-cream/50 text-sm text-center font-mono px-6">
                  {camError ?? 'Camera inactive\nPress Start to scan'}
                </p>
              </div>
            )}
          </div>

          {/* Controls */}
          <div className="p-4 flex gap-3">
            <button
              onClick={startScanner}
              disabled={scanning}
              className="flex-1 bg-shpe-blue hover:bg-navy disabled:opacity-40 text-white text-sm font-bold py-3 rounded-xl transition-colors"
            >
              ▶ Start Camera
            </button>
            <button
              onClick={stopScanner}
              disabled={!scanning}
              className="flex-1 bg-cream-dark hover:bg-cream text-text-muted disabled:opacity-40 text-sm font-bold py-3 rounded-xl border border-border transition-colors"
            >
              ■ Stop
            </button>
          </div>
          <p className="text-center text-xs text-text-muted pb-4 font-mono">ZXing · Aztec, QR, DataMatrix</p>
        </div>

        {/* Student info card */}
        <div className="bg-surface rounded-2xl border border-border overflow-hidden">
          <div className="px-5 py-3 border-b border-border">
            <h2 className="text-xs font-bold uppercase tracking-widest text-text-muted">👤 Student Info</h2>
          </div>

          <div className="p-5 min-h-64 flex flex-col">
            {resultState === 'idle' && (
              <div className="flex-1 flex flex-col items-center justify-center gap-3 text-text-muted">
                <span className="text-4xl opacity-20">◎</span>
                <p className="text-sm font-mono">Awaiting scan…</p>
              </div>
            )}

            {resultState === 'loading' && (
              <div className="flex-1 flex flex-col items-center justify-center gap-3 text-text-muted">
                <span className="text-3xl animate-spin inline-block">⟳</span>
                <p className="text-sm font-mono">Looking up student…</p>
              </div>
            )}

            {(resultState === 'found' || resultState === 'already_in' || resultState === 'confirmed') && student && (
              <div className="flex flex-col gap-4">
                {/* Status banner */}
                <div className={`flex items-center gap-2 px-4 py-2.5 rounded-xl text-sm font-bold font-mono ${
                  resultState === 'already_in'
                    ? 'bg-red-50 border border-red-200 text-red-700'
                    : 'bg-green-50 border border-green-200 text-green-700'
                }`}>
                  {resultState === 'already_in' ? '⚠ ALREADY CHECKED IN' :
                   resultState === 'confirmed'   ? '✓ CHECKED IN SUCCESSFULLY' :
                                                   '✓ IDENTITY CONFIRMED'}
                </div>

                {/* Avatar + name */}
                <div className="flex items-center gap-4">
                  <div className="w-14 h-14 rounded-xl bg-shpe-blue flex items-center justify-center text-white text-xl font-extrabold shrink-0">
                    {initials}
                  </div>
                  <div>
                    <p className="text-xl font-extrabold text-navy leading-tight">
                      {student.firstName} {student.lastName}
                    </p>
                    <p className="text-sm text-text-muted mt-0.5">Georgia State University</p>
                  </div>
                </div>

                {/* Data fields */}
                <div className="space-y-2">
                  <div className="bg-cream rounded-lg px-4 py-2.5 flex justify-between items-center">
                    <span className="text-xs font-semibold text-text-muted uppercase tracking-wider">Email</span>
                    <span className="text-sm font-mono text-navy font-semibold">{student.email}</span>
                  </div>
                  <div className="bg-cream rounded-lg px-4 py-2.5 flex justify-between items-center">
                    <span className="text-xs font-semibold text-text-muted uppercase tracking-wider">Scan Time</span>
                    <span className="text-sm font-mono text-navy font-semibold">{lastTime}</span>
                  </div>
                </div>

                {/* Check-in button */}
                {resultState === 'found' && (
                  <button
                    onClick={confirmCheckIn}
                    className="w-full bg-green-600 hover:bg-green-700 text-white font-bold text-base py-3 rounded-xl transition-colors"
                  >
                    ✓ Confirm Check-In
                  </button>
                )}
                {(resultState === 'already_in' || resultState === 'confirmed') && (
                  <button disabled className="w-full bg-text-muted/30 text-text-muted font-bold text-base py-3 rounded-xl cursor-not-allowed">
                    {resultState === 'confirmed' ? '✓ Checked In' : 'Already Checked In'}
                  </button>
                )}
              </div>
            )}
          </div>
        </div>
      </div>

      {/* ── Check-in log ─────────────────────────────────────── */}
      <div className="bg-surface rounded-2xl border border-border overflow-hidden">
        <div className="px-5 py-3 border-b border-border flex items-center justify-between">
          <h2 className="text-xs font-bold uppercase tracking-widest text-text-muted">📋 Check-In Log</h2>
          {log.length > 0 && (
            <button
              onClick={() => { setLog([]); checkedInIds.current.clear(); }}
              className="text-xs text-text-muted hover:text-brand font-mono transition-colors"
            >
              Clear
            </button>
          )}
        </div>
        {log.length === 0 ? (
          <p className="py-12 text-center text-sm text-text-muted font-mono">No check-ins yet</p>
        ) : (
          <ul className="divide-y divide-border max-h-72 overflow-y-auto">
            {log.map((entry, i) => (
              <li key={i} className="flex items-center gap-3 px-5 py-3.5">
                <div className="w-9 h-9 rounded-lg bg-shpe-blue text-white flex items-center justify-center text-xs font-bold shrink-0">
                  {(entry.firstName[0] + entry.lastName[0]).toUpperCase()}
                </div>
                <div className="flex-1 min-w-0">
                  <p className="font-semibold text-navy text-sm">{entry.firstName} {entry.lastName}</p>
                  <p className="text-xs text-text-muted font-mono truncate">{entry.email}</p>
                </div>
                <div className="text-right shrink-0">
                  <span className="inline-block bg-green-100 text-green-700 border border-green-200 text-xs font-bold font-mono px-2 py-0.5 rounded">
                    CHECKED IN
                  </span>
                  <p className="text-xs text-text-muted font-mono mt-1">
                    {entry.time.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit' })}
                  </p>
                </div>
              </li>
            ))}
          </ul>
        )}
      </div>

      {/* ── Manual sign-in modal ─────────────────────────────── */}
      {showManual && (
        <div className="fixed inset-0 z-50 flex items-end sm:items-center justify-center">
          {/* Backdrop */}
          <div
            className="absolute inset-0 bg-navy/60 backdrop-blur-sm"
            onClick={() => setShowManual(false)}
          />
          <div className="relative bg-surface rounded-2xl shadow-2xl w-full max-w-md mx-4 mb-4 sm:mb-0 p-6">
            <h3 className="text-lg font-bold text-navy mb-1">Manual Sign-In</h3>
            <p className="text-sm text-text-muted mb-5">Enter the student's GSU email address to check them in.</p>

            <form onSubmit={handleManualSubmit} className="flex flex-col gap-4">
              <div>
                <label className="text-xs font-semibold text-text-muted uppercase tracking-wider block mb-1.5">
                  Student GSU Email
                </label>
                <input
                  type="email"
                  value={manualEmail}
                  onChange={e => setManualEmail(e.target.value)}
                  placeholder="panther@student.gsu.edu"
                  required
                  autoFocus
                  className="w-full bg-cream border border-border rounded-xl px-4 py-3 text-sm text-navy font-mono placeholder:text-text-muted outline-none focus:border-shpe-blue transition-colors"
                />
              </div>

              <div className="flex gap-3">
                <button
                  type="button"
                  onClick={() => setShowManual(false)}
                  className="flex-1 bg-cream-dark hover:bg-cream border border-border text-text-muted font-semibold py-3 rounded-xl text-sm transition-colors"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={manualLoading}
                  className="flex-1 bg-shpe-blue hover:bg-navy disabled:opacity-60 text-white font-bold py-3 rounded-xl text-sm transition-colors"
                >
                  {manualLoading ? 'Looking up…' : 'Look Up & Check In'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Scanline animation */}
      <style>{`
        @keyframes scanline {
          0%   { top: 10%; opacity: 0; }
          10%  { opacity: 1; }
          90%  { opacity: 1; }
          100% { top: 90%; opacity: 0; }
        }
      `}</style>
    </div>
  );
}
