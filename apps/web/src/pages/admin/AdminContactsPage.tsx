/* Phase 1 stub — admin contacts / mailing list management. */
export default function AdminContactsPage() {
  return (
    <div>
      <h1 className="text-2xl font-bold text-navy mb-2">Contacts</h1>
      <p className="text-text-muted mb-8">Export member contact lists, manage mailing groups, and SMS blasts coming in Phase 1.</p>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {[
          { icon: '📧', title: 'Email Blast', desc: 'Send announcements to all members or filtered groups.' },
          { icon: '📱', title: 'SMS Notifications', desc: 'Text message alerts via Telnyx 10DLC for event reminders.' },
          { icon: '📥', title: 'CSV Export', desc: 'Download member contact info for external tools.' },
          { icon: '👥', title: 'Mailing Groups', desc: 'Create sub-lists by graduation year, major, or role.' },
        ].map(card => (
          <div key={card.title} className="bg-surface rounded-xl border border-border p-5 flex gap-4">
            <span className="text-3xl">{card.icon}</span>
            <div>
              <h3 className="font-semibold text-navy mb-1">{card.title}</h3>
              <p className="text-sm text-text-muted">{card.desc}</p>
              <span className="inline-block mt-2 text-xs font-semibold text-shpe-blue bg-shpe-blue/10 px-2 py-0.5 rounded">Phase 1</span>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
