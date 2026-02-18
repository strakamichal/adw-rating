# UI Structure

> **Note**: Web pages, routes, and components are defined in [04-architecture-and-interfaces.md, section 7](04-architecture-and-interfaces.md#7-web-pages-blazor-ssr). This document extends that section with navigation, acceptance criteria, key flows, and verification guides.

## 1. Navigation

### Main menu

| Section | Path | Access |
|---------|------|--------|
| Home | `/` | Public |
| Rankings | `/rankings` | Public |
| Competitions | `/competitions` | Public |

No admin UI in MVP — all admin operations are via CLI.

### Secondary navigation (in-page)

- **Rankings page**: Size category tabs (S / M / I / L), country filter dropdown, name search input
- **Competitions page**: Year filter, tier filter, country filter
- **Handler profile**: Dog/team selector to switch between teams

### Breadcrumb pattern

- Rankings: `Home > Rankings`
- Team profile: `Home > Rankings > {Handler} & {Dog}`
- Handler profile: `Home > Rankings > {Handler}`
- Competition detail: `Home > Competitions > {Competition Name}`

## 2. Screens

### Home — `/`

**Purpose**: Landing page with summary stats and entry points.

**Key elements**:
- Summary cards: qualified teams count, competitions count, total runs
- Quick links to rankings (one per size category)
- Brief explanation of the rating system

**Acceptance criteria**:
- [ ] Shows summary stats from `GET /api/rankings/summary`
- [ ] Links to `/rankings?size=S`, `/rankings?size=M`, etc. work
- [ ] Page loads under 2 s on 4G

---

### Rankings — `/rankings`

**Purpose**: Paginated leaderboard filtered by size, country, and name.

**Key elements**:
- Size category tabs (S / M / I / L) — `size` query param, required
- Country filter dropdown — `country` query param
- Name search input — `search` query param
- Data table: rank, trend arrow, handler name, dog name, country flag, normalized rating, tier label, run count
- Pagination controls

**Acceptance criteria**:
- [ ] Default view shows Large category leaderboard
- [ ] Switching size tab reloads data via Enhanced Navigation
- [ ] Country filter limits results to selected country
- [ ] Search filters by handler or dog name (min 2 chars)
- [ ] Pagination works correctly (page, pageSize query params)
- [ ] Provisional teams show "FEW RUNS" badge
- [ ] Tier labels (Elite, Champion, Expert, Competitor) displayed as badges
- [ ] Trend arrows show rank change (▲ up, ▼ down, NEW)
- [ ] Clicking a team row navigates to `/teams/{slug}`
- [ ] Empty state when no results match filters

---

### Team Profile — `/teams/{slug}`

**Purpose**: Detailed view of a single team (handler + dog).

**Key elements**:
- Bio card: handler name + country, dog name + breed, size category
- Rating display: normalized rating ± sigma, tier label
- Stats: run count, finished %, top3 %, average rank
- Rating progression chart (from `/api/teams/{slug}/history`)
- Competition history table (from `/api/teams/{slug}/results`, paginated)
- Inactive banner if `isActive = false`

**Acceptance criteria**:
- [ ] All bio card fields displayed correctly
- [ ] Rating chart renders with correct data points
- [ ] Competition history table shows: date, competition name, discipline, rank, faults, time, speed
- [ ] Clicking competition name navigates to `/competitions/{slug}`
- [ ] Clicking handler name navigates to `/handlers/{slug}`
- [ ] Inactive teams show "Inactive — not enough recent runs" banner
- [ ] 404 page for non-existent slug
- [ ] Open Graph meta tags set (title: "Handler & Dog — ADW Rating", description with rating)

---

### Handler Profile — `/handlers/{slug}`

**Purpose**: Overview of a handler's career across all dogs.

**Key elements**:
- Handler info: name, country
- Teams list: each team with dog name, size, current rating, peak rating, tier label, active status
- Dog selector: clicking a team shows run history + rating chart for that team

**Acceptance criteria**:
- [ ] All handler's teams displayed with current and peak rating
- [ ] Selecting a team shows run history table (reuses team results endpoint)
- [ ] Rating chart for selected team shows progression
- [ ] Clicking dog/team navigates to `/teams/{slug}`
- [ ] 404 page for non-existent slug
- [ ] Open Graph meta tags set

---

### Competition List — `/competitions`

**Purpose**: Browsable, searchable list of all imported competitions.

**Key elements**:
- Year filter dropdown
- Tier filter (Tier 1, Tier 2, All)
- Country filter dropdown
- Search input (competition name)
- Data table: date, name, location, country, tier, participant count
- Sorted by date descending (newest first)
- Pagination

**Acceptance criteria**:
- [ ] Shows all competitions sorted by date (newest first)
- [ ] Year filter works
- [ ] Tier filter works
- [ ] Country filter works
- [ ] Search by name works (min 2 chars)
- [ ] Clicking a row navigates to `/competitions/{slug}`
- [ ] Pagination works correctly

---

### Competition Detail — `/competitions/{slug}`

**Purpose**: Full results for a single competition.

**Key elements**:
- Competition header: name, dates, location, country, tier
- Runs grouped by: date (day 1, day 2, …) → size category → discipline
- Each run section expandable or pre-expanded with results table
- Results table columns: rank, handler, dog, country, faults, refusals, time faults, time, speed, eliminated

**Acceptance criteria**:
- [ ] Competition metadata displayed in header
- [ ] Runs grouped correctly by date/size/discipline
- [ ] Results table shows all columns from RunResultDto
- [ ] Clicking a team navigates to `/teams/{slug}`
- [ ] Eliminated teams shown at bottom with "ELIM" badge
- [ ] 404 page for non-existent slug

## 3. Role-based access

No roles in MVP. All pages are public, read-only. Admin operations are CLI-only.

| Screen | Public | Admin (CLI) |
|--------|--------|-------------|
| All web pages | View | — |
| Import / Recalculate / Merge | — | Full access |

## 4. Key UI flows

### Flow 1: Browse rankings and view team profile

1. Visitor opens `/rankings` (defaults to Large category)
2. Switches to Medium tab
3. Filters by country "CZE"
4. Sees filtered leaderboard
5. Clicks on a team row → navigates to `/teams/{slug}`
6. Views bio card, rating chart, competition history
7. Clicks a competition in the history → navigates to `/competitions/{slug}`

**Verification guide**:
1. Navigate to `/rankings`
2. Click "M" size tab → verify URL is `/rankings?size=M`
3. Select "CZE" in country filter → verify filtered results
4. Click first team → verify team profile loads
5. Verify rating chart renders
6. Click a competition in history → verify competition detail loads

### Flow 2: Search for a handler

1. Visitor uses the search bar (global search)
2. Types a handler name (e.g., "Tercova")
3. Search results appear: matching teams, handlers, competitions
4. Clicks handler result → navigates to `/handlers/{slug}`
5. Views all teams, selects one → sees run history and rating chart

**Verification guide**:
1. Type "Tercova" in the search bar
2. Verify results include handler and team matches
3. Click handler result → verify handler profile loads
4. Click a team → verify run history appears

### Flow 3: Browse competition results

1. Visitor opens `/competitions`
2. Filters by year 2024
3. Clicks "AWC 2024" → navigates to `/competitions/awc2024`
4. Sees results grouped by day → size → discipline
5. Expands Large Agility Run 1
6. Sees full results table with placements, times, faults
7. Clicks a team → navigates to team profile

**Verification guide**:
1. Navigate to `/competitions`
2. Select year 2024 → verify filtered list
3. Click "AWC 2024" → verify competition detail loads
4. Verify runs are grouped by date/size/discipline
5. Verify results table has all expected columns
6. Click a team → verify team profile loads

## 5. Export and download UX

No exports in MVP. Future consideration: CSV export of rankings or competition results.
