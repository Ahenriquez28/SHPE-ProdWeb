/*
 * AdminPortalLayout — standalone admin portal with its own sidebar + mobile bottom nav.
 *
 * Desktop: fixed left sidebar with all admin tabs.
 * Mobile:  sticky bottom tab bar (same tabs, icon + label).
 *
 * Tabs: Members | Events | Calendar | Check-In | Contacts
 */

import { Link, NavLink, Outlet } from 'react-router-dom';

interface NavItem {
  to:    string;
  label: string;
  icon:  string;
  mobileIcon: string;
}

const ADMIN_TABS: NavItem[] = [
  { to: '/admin/members',     label: 'Members',      icon: '👥', mobileIcon: '👥' },
  { to: '/admin/events',      label: 'Events',       icon: '🗓', mobileIcon: '🗓' },
  { to: '/admin/calendar',    label: 'Calendar',     icon: '📅', mobileIcon: '📅' },
  { to: '/admin/checkin',     label: 'Check-In',     icon: '✅', mobileIcon: '✅' },
  { to: '/admin/mailinglist', label: 'Mailing List', icon: '📋', mobileIcon: '📋' },
  { to: '/admin/contacts',    label: 'Contacts',     icon: '📞', mobileIcon: '📞' },
];

function SideLink({ to, label, icon }: NavItem) {
  return (
    <NavLink
      to={to}
      className={({ isActive }) =>
        [
          'flex items-center gap-3 px-4 py-2.5 rounded-lg text-sm font-medium transition-all',
          isActive
            ? 'bg-shpe-blue text-white shadow-sm'
            : 'text-cream/70 hover:bg-white/10 hover:text-cream',
        ].join(' ')
      }
    >
      <span className="text-base">{icon}</span>
      {label}
    </NavLink>
  );
}

function BottomTab({ to, label, mobileIcon }: NavItem) {
  return (
    <NavLink
      to={to}
      className={({ isActive }) =>
        [
          'flex-1 flex flex-col items-center justify-center gap-1 py-2 text-xs font-semibold transition-colors',
          isActive ? 'text-brand' : 'text-cream/50',
        ].join(' ')
      }
    >
      <span className="text-xl leading-none">{mobileIcon}</span>
      <span className="text-[10px] leading-none">{label}</span>
    </NavLink>
  );
}

export default function AdminPortalLayout() {
  return (
    <div className="min-h-screen flex bg-cream">

      {/* ── Desktop Sidebar ───────────────────────────────────────── */}
      <aside className="hidden md:flex flex-col w-60 bg-navy shrink-0 min-h-screen fixed top-0 left-0 bottom-0 z-30">

        {/* Logo */}
        <Link to="/" className="px-5 h-16 border-b border-white/10 flex items-center">
          <img src="/SHPE_logo_horiz_[school]_ALL_DK BG.png" alt="SHPE @ GSU" className="h-9 w-auto" />
        </Link>

        {/* Admin badge */}
        <div className="px-5 py-3">
          <span className="inline-flex items-center gap-1.5 text-xs font-bold uppercase tracking-widest text-white bg-brand px-2.5 py-1 rounded-full">
            ⚙ Admin Portal
          </span>
        </div>

        {/* Nav */}
        <nav className="flex-1 px-3 py-2 flex flex-col gap-1">
          <p className="px-4 mb-2 text-xs font-semibold text-cream/30 uppercase tracking-wider">Manage</p>
          {ADMIN_TABS.map(tab => <SideLink key={tab.to} {...tab} />)}
        </nav>

        {/* Back to member portal */}
        <div className="px-5 py-4 border-t border-white/10">
          <Link to="/dashboard" className="flex items-center gap-2 text-cream/40 hover:text-shpe-blue-light text-xs font-medium transition-colors">
            ← Member Portal
          </Link>
        </div>
      </aside>

      {/* ── Main content (offset for desktop sidebar) ─────────────── */}
      <div className="flex-1 flex flex-col md:ml-60">

        {/* Desktop top bar */}
        <div className="hidden md:flex items-center justify-between px-6 py-3 bg-surface border-b border-border sticky top-0 z-20">
          <div className="flex items-center gap-2">
            <span className="text-xs font-bold text-brand uppercase tracking-widest">Admin Portal</span>
            <span className="text-border">›</span>
            <span className="text-xs text-text-muted">Management Console</span>
          </div>
          <Link to="/dashboard" className="text-xs text-shpe-blue hover:text-navy font-medium transition-colors">
            ← Back to Member Portal
          </Link>
        </div>

        {/* Mobile top bar */}
        <header className="md:hidden bg-navy h-14 flex items-center justify-between px-4 shadow-md sticky top-0 z-20">
          <div className="flex items-center gap-3">
            <img src="/SHPE_logo_horiz_[school]_ALL_DK BG.png" alt="SHPE @ GSU" className="h-7 w-auto" />
            <span className="text-xs font-bold text-brand uppercase tracking-widest">Admin</span>
          </div>
          <Link to="/dashboard" className="text-cream/50 hover:text-cream text-xs transition-colors">
            ← Portal
          </Link>
        </header>

        {/* Page content — pb-24 on mobile so bottom nav doesn't cover content */}
        <main className="flex-1 p-4 md:p-6 max-w-5xl w-full mx-auto pb-28 md:pb-6">
          <Outlet />
        </main>
      </div>

      {/* ── Mobile Bottom Tab Bar ─────────────────────────────────── */}
      <nav className="md:hidden fixed bottom-0 left-0 right-0 z-40 bg-navy border-t border-white/10 flex items-stretch"
           style={{ paddingBottom: 'env(safe-area-inset-bottom)' }}>
        {ADMIN_TABS.map(tab => <BottomTab key={tab.to} {...tab} />)}
      </nav>
    </div>
  );
}
