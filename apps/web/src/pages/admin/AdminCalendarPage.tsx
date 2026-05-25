import { useState } from 'react';

const DAY_HEADERS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

function toKey(y: number, m: number, d: number) {
  return `${y}-${String(m + 1).padStart(2, '0')}-${String(d).padStart(2, '0')}`;
}

function todayKey() {
  const n = new Date();
  return toKey(n.getFullYear(), n.getMonth(), n.getDate());
}

function buildGrid(year: number, month: number) {
  const firstDow = new Date(year, month, 1).getDay();
  const daysInMo = new Date(year, month + 1, 0).getDate();
  const prevDays = new Date(year, month, 0).getDate();
  const total    = Math.ceil((firstDow + daysInMo) / 7) * 7;

  return Array.from({ length: total }, (_, i) => {
    if (i < firstDow) {
      return { day: prevDays - firstDow + i + 1, inMonth: false,
               key: toKey(month === 0 ? year - 1 : year, month === 0 ? 11 : month - 1, prevDays - firstDow + i + 1) };
    }
    const day = i - firstDow + 1;
    if (day > daysInMo) {
      return { day: day - daysInMo, inMonth: false,
               key: toKey(month === 11 ? year + 1 : year, month === 11 ? 0 : month + 1, day - daysInMo) };
    }
    return { day, inMonth: true, key: toKey(year, month, day) };
  });
}

export default function AdminCalendarPage() {
  const today = todayKey();
  const [viewDate, setViewDate] = useState(() => new Date());

  const year  = viewDate.getFullYear();
  const month = viewDate.getMonth();
  const grid  = buildGrid(year, month);
  const monthLabel = viewDate.toLocaleString('default', { month: 'long', year: 'numeric' });

  const prevMonth = () => setViewDate(d => new Date(d.getFullYear(), d.getMonth() - 1, 1));
  const nextMonth = () => setViewDate(d => new Date(d.getFullYear(), d.getMonth() + 1, 1));
  const goToday   = () => setViewDate(new Date());

  return (
    <div>
      {/* Header */}
      <div className="flex items-center justify-between mb-5 flex-wrap gap-3">
        <div className="flex items-center gap-3">
          <button
            onClick={prevMonth}
            className="w-8 h-8 flex items-center justify-center rounded-lg border border-border bg-surface hover:bg-cream transition-colors text-navy font-bold"
          >
            ‹
          </button>
          <h1 className="text-xl font-bold text-navy w-48 text-center">{monthLabel}</h1>
          <button
            onClick={nextMonth}
            className="w-8 h-8 flex items-center justify-center rounded-lg border border-border bg-surface hover:bg-cream transition-colors text-navy font-bold"
          >
            ›
          </button>
        </div>
        <button
          onClick={goToday}
          className="text-xs font-semibold text-shpe-blue border border-shpe-blue/30 bg-shpe-blue/5 hover:bg-shpe-blue/10 px-3 py-1.5 rounded-lg transition-colors"
        >
          Today
        </button>
      </div>

      {/* Grid */}
      <div className="bg-surface rounded-2xl border border-border overflow-hidden">

        {/* Day headers */}
        <div className="grid grid-cols-7 border-b border-border">
          {DAY_HEADERS.map(d => (
            <div key={d} className="py-2.5 text-center text-xs font-bold text-text-muted uppercase tracking-wider">
              {d}
            </div>
          ))}
        </div>

        {/* Day cells */}
        <div className="grid grid-cols-7">
          {grid.map((cell, i) => {
            const isToday = cell.key === today;

            return (
              <div
                key={i}
                className={[
                  'min-h-[72px] md:min-h-[88px] p-1.5 border-b border-r border-border',
                  cell.inMonth ? 'bg-surface' : 'bg-cream/50',
                ].join(' ')}
              >
                <span className={[
                  'inline-flex w-7 h-7 items-center justify-center rounded-full text-sm font-semibold',
                  isToday                        ? 'bg-shpe-blue text-white' : '',
                  !isToday && cell.inMonth       ? 'text-navy'              : '',
                  !isToday && !cell.inMonth      ? 'text-text-muted'        : '',
                ].join(' ')}>
                  {cell.day}
                </span>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}
