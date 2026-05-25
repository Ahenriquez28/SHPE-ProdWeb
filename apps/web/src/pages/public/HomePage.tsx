/*
 * HomePage — public landing page.
 * Hero uses official SHPE logo + brand colors (navy bg, orange + blue accents).
 * Sponsor carousel and event cards come in Phase 1.
 */

import { Link } from 'react-router-dom';
import Button from '@/components/ui/Button';

export default function HomePage() {
  return (
    <div>
      {/* ── Hero ──────────────────────────────────────────────────── */}
      <section className="bg-navy text-cream py-28 px-4 text-center relative overflow-hidden">
        {/* Subtle blue gradient overlay */}
        <div className="absolute inset-0 bg-gradient-to-br from-navy via-navy to-shpe-blue/20 pointer-events-none" />

        <div className="relative max-w-3xl mx-auto flex flex-col items-center gap-6">
          {/* SHPE logo */}
          <img
            src="/SHPE_logo_horiz_[school]_ALL_DK BG.png"
            alt="SHPE @ Georgia State University"
            className="h-16 md:h-20 w-auto"
          />

          <p className="text-cream/70 text-xl max-w-2xl leading-relaxed">
            Society of Hispanic Professional Engineers — empowering Hispanic
            students in STEM at Georgia State University.
          </p>

          <div className="flex justify-center gap-4 flex-wrap pt-2">
            <Link to="/events">
              <Button size="lg">Upcoming Events</Button>
            </Link>
            <Link to="/about">
              <Button size="lg" variant="secondary">
                Learn More
              </Button>
            </Link>
          </div>
        </div>

        {/* Bottom wave separator */}
        <div className="absolute bottom-0 left-0 right-0 h-8 bg-cream"
          style={{ clipPath: 'ellipse(55% 100% at 50% 100%)' }}
        />
      </section>

      {/* ── Value props ────────────────────────────────────────────── */}
      <section className="max-w-5xl mx-auto px-4 py-20">
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          {[
            {
              icon: '🎓',
              title: 'Academic Excellence',
              body: 'Workshops, tutoring, and study groups to help you succeed in STEM.',
              accent: 'border-shpe-blue',
            },
            {
              icon: '🤝',
              title: 'Professional Network',
              body: 'Connect with industry partners, alumni, and recruiters at our events.',
              accent: 'border-brand',
            },
            {
              icon: '🌎',
              title: 'Community',
              body: 'A family of Hispanic engineers supporting each other at GSU and beyond.',
              accent: 'border-shpe-blue-light',
            },
          ].map((card) => (
            <div
              key={card.title}
              className={`bg-surface rounded-2xl p-6 shadow-sm border-t-4 ${card.accent} flex flex-col gap-3`}
            >
              <span className="text-3xl">{card.icon}</span>
              <h3 className="text-lg font-bold text-navy">{card.title}</h3>
              <p className="text-text-muted text-sm leading-relaxed">{card.body}</p>
            </div>
          ))}
        </div>
      </section>

      {/* ── Placeholder sections ────────────────────────────────────── */}
      <section className="bg-cream-dark py-16">
        <div className="max-w-5xl mx-auto px-4 text-center">
          <h2 className="text-2xl font-bold text-navy mb-3">Upcoming Events</h2>
          <p className="text-text-muted">Event cards will appear here in Phase 1.</p>
        </div>
      </section>

      <section className="py-16">
        <div className="max-w-5xl mx-auto px-4 text-center">
          <h2 className="text-2xl font-bold text-navy mb-3">Our Sponsors</h2>
          <p className="text-text-muted">Sponsor logos and tiers will appear here in Phase 1.</p>
        </div>
      </section>
    </div>
  );
}
