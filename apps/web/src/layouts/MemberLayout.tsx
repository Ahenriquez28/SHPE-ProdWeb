/*
 * MemberLayout — authenticated member area with sidebar + mobile bottom nav.
 *
 * Desktop: fixed left sidebar.
 * Mobile:  sticky bottom tab bar with 5 tabs (icon + label).
 *
 * Tabs: Dashboard | Profile | Events | Directory | E-Board
 *
 * Admin portal lives at /admin/* (AdminPortalLayout) — separate layout entirely.
 */

import { Link, NavLink, Outlet } from 'react-router-dom';
import { UserButton } from '@clerk/react';
import { useMe } from '@/hooks/useMe';

interface NavItem {
  to:    string;
  label: string;
  icon:  string;
}

const MEMBER_TABS: NavItem[] = [
  { to: '/dashboard',           label: 'Home',      icon: '🏠' },
  { to: '/dashboard/profile',   label: 'Profile',   icon: '👤' },
  { to: '/dashboard/events',    label: 'Events',    icon: '📅' },
  { to: '/dashboard/directory', label: 'Directory', icon: '📋' },
  { to: '/dashboard/eboard',    label: 'E-Board',   icon: '⭐' },
];

function SideLink({ to, label, icon }: NavItem) {
  return (
    <NavLink
      to={to}
      end={to === '/dashboard'}    // exact match for dashboard index
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

function BottomTab({ to, label, icon }: NavItem) {
  return (
    <NavLink
      to={to}
      end={to === '/dashboard'}
      className={({ isActive }) =>
        [
          'flex-1 flex flex-col items-center justify-center gap-1 py-2 text-xs font-semibold transition-colors',
          isActive ? 'text-brand' : 'text-cream/50',
        ].join(' ')
      }
    >
      <span className="text-xl leading-none">{icon}</span>
      <span className="text-[10px] leading-none">{label}</span>
    </NavLink>
  );
}

export default function MemberLayout() {
  const { data: me } = useMe();

  return (
    <div className="min-h-screen flex bg-cream">

      {/* ── Desktop Sidebar ───────────────────────────────────────── */}
      <aside className="hidden md:flex flex-col w-56 bg-navy shrink-0 min-h-screen fixed top-0 left-0 bottom-0 z-30">

        {/* Logo */}
        <Link to="/" className="px-4 h-16 border-b border-white/10 flex items-center">
          <img src="/SHPE_logo_horiz_[school]_ALL_DK BG.png" alt="SHPE @ GSU" className="h-9 w-auto" />
        </Link>

        {/* Nav links */}
        <nav className="flex-1 px-3 py-4 flex flex-col gap-1">
          <p className="px-4 mb-2 text-xs font-semibold text-cream/30 uppercase tracking-wider">Member</p>
          {MEMBER_TABS.map(tab => <SideLink key={tab.to} {...tab} />)}
        </nav>

        {/* Admin portal shortcut */}
        <div className="px-3 pb-3">
          <Link
            to="/admin"
            className="flex items-center gap-2 px-4 py-2.5 rounded-lg text-sm font-medium text-cream/40 hover:bg-brand/20 hover:text-brand transition-all"
          >
            <span>⚙</span> Admin Portal
          </Link>
        </div>

        {/* User avatar + Clerk account menu */}
        <div className="px-4 py-4 border-t border-white/10 flex items-center gap-3">
          <UserButton
            appearance={{
              elements: {
                avatarBox: 'w-8 h-8',
              },
            }}
          />
          <span className="text-cream/60 text-xs truncate">{me?.fullName ?? ''}</span>
        </div>
      </aside>

      {/* ── Main content (offset for desktop sidebar) ─────────────── */}
      <div className="flex-1 flex flex-col md:ml-56">

        {/* Mobile top bar */}
        <header className="md:hidden bg-navy h-14 flex items-center justify-between px-4 shadow-md sticky top-0 z-20">
          <Link to="/">
            <img src="/SHPE_logo_horiz_[school]_ALL_DK BG.png" alt="SHPE @ GSU" className="h-7 w-auto" />
          </Link>
          <UserButton
            appearance={{ elements: { avatarBox: 'w-8 h-8' } }}
          />
        </header>

        {/* Page content — extra bottom padding on mobile for tab bar */}
        <main className="flex-1 p-4 md:p-6 max-w-5xl w-full mx-auto pb-28 md:pb-6">
          <Outlet />
        </main>
      </div>

      {/* ── Mobile Bottom Tab Bar ─────────────────────────────────── */}
      <nav
        className="md:hidden fixed bottom-0 left-0 right-0 z-40 bg-navy border-t border-white/10 flex items-stretch"
        style={{ paddingBottom: 'env(safe-area-inset-bottom)' }}
      >
        {MEMBER_TABS.map(tab => <BottomTab key={tab.to} {...tab} />)}
      </nav>
    </div>
  );
}
