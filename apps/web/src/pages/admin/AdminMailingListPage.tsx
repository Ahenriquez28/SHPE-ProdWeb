/* Phase 1 stub — admin mailing list builder.
 *
 * Different from Contacts (which manages communication channels / blasts).
 * This page is for building, tagging, and exporting curated contact lists —
 * think "create a list of all 2026 graduates for alumni outreach."
 */
export default function AdminMailingListPage() {
  return (
    <div>
      <h1 className="text-2xl font-bold text-navy mb-2">Mailing Lists</h1>
      <p className="text-text-muted mb-8">
        Build and manage targeted contact lists for outreach campaigns.
      </p>

      {/* What this will do */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-8">
        {[
          {
            icon: '📋',
            title: 'Build a List',
            desc: 'Filter members by grad year, major, role, or event attendance and save as a named list.',
            badge: 'Phase 1',
          },
          {
            icon: '🏷️',
            title: 'Tags & Segments',
            desc: 'Tag members manually (e.g. "alumni", "intern-ready") and segment by tag.',
            badge: 'Phase 1',
          },
          {
            icon: '📤',
            title: 'Export CSV',
            desc: 'Download any list as a CSV with name, email, phone, and grad year columns.',
            badge: 'Phase 1',
          },
          {
            icon: '📧',
            title: 'Send to List',
            desc: 'Pipe a saved list directly into an email blast without copying addresses.',
            badge: 'Phase 1',
          },
        ].map(card => (
          <div key={card.title} className="bg-surface rounded-xl border border-border p-5 flex gap-4">
            <span className="text-3xl shrink-0">{card.icon}</span>
            <div>
              <h3 className="font-semibold text-navy mb-1">{card.title}</h3>
              <p className="text-sm text-text-muted leading-relaxed">{card.desc}</p>
              <span className="inline-block mt-2 text-xs font-semibold text-shpe-blue bg-shpe-blue/10 px-2 py-0.5 rounded">
                {card.badge}
              </span>
            </div>
          </div>
        ))}
      </div>

      {/* Placeholder list table */}
      <div className="bg-surface rounded-2xl border border-border overflow-hidden">
        <div className="px-5 py-3 border-b border-border flex items-center justify-between">
          <h2 className="text-xs font-bold uppercase tracking-widest text-text-muted">
            Saved Lists
          </h2>
          <button
            disabled
            className="text-xs font-semibold bg-shpe-blue/10 text-shpe-blue px-3 py-1.5 rounded-lg opacity-50 cursor-not-allowed"
          >
            + New List
          </button>
        </div>
        <div className="py-16 text-center">
          <span className="text-4xl opacity-20">📋</span>
          <p className="text-sm text-text-muted font-mono mt-3">No lists yet — coming in Phase 1.</p>
        </div>
      </div>
    </div>
  );
}
