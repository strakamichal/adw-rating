# TO-BE — Vision and Scope

## 1. Vision

Build a web application for calculating and displaying performance ratings of agility teams (handler + dog) on a global scale. The application operates under the **AgilityDogsWorld** brand, which has an established community (43k Instagram, 20k Facebook followers). The rating database will serve as the primary driver for traffic to the AgilityDogsWorld website, which currently has no content.

**Primary goals:**

1. Provide an objective, data-driven ranking of agility teams worldwide using the Glicko-2 algorithm.
2. Offer each team an interactive profile (bio card) with result history and rating progression.
3. Drive the existing social media community to the website and create a reason for repeat visits.
4. Build the foundation for future monetization (sponsors, premium profiles, partnerships with event organizers).

### NFRs (non-functional requirements)

- **Performance**: Ranking page and profile pages load under 2 s on a 4G connection. Rating recalculation (batch, offline) is not user-facing and has no latency target.
- **Availability**: Best-effort; no formal SLA for MVP. Target: < 1 h unplanned downtime per month. Static/CDN-friendly pages preferred.
- **Security**: No user authentication in MVP — the site is fully public, read-only. Data management via CLI/scripts or direct DB access. No sensitive user data stored.
- **Audit/Logging**: Log competition imports (what was imported, when, row counts). Application error logging for debugging.
- **Localization**: English only. No i18n framework required for MVP.
- **Accessibility**: Semantic HTML, sufficient color contrast, keyboard-navigable. No formal WCAG audit required for MVP.
- **SEO**: Server-rendered or statically generated pages for discoverability (team profiles, rankings). Clean URLs.

## 2. Key concepts

- **Team** — A unique combination of one handler and one dog. This is the rated entity. A handler with two dogs forms two separate teams; if two handlers run the same dog, those are also separate teams, each with their own rating.
- **Handler** — A human competitor. Identified by name and country. A handler can have multiple dogs (and thus multiple teams).
- **Dog** — Identified by call name (and optionally registered name), breed, and size category. A dog can have multiple handlers.
- **Competition (event)** — A single competition event (e.g., AWC 2025, Moravia Open 2024). Takes place on specific dates at a specific location. Has an assigned tier.
- **Run** — A single scored attempt within a competition (e.g., agility individual Large run 1). Each run has its own results (placement, time, faults). Runs are the atomic unit for rating calculation.
- **Combined result** — An aggregation of multiple runs within a competition (e.g., individual overall = agility + jumping). Tracked for display (e.g., World Champion title) but rating is calculated from individual runs.
- **Competition tier** — A classification of competition importance that affects rating weight. Tiers to be defined (e.g., Tier 1: FCI World/Continental Championships; Tier 2: Large international opens; Tier 3: National championships). See `docs/08-rating-rules.md`.
- **Size category** — Dog height category. Ratings are calculated separately per category. FCI uses S (<35 cm), M (35–43 cm), L (>43 cm). Other organizations may use different boundaries (e.g., WAO has different splits). A dog is assigned one primary size category; when competing in a different category at another organization, the runs still count toward the same team rating. See `docs/08-rating-rules.md`.
- **Active team** — A team with ≥ 3 runs in the last 12 months. Only active teams appear in the live rankings. Inactive teams retain their profile (marked as inactive) but are excluded from the leaderboard.

## 3. Scope (MVP)

The MVP covers the following functional areas:

1. **Rating engine** — Glicko-2 calculation from individual run results. Ratings are per team, separated by size category. Competition tier weighting affects rating impact. Details of the Glicko-2 adaptation for multi-competitor races are defined in `docs/08-rating-rules.md`.
2. **Competition data import** — Manual import of structured result data (CSV/Excel) via CLI or admin script. Covers 2–3 years of major international events. Import is all-or-nothing: the entire file must be valid or the import is rejected. Errors are reported for correction.
3. **Identity resolution** — Automatic fuzzy matching of handlers and dogs across imports (diacritics normalization, Levenshtein distance). Confirmed matches are stored in an alias table for future imports. When matching fails, a new entity is created. Admin can merge duplicates later.
4. **Rankings** — Interactive leaderboard filtered by size category (S/M/L) and country. Only active teams displayed (≥ 3 runs in last 12 months).
5. **Team profiles (bio cards)** — Handler + dog info, current rating ± deviation, rating progression chart, competition history, statistics (clean run rate, average placement). No photos in MVP (initials/generic avatar as placeholder).
6. **Handler profiles** — Aggregated view: all dogs (teams), peak rating per team, career statistics.
7. **Dog profiles** — Aggregated view: breed, size category, handler(s), competition history across all teams.
8. **Competition pages** — Results per category and run, metadata (name, date, location, tier). Combined results (e.g., individual overall) displayed where applicable.
9. **Search** — Simple name search with autocomplete across teams, handlers, and dogs.

### Data sources (MVP)

