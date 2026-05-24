/*
 * PublicLayout — wraps all unauthenticated pages (home, events list, sponsors, etc.)
 *
 * Structure:
 *   <nav>         sticky top nav with logo + sign-in link
 *   <main>        page content (fills remaining viewport height)
 *   <footer>      SHPE branding + links
 *
 * Color contract: navy nav, cream body, orange accents.
 */

import { Link, Outlet } from 'react-router-dom';
import { useAuth, SignInButton, UserButton } from '@clerk/react';

export default function PublicLayout() {
  const { isLoaded, isSignedIn } = useAuth();

  return (
    <div className="min-h-screen flex flex-col bg-cream">
      {/* ── Top Nav ───────────────────────────────────────────────── */}
      <header className="bg-navy sticky top-0 z-50 shadow-md">
        <div className="max-w-6xl mx-auto px-4 h-16 flex items-center justify-between">
          {/* Logo */}
          <Link to="/" className="flex items-center">
            <img
              src="/SHPE_logo_horiz_[school]_ALL_DK BG.png"
              alt="SHPE @ Georgia State University"
              className="h-10 w-auto"
            />
          </Link>

          {/* Nav links */}
          <nav className="hidden md:flex items-center gap-6 text-sm font-medium">
            <Link
              to="/events"
              className="text-cream/70 hover:text-brand transition-colors"
            >
              Events
            </Link>
            <Link
              to="/sponsors"
              className="text-cream/70 hover:text-brand transition-colors"
            >
              Sponsors
            </Link>
            <Link
              to="/about"
              className="text-cream/70 hover:text-brand transition-colors"
            >
              About
            </Link>
          </nav>

          {/* Auth controls */}
          <div className="flex items-center gap-3">
            {isLoaded && isSignedIn && (
              <>
                <Link
                  to="/dashboard"
                  className="text-cream/70 hover:text-shpe-blue-light text-sm font-medium transition-colors"
                >
                  Member Portal
                </Link>
                <Link
                  to="/admin"
                  className="bg-brand hover:bg-brand-dark text-white text-sm font-semibold px-4 py-2 rounded-lg transition-colors"
                >
                  Admin
                </Link>
                <UserButton appearance={{ elements: { avatarBox: 'w-8 h-8' } }} />
              </>
            )}
            {isLoaded && !isSignedIn && (
              <SignInButton mode="modal">
                <button className="bg-brand hover:bg-brand-dark text-white text-sm font-semibold px-4 py-2 rounded-lg transition-colors">
                  Sign In
                </button>
              </SignInButton>
            )}
          </div>
        </div>
      </header>

      {/* ── Page content ──────────────────────────────────────────── */}
      <main className="flex-1">
        <Outlet />
      </main>

      {/* ── Footer ───────────────────────────────────────────────── */}
      <footer className="bg-navy-dark text-cream/40 text-sm py-10 mt-auto">
        <div className="max-w-6xl mx-auto px-4 flex flex-col md:flex-row items-center justify-between gap-6">
          <div className="flex flex-col items-center md:items-start gap-2">
            <img
              src="/SHPE_logo_horiz_[school]_ALL_DK BG.png"
              alt="SHPE @ GSU"
              className="h-8 w-auto opacity-70"
            />
            <p className="text-xs">© {new Date().getFullYear()} Society of Hispanic Professional Engineers @ Georgia State University</p>
          </div>
          <div className="flex gap-6">
            <a
              href="https://shpe.org"
              target="_blank"
              rel="noopener noreferrer"
              className="hover:text-shpe-blue-light transition-colors"
            >
              SHPE National
            </a>
            <Link to="/about" className="hover:text-shpe-blue-light transition-colors">
              About
            </Link>
          </div>
        </div>
      </footer>
    </div>
  );
}
