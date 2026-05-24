/*
 * App.tsx — root router.
 *
 * Public:   /  /events  /sponsors  /about
 * Member:   /dashboard  /dashboard/profile  /events  /directory  /eboard
 * Admin:    /admin/members  /events  /calendar  /checkin  /contacts
 *
 * AuthSync runs at the root so it fires on every sign-in regardless of which
 * route the user lands on. It's a no-op after the first successful call.
 */

import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { useAuthSync } from '@/hooks/useAuthSync';

import PublicLayout      from '@/layouts/PublicLayout';
import MemberLayout      from '@/layouts/MemberLayout';
import AdminPortalLayout from '@/layouts/AdminPortalLayout';
import RequireAuth       from '@/components/RequireAuth';

// Public
import HomePage     from '@/pages/public/HomePage';
import EventsPage   from '@/pages/public/EventsPage';
import SponsorsPage from '@/pages/public/SponsorsPage';
import AboutPage    from '@/pages/public/AboutPage';

// Member
import DashboardPage from '@/pages/member/DashboardPage';
import ProfilePage   from '@/pages/member/ProfilePage';
import DirectoryPage from '@/pages/member/DirectoryPage';
import EBoardPage    from '@/pages/member/EBoardPage';

// Admin
import AdminMembersPage  from '@/pages/admin/AdminMembersPage';
import AdminEventsPage   from '@/pages/admin/AdminEventsPage';
import AdminEBoardPage   from '@/pages/admin/AdminEBoardPage';
import AdminSponsorsPage from '@/pages/admin/AdminSponsorsPage';
import AdminCalendarPage    from '@/pages/admin/AdminCalendarPage';
import AdminCheckInPage     from '@/pages/admin/AdminCheckInPage';
import AdminContactsPage    from '@/pages/admin/AdminContactsPage';
import AdminMailingListPage from '@/pages/admin/AdminMailingListPage';

// Thin component so the hook runs inside BrowserRouter (Router context required
// for useNavigate internally) and inside ClerkProvider (set up in main.tsx).
function AuthSync() {
  useAuthSync();
  return null;
}

export default function App() {
  return (
    <BrowserRouter>
      <AuthSync />
      <Routes>
        {/* ── Public ─────────────────────────────────────────────── */}
        <Route element={<PublicLayout />}>
          <Route index element={<HomePage />} />
          <Route path="events"   element={<EventsPage />} />
          <Route path="sponsors" element={<SponsorsPage />} />
          <Route path="about"    element={<AboutPage />} />
        </Route>

        {/* ── Member portal ──────────────────────────────────────── */}
        <Route path="dashboard" element={<RequireAuth><MemberLayout /></RequireAuth>}>
          <Route index element={<DashboardPage />} />
          <Route path="profile"   element={<ProfilePage />} />
          <Route path="events"    element={<EventsPage />} />
          <Route path="directory" element={<DirectoryPage />} />
          <Route path="eboard"    element={<EBoardPage />} />
        </Route>

        {/* ── Admin portal — own layout, own sidebar ─────────────── */}
        <Route path="admin" element={<RequireAuth><AdminPortalLayout /></RequireAuth>}>
          <Route index element={<Navigate to="/admin/members" replace />} />
          <Route path="members"  element={<AdminMembersPage />} />
          <Route path="events"   element={<AdminEventsPage />} />
          <Route path="eboard"   element={<AdminEBoardPage />} />
          <Route path="sponsors" element={<AdminSponsorsPage />} />
          <Route path="calendar"    element={<AdminCalendarPage />} />
          <Route path="checkin"     element={<AdminCheckInPage />} />
          <Route path="mailinglist" element={<AdminMailingListPage />} />
          <Route path="contacts"    element={<AdminContactsPage />} />
        </Route>

        {/* Legacy shorthand redirects */}
        <Route path="profile"   element={<Navigate to="/dashboard/profile"   replace />} />
        <Route path="directory" element={<Navigate to="/dashboard/directory" replace />} />

        {/* 404 → home */}
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}