Initial dataset targets major international competitions over the last 2–3 years:
- **FCI events**: Agility World Championship (AWC), European Open (EO), Junior Open
- **WAO events**: World Agility Open
- **Large international opens**: Polish Open, Hungarian Open, Slovenian Open, Moravia Open

Data formats vary by source and will need to be acquired and normalized. No existing dataset is available — data collection is a significant upfront effort.

### Scope (Phase 2 — Crowdsourcing)

1. **Admin authentication** — Simple login for admin users to manage competitions, review imports, and moderate data via the web UI.
2. **Crowdsourced result uploads** — Competitors and organizers can submit competition results through a web upload interface (CSV/Excel). Submitted data enters a review queue.
3. **Import review workflow** — Admin reviews and approves/rejects submitted results before they enter the rating calculation.
4. **Competition calendar** — Upcoming events listed with date, location, and tier.
5. **Claim your profile** — A handler can request to link their social/contact info to their profile (moderated by admin).

### Scope (Phase 3 — Automation & Growth)

1. **Automated data import** — Scrapers and API integrations with competition management platforms (where technically feasible and legally permitted).
2. **User accounts** — Handlers can register, claim their profile, and manage their public info.
3. **Premium profiles** — Extended statistics, historical trends, head-to-head comparisons (potential monetization).
4. **Content section** — Analyses, articles, and social media cross-posts tied to IG/FB content.
5. **Sponsorship integrations** — Display slots for partners (pet food brands, equipment manufacturers).
6. **Regional rankings** — Dedicated regional leaderboards alongside the global one.
7. **Team photos** — Profile photos via user accounts or admin upload.

## 4. Non-goals

These items are out of scope for **all planned phases**:

- **Mobile native app** — responsive web is sufficient.
- **Real-time updates** — ratings are recalculated in batch, not live.
- **Notifications** — no email, push, or in-app notifications.
- **Multi-language UI** — English only for the foreseeable future.
- **Offline/PWA mode** — not needed for this type of content site.
- **Team/squad competitions** — only individual results are rated. Team events may be displayed but do not contribute to team ratings.

## 5. Key use cases

1. **Browse global rankings** — A visitor opens the ranking page, selects size category (S/M/L), optionally filters by country, and browses the leaderboard of active teams sorted by rating.
2. **View team profile** — A visitor clicks on a team in the rankings (or finds it via search) and sees the bio card: rating ± deviation, rating chart over time, competition history, and statistics.
3. **View handler profile** — A visitor views a handler's page showing all their dogs (teams), peak ratings, and career overview.
4. **View competition results** — A visitor opens a competition page to see results per run and combined results per category, including placement, faults, and time. DSQ entries are shown with last-place ranking.
5. **Search for a team, handler, or dog** — A visitor searches by name (autocomplete) and navigates to the relevant profile.
6. **Import competition results** — An admin imports a CSV/Excel file of competition results via CLI. The system validates the entire file, performs identity resolution (fuzzy matching against known handlers/dogs), and stores the results. If validation fails, the entire import is rejected with an error report.
7. **Recalculate ratings** — After importing new competition data, the admin triggers a full Glicko-2 recalculation. Ratings, deviations, and volatilities are updated for all affected teams.
8. **Share a profile via social media** — A visitor copies a clean URL to a team profile and shares it on Instagram/Facebook. The link renders with proper Open Graph metadata (name, rating).
9. **View inactive team** — A visitor finds a team that hasn't competed recently. The profile is accessible but marked as inactive, and the team is excluded from the live ranking.
10. **Merge duplicate entities** — An admin discovers two handler or dog records that represent the same real-world entity. The admin merges them, consolidating competition history and recalculating ratings.

## 6. Protection sections

<!-- CRITICAL: Define boundaries for AI agent behavior. Review with the human. -->

### Always (invariants that must always hold)

- Validate imported competition data before storing (required fields, no duplicate entries, valid date/placement values). Entire import must pass validation or nothing is stored.
- Size categories (S/M/L) must be kept strictly separate — never mix ratings across categories.
- Rating recalculation must be deterministic and reproducible — same input data must produce the same ratings.
- All public pages must be server-rendered or statically generated for SEO.
- Keep competition result data immutable after import — edits go through a re-import or explicit correction workflow.
- Identity resolution alias table must be append-only — confirmed matches are never silently removed.

### Ask first (agent must stop and ask before proceeding)

- Before changing the Glicko-2 algorithm parameters or calculation logic.
- Before changing the database schema or adding migrations.
- Before adding new external dependencies or third-party services.
- Before changing the data import format or validation rules.
- Before modifying deployment or infrastructure configuration.
- Before changing the identity resolution matching logic or thresholds.

### Never (agent must not do these)

- Never store secrets (API keys, DB credentials) in source code.
- Never delete or overwrite imported competition data without explicit confirmation.
- Never modify the rating calculation to favor or penalize specific teams/countries.
- Never expose admin/import functionality to public users.
- Never commit directly to main branch — always use feature branches.
- Never auto-merge entities without admin confirmation — fuzzy matching creates candidates, not facts.
