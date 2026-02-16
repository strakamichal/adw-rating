# TO-BE — Vision and Scope

## 1. Vision

Build a web application for calculating and displaying performance ratings of agility teams (handler + dog) on a global scale. The application operates under the **AgilityDogsWorld** brand, which has an established community (43k Instagram, 20k Facebook followers). The rating database will serve as the primary driver for traffic to the AgilityDogsWorld website, which currently has no content.

**Primary goals:**

1. Provide an objective, data-driven ranking of agility teams worldwide using the Glicko-2 algorithm.
2. Offer each team an interactive profile (bio card) with result history and rating progression.
3. Drive the existing social media community to the website and create a reason for repeat visits.
4. Build the foundation for future monetization (sponsors, premium profiles, partnerships with event organizers).

### NFRs (non-functional requirements)

- **Performance**: [Target, e.g., "Responsive UI with dashboards loading under 2s at N users"]
- **Availability**: [Target, e.g., "99.9% uptime during business hours"]
- **Security**: [Requirements, e.g., "MFA, SSO, role-based access"]
- **Audit/Logging**: [What to log, retention period]
- **Localization**: [Supported languages]
- **Accessibility**: [Standards, e.g., WCAG 2.1 AA]

## 2. Scope

<!-- What areas/modules does this project cover? -->

[List all functional areas in scope. Example:
"The system covers: user management, training requirements, session planning,
attendance tracking, compliance dashboards, reporting, and data imports."]

## 3. Non-goals

<!-- What is explicitly OUT of scope? Be specific. -->

- [Non-goal 1, e.g., "Mobile native app — web responsive is sufficient"]
- [Non-goal 2, e.g., "Real-time collaboration features"]
- [Non-goal 3]

## 4. Key use cases

<!-- High-level use cases, not implementation details. 5-15 items. -->

- [Use case 1, e.g., "Create and manage user accounts with role assignments"]
- [Use case 2, e.g., "Define requirements with validity rules and assign to positions"]
- [Use case 3]
- [Use case 4]
- [Use case 5]

## 5. Change summary vs AS-IS

<!-- Only for REWRITE scenario. Delete this section for greenfield/feature. -->

### KEEP
- [What stays the same from the current system]

### CHANGE
- [What changes and how]

### REMOVE
- [What is being dropped]

### NEW
- [What is being added that didn't exist before]

## 6. Protection sections

<!-- CRITICAL: Define boundaries for AI agent behavior. Review with the human. -->

### Always (invariants that must always hold)

- [e.g., "All data queries must be scoped by tenant/organization ID"]
- [e.g., "Every mutation must be audit-logged"]
- [e.g., "User input must be validated on both client and server"]

### Ask first (agent must stop and ask before proceeding)

- [e.g., "Before changing database schema or adding migrations"]
- [e.g., "Before adding new external dependencies"]
- [e.g., "Before changing authentication/authorization logic"]
- [e.g., "Before modifying deployment configuration"]

### Never (agent must not do these)

- [e.g., "Never bypass authentication or authorization checks"]
- [e.g., "Never store secrets in source code"]
- [e.g., "Never delete production data or migrations"]
- [e.g., "Never commit directly to main/production branch"]
