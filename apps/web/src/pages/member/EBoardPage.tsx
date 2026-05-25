/* Phase 1 stub — public E-Board & leadership directory. */
export default function EBoardPage() {
  return (
    <div>
      <h1 className="text-2xl font-bold text-navy mb-2">E-Board & Leadership</h1>
      <p className="text-text-muted mb-8">Meet the SHPE @ GSU executive board and dev team.</p>

      <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-4">
        {[
          { role: 'President', name: 'TBD' },
          { role: 'Vice President', name: 'TBD' },
          { role: 'Secretary', name: 'TBD' },
          { role: 'Treasurer', name: 'TBD' },
          { role: 'VP Academic', name: 'TBD' },
          { role: 'VP Professional', name: 'TBD' },
          { role: 'VP Social', name: 'TBD' },
          { role: 'Webmaster', name: 'TBD' },
        ].map(member => (
          <div key={member.role} className="bg-surface rounded-xl border border-border p-4 flex flex-col items-center gap-3 text-center">
            <div className="w-16 h-16 rounded-full bg-cream-dark border-2 border-border flex items-center justify-center text-2xl">
              👤
            </div>
            <div>
              <p className="font-semibold text-navy text-sm">{member.name}</p>
              <p className="text-xs text-text-muted mt-0.5">{member.role}</p>
            </div>
          </div>
        ))}
      </div>
      <p className="text-center text-xs text-text-muted mt-6">Headshots and names populated in Phase 1.</p>
    </div>
  );
}
