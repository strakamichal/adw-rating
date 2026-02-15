# AgilityDogsWorld Rankings – Project Brief

**Version:** 1.0
**Date:** February 2026
**Author:** AgilityDogsWorld

---

## 1. Project Vision

Build a web application for calculating and displaying performance ratings of agility teams (handler + dog) on a global scale. The application will operate under the AgilityDogsWorld brand, which has an established community of 43,000 Instagram followers and 20,000 Facebook followers. The rating database is intended to serve as the primary driver for traffic to the AgilityDogsWorld website, which currently has no content.

## 2. Key Objectives

- Provide an objective, data-driven ranking of agility teams worldwide.
- Offer each team an interactive profile (bio card) with result history and rating progression.
- Drive the existing social media community to the website and create a reason for repeat visits.
- Build the foundation for future monetization (sponsors, premium profiles, partnerships with event organizers).

## 3. Core Entities

### 3.1 Competition Team (Handler + Dog)

Ratings are always calculated for a specific handler-dog pair. A single handler may have multiple dogs – each pair maintains its own independent rating. The team is the fundamental unit of the ranking system.

### 3.2 Handler

Handler profile includes: name, country, club/team, list of all dogs and their ratings (active and historical), career statistics.

### 3.3 Dog

Dog profile includes: name, breed, date of birth (if known), size category (S / M / L), list of handlers (typically one, but may change).

### 3.4 Competition

Competition metadata: name, date, location, country, organizer, tier (see section 5.2), list of categories and results.

## 4. Rating System

### 4.1 Algorithm: Glicko-2

The system uses the Glicko-2 rating with the following parameters for each competition team:

- **μ (rating)** – numerical performance value, starting at 1500.
- **σ (rating deviation)** – measure of rating uncertainty. New teams start with a high deviation (~350), which decreases as more competitions are completed.
- **τ (volatility)** – measure of performance consistency. A stable competitor has low volatility.

### 4.2 Competition Processing

Each competition is decomposed into pairwise comparisons. The finishing order determines who "beat" whom. If a team finishes 5th out of 30, it beat 25 teams and lost to 4. A Glicko-2 update is performed for each pair.

Disqualified or incomplete runs are counted as last place.

### 4.3 Separate Ratings by Size Category

Size categories S, M, and L are rated independently – ratings are not comparable across categories.

### 4.4 Inactivity and Decay

The rating itself **does not decay**. When a team stops competing, the rating (μ) remains frozen, but the deviation (σ) gradually increases – the system expresses growing uncertainty about the team's current strength.

The main ranking displays only **active teams** – defined as having completed at least 3 competitions in the last 12 months. Inactive teams remain in the database with their profile, history, and last known rating, but are excluded from the live ranking.

## 5. Competition Weighting

### 5.1 Automatic Weighting

Result weight is primarily determined by the **average rating of the starting field**. Winning a competition with strong opponents shifts the rating more than winning a weakly contested event. This mechanism requires no manual categorization – the system calibrates itself.

Additionally, **starting field size** is factored in – more competitors means more information for the system.

### 5.2 Tier System (Manual Multiplier)

For clearly identifiable major events, a tier multiplier is applied to the K-factor:

| Tier | Example Competitions | Multiplier |
|------|---------------------|------------|
| Tier 1 | FCI World Championship, EO, AWC | 2.0× |
| Tier 2 | National championships, major international events (Czech Open, Border Collie Classic) | 1.5× |
| Tier 3 | Championship qualifiers, regional cups, series events | 1.2× |
| Tier 4 | Regular weekend competitions | 1.0× |

Exact multiplier values will be finalized after testing on real data.

## 6. Data Sources

### 6.1 Phase 1 – MVP (Manual Collection)

Manually collected results from the last 2–3 years of major international events (FCI World Championship, EO, AWC, selected large open tournaments). On the order of dozens of competitions, hundreds of teams. Goal: a functional demo with real data and recognizable names.

### 6.2 Phase 2 – Crowdsourcing

Organizers and competitors can upload results via a simple upload interface (CSV/Excel). Motivation: a competitor wants to see their rating → they upload their results. The Glicko-2 system handles incomplete data gracefully – a competitor with fewer competitions in the system simply has a higher deviation (lower rating confidence).

### 6.3 Phase 3 – Automation

Automated import from online result sources where technically feasible (scraping, APIs, partnerships with competition management platforms).

## 7. Isolated Regional Bubbles

Competitors within a single country predominantly compete against each other. Ratings are reliable within a region but cross-region comparisons are initially imprecise. International competitions (World Championship, EO) serve as "bridges" for calibrating ratings across regions.

Solution: transparent communication to users – display "rating confidence" (based on σ and the number of cross-region competitions), and optionally provide regional rankings alongside the global one.

## 8. Team Profile (Bio Card)

Each profile contains:

- Basic info about the handler and dog (name, country, breed, category).
- Current rating with deviation (displayed as an interval, e.g. 1800 ± 75).
- Rating progression chart over time.
- Competition history – list of events, placements, clean/faulted runs.
- Statistics: clean run rate, average placement, number of competitions.
- Handler career overview – all dogs, the peak rating achieved with each.
- Status: active / inactive (with date of last competition).

## 9. Website – Structure and Features

### 9.1 Main Sections

- **Rankings** – interactive leaderboard filtered by category (S/M/L), region, and time period. The primary draw of the website.
- **Profiles** – team, handler, and dog bio cards with search functionality.
- **Competitions** – results, statistics, calendar of upcoming events.
- **Content** – analyses, comparisons, articles tied to IG/FB content.

### 9.2 Social Media Integration

The website is linked to the existing IG/FB channels. Typical flow: competition video on IG → link to website for detailed rating impact → competitor views their profile. The goal is a feedback loop between social media content and the website.

## 10. Future Monetization (Out of MVP Scope)

- Sponsorship partnerships (pet food brands, equipment, obstacle manufacturers).
- Premium profiles for competitors (extended statistics, historical trends, comparisons).
- Partnerships with event organizers ("verified" results).

## 11. MVP – Minimum Viable Product

### In Scope

- Ranking of top competitors calculated from results of major international events over the last 2–3 years.
- Basic team profiles with rating and result history.
- Filtering by size category.
- Simple, responsive website – no registration required for browsing.

### Out of Scope (Deferred to Later Phases)

- User-submitted result uploads.
- Automated data import.
- User accounts and authentication.
- Premium features.
- Competition calendar.

## 12. Open Questions

- Exact tier multiplier values – to be tested on real data.
- Specific Glicko-2 parameters (initial σ, σ growth during inactivity, rating period length) – to be calibrated.
- Definition of "active" competitor – 3 competitions in 12 months is the initial proposal, subject to adjustment.
- Legal aspects of using competitor names and results from publicly available sources.
- Technology stack for the website (frontend framework, database, hosting).
