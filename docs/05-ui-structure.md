# UI Structure

> **Note**: Web pages, routes, and components are defined in [04-architecture-and-interfaces.md, section 7](04-architecture-and-interfaces.md#7-web-pages-blazor-ssr). This document extends that section with detailed UI design, navigation, acceptance criteria, key flows, and verification guides.

## 1. Design Principles

1. **Numbers become stories** — every data point is shown in context that makes it meaningful. A rating of 1687 means nothing alone; "#4 in Large, Elite tier, +23 since last competition" tells a story.
2. **Progressive disclosure** — show the most compelling information first, let users drill deeper on demand. Leaderboard shows rating + trend; click through for full history.
3. **Shareability by default** — team profiles are designed to be screenshot-worthy. Competitors share their "player cards" on Instagram/Facebook.
4. **Mobile-first** — majority of traffic comes from social media (43k IG, 20k FB followers) = mobile devices. Every screen must work well on phone screens first.

## 2. Navigation and Layout

### Header (persistent across all pages)

| Element | Desktop | Mobile |
|---------|---------|--------|
| Logo | "ADW Rating" brand mark, links to `/` | Compact logo |
| Nav links | Rankings, Competitions | Hamburger menu |
| Search | Always-visible search input in header | Search icon → expands to full-width input |

### Footer

- "How it works" link → `/how-it-works` (rating methodology and disclaimers)
- Social media links (Instagram, Facebook)
- "Data from major international agility competitions" disclaimer

### Breadcrumbs

| Page | Breadcrumb |
|------|------------|
| Rankings | `Home > Rankings` |
| Team profile | `Home > Rankings > {Handler} & {Dog}` |
| Handler profile | `Home > Rankings > {Handler}` |
| Competition detail | `Home > Competitions > {Competition Name}` |

On mobile, breadcrumbs collapse to a back link: `< Back to Rankings`.

### Navigation routes

| Section | Path | Access |
|---------|------|--------|
| Home | `/` | Public |
| Rankings | `/rankings` | Public |
| Competitions | `/competitions` | Public |
| How It Works | `/how-it-works` | Public |

No admin UI in MVP — all admin operations are via CLI.

## 3. Screens

### Home — `/`

**Purpose**: A living dashboard that changes after every competition import. Answers: "Who's at the top? What changed? What happened recently?"

**Sections (top to bottom):**

**3.1 Hero area**
- Headline: "The Global Agility Rating"
- One-sentence tagline explaining the system
- Large, prominent search bar — primary entry point for visitors arriving from social media who want to find a specific team or handler
- Data source: search bar uses `GET /api/search`

**3.2 Summary stats**
- Three large stat cards in a row: **Qualified Teams**, **Competitions Tracked**, **Total Runs**
- Each displayed as a large bold number with a label below
- Data source: `GET /api/rankings/summary` → `RankingSummary(QualifiedTeams, Competitions, Runs)`

**3.3 Top Teams Spotlight**
- Four columns on desktop / tabs on mobile: S / M / I / L
- Each shows the top 3 teams:
  - Rank number, handler name, country flag, dog call name, rating, tier badge
  - The #1 team in each category is visually emphasized (larger card)
- "View full rankings →" link below each column
- Data source: `GET /api/rankings?size=X&pageSize=3` (one call per category)

**3.4 Recent Movers**
- Horizontal scrollable card row showing teams with the biggest rank improvement
- Each card: handler + dog name, country flag, rank change magnitude (e.g., "▲ 12 positions"), new rating, tier badge
- Derived from `TeamRankingDto.Rank` vs `TeamRankingDto.PrevRank` — load one page of Large rankings, pick teams with largest positive `PrevRank - Rank` delta
- Data source: computed from `GET /api/rankings?size=L&pageSize=50`

**3.5 Recent Competitions**
- Last 5 competitions: name, dates (formatted, e.g., "Oct 3–6, 2024"), country flag, tier badge ("Major" for Tier 1), participant count
- "View all competitions →" link
- Data source: `GET /api/competitions?pageSize=5`

**3.6 About the Rating**
- 2–3 sentence explanation: "Ratings are based on the PlackettLuce model, updated after each competition. Higher rating = stronger performance against tough competition. Ratings are comparable across all size categories."
- "Learn more →" link to `/how-it-works`

**Acceptance criteria**:
- [ ] Summary stats load from `GET /api/rankings/summary`
- [ ] Top 3 teams displayed per size category with correct data
- [ ] Size category tabs/columns switch correctly
- [ ] Recent movers section shows teams with biggest positive rank change
- [ ] Recent competitions section shows last 5 with correct metadata
- [ ] Search bar triggers instant search dropdown (see section 8)
- [ ] Links to `/rankings?size=S`, `/rankings?size=M`, etc. work
- [ ] Page loads under 2 s on 4G

---

### Rankings — `/rankings`

**Purpose**: The definitive leaderboard — a filterable, paginated ranking of active teams within a size category.

**3.7 Filter bar (sticky on scroll)**
- **Size category selector**: four pill buttons (S / M / I / L), `size` query param, **required**. Default: `L` (Large — the most popular category)
- **Country filter**: dropdown with country flags, `country` query param. Default: "All Countries"
- **Name search**: input field, `search` query param. Filters by handler or dog name. Minimum 2 characters, with debounce
- All filter values reflected in URL query params for shareability and bookmarkability

**3.8 Summary row**
- Dynamic text below filter bar, above table: "Showing {count} qualified teams in {size category}"
- If country filtered: "Showing {count} {country} teams in {size category}"

**3.9 Rankings table**

| Column | Content | Notes |
|--------|---------|-------|
| **#** | Rank number | Bold, prominent |
| **Trend** | Rank change indicator | ▲ N (green) = moved up N positions, ▼ N (red) = moved down, — (gray) = unchanged, NEW (blue badge) = first appearance. Computed from `Rank` vs `PrevRank` |
| **Team** | Handler name (primary) + dog call name (secondary, lighter). Country flag next to handler | Entire cell clickable → `/teams/{slug}` |
| **Rating** | `Rating` as bold number (e.g., "1687"). Below in smaller text: ± uncertainty range | From `TeamRankingDto.Rating` and `Sigma` |
| **Tier** | Badge: Elite (gold), Champion (silver), Expert (bronze), Competitor (no badge) | From `TeamRankingDto.TierLabel` |
| **Runs** | `RunCount`, optionally with a small fraction showing `Top3RunCount`/`RunCount` | Shows volume and quality at a glance |

**Row styling**:
- Elite teams: subtle gold left-border or background tint
- Champion teams: subtle silver left-border
- Provisional teams (`IsProvisional`): slightly muted/faded, with "FEW RUNS" badge next to tier
- Entire row is clickable (navigates to team profile)

**3.10 Tier distribution** (below table)
- Horizontal stacked bar or simple counts: "Elite: 7 | Champion: 27 | Expert: 88 | Competitor: 220"
- Gives context to what the tiers mean in absolute numbers for the current filter

**Pagination**: page numbers + prev/next, showing "Page {n} of {total}". Query params: `page`, `pageSize`.

**Acceptance criteria**:
- [ ] Default view shows Large category leaderboard
- [ ] Switching size tab reloads data via Enhanced Navigation
- [ ] Country filter limits results to selected country
- [ ] Search filters by handler or dog name (min 2 chars)
- [ ] Pagination works correctly (page, pageSize query params)
- [ ] Trend indicators show rank change with direction and magnitude
- [ ] NEW badge shown for teams with `PrevRank = null`
- [ ] Provisional teams show "FEW RUNS" badge
- [ ] Tier labels displayed as styled badges (Elite gold, Champion silver, Expert bronze)
- [ ] Elite/Champion rows have visual distinction (left-border or tint)
- [ ] Clicking a team row navigates to `/teams/{slug}`
- [ ] Empty state when no results match filters: "No teams match your filters" with suggestion to broaden search
- [ ] Filter bar stays visible when scrolling (sticky)
- [ ] URL reflects current filter state (shareable)

---

### Team Profile — `/teams/{slug}`

**Purpose**: The "player card" — the most shareable page. Designed so competitors screenshot and share on Instagram.

**3.11 Hero Card**

The top section, visually distinct, designed to be screenshot-worthy.

| Left side | Right side |
|-----------|------------|
| Avatar circle with handler initials (large) | **Rating**: large number (`Rating`, e.g., "1687") |
| Handler name + country flag | ± sigma uncertainty below rating |
| Dog call name | Tier badge (Elite/Champion/Expert/Competitor) |
| Breed (if available) | Trend: rating change (`Rating - PrevRating`), e.g., "+23 ▲" or "-12 ▼" |
| Size category badge (S/M/I/L) | Peak rating if different from current: "Peak: 1723" *(requires adding `PeakRating` to `TeamDetailDto`)* |

- If `IsProvisional`: "FEW RUNS" badge on the card
- If `!IsActive`: prominent "INACTIVE" banner across the card with text "Inactive — not enough recent runs"

Data source: `GET /api/teams/{slug}` → `TeamDetailDto`

**3.12 Quick Stats Row**

Four stat cards in a horizontal row:

| Stat | Display | Source |
|------|---------|--------|
| Runs | `RunCount` (e.g., "47") | `TeamDetailDto.RunCount` |
| Finish Rate | Percentage (e.g., "94%") | `TeamDetailDto.FinishedPct` |
| Podium Rate | Percentage (e.g., "38%") | `TeamDetailDto.Top3Pct` |
| Avg Rank | Number (e.g., "3.2") | `TeamDetailDto.AvgRank` |

Each card: large number + small label below.

**3.13 Rating Progression Chart**

- Line chart showing `Rating` over time
- X-axis: dates of competitions
- Y-axis: rating value
- Data points: dots on the line, hoverable/tappable to show: date, rating at that point. *(Note: `RatingSnapshot` contains `CompetitionId` but not `CompetitionName` — either denormalize the name into a chart DTO, or show only date + rating in tooltips for MVP)*
- Uncertainty band: shaded area around the line (rating ± sigma), visually narrowing as sigma decreases — communicates growing confidence
- Peak rating: marked with a special indicator (star) and labeled "Peak: {value}" if it differs from current rating
- Data source: `GET /api/teams/{slug}/history` → `IReadOnlyList<RatingSnapshot>`

**3.14 Competition History Table**

Paginated table, newest results first.

| Column | Content | Notes |
|--------|---------|-------|
| **Date** | Competition date | From `TeamResultDto.Date` |
| **Competition** | Name (clickable → `/competitions/{slug}`) | Tier 1 competitions show small "Major" badge next to name |
| **Discipline** | Agility / Jumping / Final | Team rounds show small "Team" tag |
| **Rank** | Placement number, or "ELIM" badge if eliminated | |
| **Faults** | Obstacle faults | |
| **Time** | Run time in seconds | |
| **Speed** | Speed in m/s | |

**Row styling**:
- Rank 1: gold accent (left-border or subtle background)
- Rank 2–3: silver/bronze accent
- Eliminated: grayed out with "ELIM" badge

Pagination: "Showing 1–20 of 47 results".
Data source: `GET /api/teams/{slug}/results` → `PagedResult<TeamResultDto>`

**3.15 Handler link**

"See all teams by {Handler Name} →" link at the bottom, navigates to `/handlers/{slug}`.

**3.16 Open Graph meta tags**

- `og:title`: "{Handler} & {Dog} — ADW Rating"
- `og:description`: "Rating: {Rating} | {TierLabel} | {SizeCategory} | {RunCount} runs"
- `og:url`: canonical URL for this team

**Acceptance criteria**:
- [ ] Hero card displays all fields from `TeamDetailDto` correctly
- [ ] Rating shown as large prominent number with sigma uncertainty
- [ ] Tier badge styled correctly (gold/silver/bronze)
- [ ] Trend indicator shows rating change (positive green, negative red)
- [ ] Peak rating shown when it differs from current
- [ ] "FEW RUNS" badge shown when `IsProvisional = true`
- [ ] "INACTIVE" banner shown when `IsActive = false`
- [ ] Quick stats row shows all four stats
- [ ] Rating progression chart renders with correct data points from history endpoint
- [ ] Chart data points are hoverable/tappable with tooltip (date, rating)
- [ ] Uncertainty band (sigma) visible in chart
- [ ] Competition history table shows all columns from `TeamResultDto`
- [ ] Clicking competition name navigates to `/competitions/{slug}`
- [ ] Clicking handler name navigates to `/handlers/{slug}`
- [ ] Podium rows highlighted (gold/silver/bronze)
- [ ] Eliminated rows grayed out with "ELIM" badge
- [ ] Pagination works on competition history
- [ ] 404 page for non-existent slug
- [ ] Open Graph meta tags set correctly
- [ ] Page is responsive — hero card stacks vertically on mobile

---

### Handler Profile — `/handlers/{slug}`

**Purpose**: Career page showing a handler's full agility journey across all their dogs.

**3.17 Handler header**

- Handler name (large), country flag
- Subtitle: "{N} teams" (total team count)

Data source: `GET /api/handlers/{slug}` → `HandlerDetailDto`

**3.18 Teams as cards**

Card layout (not a table) — one card per team (dog):

| Card element | Source |
|-------------|--------|
| Dog call name (large) | `HandlerTeamSummaryDto.DogCallName` |
| Breed (smaller, if available) | `HandlerTeamSummaryDto.DogBreed` |
| Size category badge | `HandlerTeamSummaryDto.SizeCategory` |
| Current rating (large number) | `HandlerTeamSummaryDto.Rating` |
| Peak rating (smaller, labeled "Peak") | `HandlerTeamSummaryDto.PeakRating` |
| Tier badge | `HandlerTeamSummaryDto.TierLabel` |
| Run count | `HandlerTeamSummaryDto.RunCount` |
| Active/Inactive indicator | `HandlerTeamSummaryDto.IsActive` |
| "FEW RUNS" badge | *Requires adding `IsProvisional` to `HandlerTeamSummaryDto`* |

**Card ordering**: active teams first (sorted by `Rating` desc), then inactive teams (sorted by `PeakRating` desc). Inactive cards are visually muted.

**Card interaction**:
- Each card is clickable → navigates to `/teams/{slug}`
- On desktop: clicking a card can optionally expand an inline detail panel below it (without navigating away) showing a mini rating chart + last 10 results, with a "View full profile →" link. Data comes from `GET /api/teams/{slug}/history` and `GET /api/teams/{slug}/results?pageSize=10`
- On mobile: tapping a card navigates directly to the team profile

**3.19 Open Graph meta tags**

- `og:title`: "{Handler} — ADW Rating"
- `og:description`: "{N} teams | Best rating: {highest Rating}"

**Acceptance criteria**:
- [ ] Handler name and country displayed with flag
- [ ] All teams displayed as cards with correct data from `HandlerTeamSummaryDto`
- [ ] Cards show current and peak rating
- [ ] Cards sorted: active first (by rating desc), inactive second (by peak desc)
- [ ] Inactive cards visually muted
- [ ] Clicking a card navigates to `/teams/{slug}`
- [ ] Desktop: expandable inline detail with mini chart and recent results (optional progressive enhancement)
- [ ] 404 page for non-existent slug
- [ ] Open Graph meta tags set correctly

---

### Competition List — `/competitions`

**Purpose**: Browsable, searchable archive of all imported competitions. Feels like a sports event timeline.

**3.20 Filter bar**

- **Year selector**: pills for recent years (e.g., 2024, 2025, 2026), `year` query param
- **Tier filter**: "Major Events" (Tier 1) / "All" / "Standard" (Tier 2), `tier` query param
- **Country filter**: dropdown with country flags, `country` query param
- **Search**: input for competition name, `search` query param (min 2 chars)

**3.21 Competition list**

Grouped by year with visual year headers (e.g., "2025", "2024"). Within each year, sorted by date descending.

Each competition entry:

| Element | Source |
|---------|--------|
| Competition name (large, clickable → `/competitions/{slug}`) | `CompetitionDetailDto.Name` |
| Dates: formatted range, e.g., "Oct 3–6, 2024" | `Date`, `EndDate` |
| Location: city + country flag | `Location`, `Country` |
| Tier badge: "Major" (gold) for Tier 1, no badge for Tier 2 | `Tier` |
| Participant count | `ParticipantCount` |

Major events (Tier 1) have a subtle visual distinction — slightly larger card or gold accent.

Pagination below. Data source: `GET /api/competitions` → `PagedResult<CompetitionDetailDto>`

**Acceptance criteria**:
- [ ] Competitions shown sorted by date (newest first), grouped by year
- [ ] Year filter works
- [ ] Tier filter works (Major / All / Standard)
- [ ] Country filter works
- [ ] Search by name works (min 2 chars)
- [ ] Major events visually distinct (badge + accent)
- [ ] Clicking a competition navigates to `/competitions/{slug}`
- [ ] Pagination works correctly
- [ ] Empty state when no competitions match filters

---

### Competition Detail — `/competitions/{slug}`

**Purpose**: Full results for a single competition. Answers: "What happened at this event?"

**3.22 Competition header**

- Competition name (large)
- Dates (formatted range), location + country flag
- Tier badge ("Major" for Tier 1)
- Summary stats: "{ParticipantCount} teams | {RunCount} runs"

Data source: `GET /api/competitions/{slug}` → `CompetitionDetailDto`

**3.23 Run navigation**

Runs are grouped hierarchically: **Day → Size category → Discipline**.

- For multi-day competitions: day tabs or anchors (Day 1, Day 2, etc.)
- Within each day: size category sections. **Display uses the original source category** (`Run.OriginalSizeCategory`) when available — e.g., WAO shows "250 / 300 / 400 / 500 / 600", AKC shows "20 inch / 24 inch". Falls back to FCI labels (S / M / I / L) when `OriginalSizeCategory` is null.
- Within each size: discipline sections (Agility, Jumping, Final)
- On mobile: accordion pattern (tap to expand/collapse sections)

Run list data source: `GET /api/competitions/{slug}/runs` → `IReadOnlyList<RunSummaryDto>`

**3.24 Results tables**

Each run section has a header (e.g., "Day 1 — Large — Agility — Run 1") and a results table.

| Column | Content | Notes |
|--------|---------|-------|
| **#** | Rank | |
| **Team** | Handler name + dog call name, country flag | Clickable → `/teams/{slug}` |
| **Faults** | Obstacle faults | |
| **Refusals** | Refusal count | *Conditionally hidden if all zero* |
| **Time Faults** | Time penalty | *Conditionally hidden if all zero* |
| **Time** | Run time in seconds | |
| **Speed** | Speed in m/s | |
| **Status** | "ELIM" badge for eliminated teams | |

**Row styling**:
- Rank 1: gold accent
- Rank 2: silver accent
- Rank 3: bronze accent
- Eliminated: grayed out, shown at bottom of table

**Loading strategy (performance)**:
- Run list (`/runs`) loads on page load — this provides the structure (hierarchy of days/sizes/disciplines)
- Results for each run load **on-demand** when the section is expanded: `GET /api/competitions/{slug}/runs/{roundKey}/results`
- Default: first run expanded, rest collapsed (for long competitions with 20+ runs)
- Shows spinner while results are loading

Data source: `GET /api/competitions/{slug}/runs/{roundKey}/results` → `IReadOnlyList<RunResultDto>`

**Acceptance criteria**:
- [ ] Competition metadata displayed in header with tier badge
- [ ] Summary stats (teams, runs) shown
- [ ] Runs grouped correctly by date → size → discipline
- [ ] Run sections are collapsible (accordion)
- [ ] First run expanded by default, rest collapsed
- [ ] Results load on-demand when section is expanded (lazy loading)
- [ ] Results table shows all relevant columns from `RunResultDto`
- [ ] Refusals/time faults columns hidden when all values are zero
- [ ] Top 3 placements highlighted (gold/silver/bronze)
- [ ] Eliminated teams shown at bottom with "ELIM" badge, grayed out
- [ ] Clicking a team navigates to `/teams/{slug}`
- [ ] Loading spinner shown while results are fetching
- [ ] 404 page for non-existent slug

---

### How It Works — `/how-it-works`

**Purpose**: Transparent explanation of the rating system for competitors and fans. Builds trust by being upfront about methodology and limitations. Linked from the footer ("About" link) and from the home page "About the Rating" section.

**3.25 Content sections (static page, no API calls):**

**Hero**
- Title: "How the Rating Works"
- Subtitle: "An independent, community-driven ranking — not affiliated with any official organization"

**What is ADW Rating?**
- Brief explanation: independent rating of agility teams based on competition results
- Not official, not affiliated with FCI, AKC, or any governing body
- Open about methodology — anyone can understand how ratings are calculated

**How ratings are calculated**
- PlackettLuce model explained in plain language: "Each run is a head-to-head comparison. Beat strong teams → your rating goes up more. Lose to weaker teams → your rating goes down more."
- What affects rating: placement in each run, strength of competitors in that run, competition tier (Major events count more)
- What doesn't affect rating: time or faults directly (only placement matters), breed, country
- Rating scale: centered around 1500, higher = better. ~68% of teams fall between 1350–1650.
- Size categories: S, M, I, L — each dog competes in one category. When dogs from different categories meet in the same run, they're all rated against each other. Display ratings are then normalized per category so they're comparable across S, M, I, and L

**Data sources**
- List of competition types included (FCI, WAO, AKC, USDAA, UKI, IFCS)
- "We include major international competitions from the last 2–3 years"
- Transparency: not all competitions are included — data is manually imported

**Size categories and cross-organization competitions**
- FCI uses S/M/I/L based on dog height at withers. Other organizations (AKC, USDAA, WAO) use different height categories.
- "Each dog is assigned to one FCI category (S/M/I/L) based on their most recent run. Their rating appears in that category's leaderboard."
- "When a competition mixes dogs from different categories in one run (e.g., WAO 500 includes both Intermediate and Large dogs), all competitors are rated against each other. The placement is real — they ran the same course."
- Table showing the approximate mapping from non-FCI categories to FCI (simplified version of the one in `docs/03-domain-and-data.md`)
- "AKC Preferred heights are excluded because dogs jump lower obstacles, making results incomparable."

**Limitations and disclaimers**
- "ADW Rating is an independent project. It is not endorsed by or affiliated with FCI, AKC, USDAA, UKI, IFCS, or any other organization."
- "Ratings are only as good as the data. We rely on publicly available competition results. Errors in source data may affect ratings."
- "The non-FCI size category mapping is approximate. Dogs near category boundaries may be placed in a different category than expected."
- "Not all competitions are included. A team's true strength may not be fully reflected if they primarily compete at events not yet in our database."
- "Rating is based on placement, not absolute performance. A clean run at a weak competition may count less than a faulted run at a strong one."
- "When dogs from different size categories compete in the same run (e.g., at WAO), they are rated against each other directly. Because size categories mostly compete separately, the internal rating scales between categories may not be perfectly aligned. This has minimal impact — each category has its own leaderboard, and display ratings are normalized independently."

**Acceptance criteria**:
- [ ] Page is accessible at `/how-it-works`
- [ ] Footer "About" link points to this page
- [ ] Home page "About the Rating" section links here
- [ ] All content is static (no API calls)
- [ ] Page is server-rendered for SEO
- [ ] Size category mapping table is present with disclaimer
- [ ] "Not affiliated" disclaimer is prominent
- [ ] Page is responsive

---

## 4. Global Search

### Placement

- **Header**: always-visible search input on desktop, search icon on mobile (expands to full-width input on tap)
- **Home page**: also shown as a large, prominent search bar in the hero section

### Search behavior

- Triggers on 2+ characters typed, with debounce
- Shows an instant dropdown below the search bar with results grouped by type:

| Group | Display per result |
|-------|-------------------|
| **Teams** | `DisplayName` (handler + dog name), `Subtitle` (rating + country) |
| **Handlers** | `DisplayName` (name), `Subtitle` (country) |
| **Competitions** | `DisplayName` (name), `Subtitle` (date) |

*Note: `SearchResult` has `DisplayName` and `Subtitle` (string). Tier badges and country flags are not available from the search endpoint — the subtitle carries the key info as text (e.g., "1687 | CZE"). If richer search results are desired, `SearchResult` DTO would need to be extended.*

- Each result is clickable → navigates to the appropriate detail page
- Max ~10 results in dropdown
- Empty state: "No results found for '{query}'"

Data source: `GET /api/search?q={query}&limit=10` → `IReadOnlyList<SearchResult>`

**Acceptance criteria**:
- [ ] Search bar visible in header on all pages
- [ ] Search triggers after 2+ characters
- [ ] Dropdown shows results grouped by type (teams, handlers, competitions)
- [ ] Each result is clickable and navigates to correct page
- [ ] Empty state shown when no results match
- [ ] Search is performant (results appear within 300 ms)

---

## 5. Cross-cutting UI Elements

These design patterns are applied consistently across all screens.

### Country flags

Small flag icons (emoji flags or lightweight SVG set) displayed next to all country codes throughout the site. Adds visual interest and instant recognition.

### Tier badges

Consistent badge styling across all pages:

| Tier | Style |
|------|-------|
| Elite | Gold background, dark text |
| Champion | Silver background, dark text |
| Expert | Bronze/copper background, dark text |
| Competitor | No badge or very subtle gray |

Always the same size and position for scannability.

### Rating change indicators

| State | Display |
|-------|---------|
| Improved | Green ▲ + number of positions (e.g., "▲ 5") |
| Declined | Red ▼ + number of positions (e.g., "▼ 3") |
| Unchanged | Gray dash (—) |
| New entry | Blue "NEW" badge |

Used on: rankings table, team profile hero card.

### Provisional badge

"FEW RUNS" — small tag/badge shown wherever a rating is displayed for a provisional team (`IsProvisional = true`). Communicates that the rating has higher uncertainty.

### Loading states

- **Initial page load**: skeleton screens (gray placeholder blocks in the shape of expected content)
- **Lazy-loaded content** (e.g., expanding competition run sections): spinner
- **Enhanced Navigation transitions**: near-instant partial DOM swap (built into Blazor SSR)

### Empty states

- **No results**: "No teams match your filters" with suggestion to broaden search or clear filters
- **No competitions found**: "No competitions found" with filter reset suggestion
- All empty states include a clear call-to-action to help the user recover

---

## 6. SEO and Social Sharing

### Page titles

| Page | Title |
|------|-------|
| Home | ADW Rating — Global Agility Team Rankings |
| Rankings | {Size} Rankings — ADW Rating |
| Team profile | {Handler} & {Dog} — ADW Rating |
| Handler profile | {Handler} — ADW Rating |
| Competition list | Competitions — ADW Rating |
| Competition detail | {Competition Name} — ADW Rating |

### Meta descriptions

| Page | Description template |
|------|---------------------|
| Team | "Rating: {Rating} | {TierLabel} | {SizeCategory} category | {RunCount} runs | {Top3Pct}% podium rate" |
| Handler | "{N} teams | Best rating: {highest Rating} ({TierLabel})" |
| Competition | "{Date range} | {Location}, {Country} | {Tier label} | {ParticipantCount} teams" |

### Open Graph tags

Set on team and handler profiles (detailed in sections 3.16 and 3.19). Include `og:title`, `og:description`, `og:url`. A branded fallback image is used for `og:image` (dynamic OG images are a future enhancement).

### URL structure

All URLs are clean, human-readable, and bookmarkable:
- `/rankings?size=L&country=CZE&page=2` — rankings with filters
- `/teams/john-smith-rex` — team profile (slug-based)
- `/handlers/john-smith` — handler profile
- `/competitions/awc2024` — competition detail

---

## 7. Role-based Access

No roles in MVP. All pages are public, read-only. Admin operations are CLI-only.

| Screen | Public | Admin (CLI) |
|--------|--------|-------------|
| All web pages | View | — |
| Import / Recalculate / Merge | — | Full access |

---

## 8. Key UI Flows

### Flow 1: Browse rankings and view team profile

1. Visitor opens `/rankings` (defaults to Large category)
2. Switches to Medium tab → URL updates to `/rankings?size=M`
3. Filters by country "CZE" → URL updates to `/rankings?size=M&country=CZE`
4. Sees filtered leaderboard with trend indicators and tier badges
5. Clicks on a team row → navigates to `/teams/{slug}`
6. Views hero card with rating, trend, and tier badge
7. Scrolls to rating progression chart — hovers data points to see competition names
8. Scrolls to competition history — clicks a competition → navigates to `/competitions/{slug}`

**Verification guide**:
1. Navigate to `/rankings`
2. Verify Large category is selected by default
3. Click "M" size tab → verify URL is `/rankings?size=M` and data reloads
4. Select "CZE" in country filter → verify filtered results and URL update
5. Verify trend indicators show direction and magnitude
6. Verify tier badges are styled (gold/silver/bronze)
7. Click first team → verify team profile loads with hero card
8. Verify rating chart renders with data points
9. Hover a chart data point → verify tooltip shows date + competition + rating
10. Click a competition in history → verify competition detail loads

### Flow 2: Find yourself via search (social media arrival)

1. Visitor arrives from an Instagram link to the home page
2. Types their name in the prominent search bar (e.g., "Tercova")
3. Search dropdown appears with grouped results: teams, handlers
4. Clicks their team result → navigates to `/teams/{slug}`
5. Screenshots the hero card to share on Instagram
6. Scrolls down to view rating progression and competition history

**Verification guide**:
1. Navigate to `/`
2. Verify search bar is prominently visible in hero section
3. Type "Tercova" → verify dropdown appears with grouped results
4. Verify results include team and handler matches with ratings and flags
5. Click a team result → verify team profile loads
6. Verify hero card displays all info (rating, tier, trend, stats)

### Flow 3: Browse competition results

1. Visitor opens `/competitions`
2. Filters by year 2024
3. Sees competitions grouped by year with "Major" badges on Tier 1 events
4. Clicks "AWC 2024" → navigates to `/competitions/awc2024`
5. Sees header with competition info and summary stats
6. First run section is expanded with results table — top 3 highlighted
7. Expands another run section → results load on demand
8. Clicks a team in results → navigates to team profile

**Verification guide**:
1. Navigate to `/competitions`
2. Select year 2024 → verify filtered list, grouped by year
3. Verify Major events have gold badge/accent
4. Click "AWC 2024" → verify competition detail loads
5. Verify header shows dates, location, tier badge, team/run counts
6. Verify first run section is expanded with results
7. Verify top 3 placements highlighted (gold/silver/bronze)
8. Click a collapsed run section → verify results load with spinner
9. Click a team → verify team profile loads

### Flow 4: Explore a handler's career

1. Visitor is on a team profile and clicks "See all teams by {Handler}" link
2. Handler profile loads with all teams as cards
3. Visitor sees current and peak rating for each dog
4. Clicks on a different dog card → navigates to that team's profile
5. Compares ratings across the handler's different dogs

**Verification guide**:
1. Navigate to a team profile
2. Click "See all teams by {Handler}" → verify handler profile loads
3. Verify all teams shown as cards with current rating, peak rating, tier badge
4. Verify active teams sorted by rating, inactive teams after them (muted)
5. Click a different team card → verify that team profile loads

---

## 9. Export and Download UX

No exports in MVP. Future consideration: CSV export of rankings or competition results.
