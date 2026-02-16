# UI Structure

<!-- AI AGENT: To fill this document:
1. Derive screens from the scope in 02-to-be.md and use cases
2. Navigation should reflect the main modules
3. Role-based access comes from domain model (roles, permissions)
4. Key flows should trace a complete user journey (not just one screen)
5. Every screen needs acceptance criteria — these become your E2E test specs
6. Verification guides help reviewers (human or AI) validate the implementation
-->

## 1. Navigation

<!-- Define the main menu structure and navigation hierarchy. -->

### Main menu

| Section | Path | Icon | Access |
|---------|------|------|--------|
| [Home/Dashboard] | `/` | [icon] | All authenticated users |
| [Module 1] | `/[module1]` | [icon] | [Roles] |
| [Module 2] | `/[module2]` | [icon] | [Roles] |
| [Settings] | `/settings` | [icon] | Admin |

### Breadcrumb pattern

`[Home] > [Module] > [Entity List] > [Entity Detail]`

## 2. Screens

<!-- List all screens per module with their purpose and key elements. -->

### [Module 1]

#### [Entity] List — `/[module]/[entities]`

**Purpose**: Browse, search, and filter [entities].

**Key elements**:
- Search/filter bar: [filterable fields]
- Data table with columns: [column list]
- Pagination
- Actions: [New, Edit, Delete, Export]

**Acceptance criteria**:
- [ ] Shows paginated list with columns: [list columns]
- [ ] Filter by [field] works correctly
- [ ] Sort by [field] works correctly
- [ ] Empty state shows appropriate message
- [ ] Loading state visible during data fetch
- [ ] "New" button navigates to create form
- [ ] Row click navigates to detail view
- [ ] Export to [format] downloads correct data

#### [Entity] Detail — `/[module]/[entities]/{id}`

**Purpose**: View and edit a single [entity].

**Key elements**:
- Read-only detail view with key fields
- Edit button → switches to edit mode or navigates to form
- Related entities section (tabs or sections)
- Action buttons: [Save, Cancel, Delete]

**Acceptance criteria**:
- [ ] Displays all entity fields correctly
- [ ] Edit mode enables form fields
- [ ] Save validates and persists changes
- [ ] Cancel discards unsaved changes
- [ ] Delete asks for confirmation before proceeding
- [ ] Related entities shown in [tabs/sections]

#### [Entity] Form — `/[module]/[entities]/new` or `/{id}/edit`

**Purpose**: Create or edit an [entity].

**Key elements**:
- Form fields matching entity definition from domain model
- Validation messages per field
- Save / Cancel buttons

**Acceptance criteria**:
- [ ] All required fields are marked
- [ ] Validation errors shown inline per field
- [ ] Successful save redirects to [detail/list]
- [ ] Server-side validation errors displayed
- [ ] Form pre-filled in edit mode

---

<!-- Copy the screen templates above for each module. -->

### [Module 2]

<!-- ... -->

## 3. Role-based access

<!-- Define which roles can see and do what. -->

| Screen / Action | [Role 1, e.g., Admin] | [Role 2, e.g., Manager] | [Role 3, e.g., User] |
|----------------|----------------------|------------------------|---------------------|
| [Entity] List | View, Create, Delete | View, Create | View |
| [Entity] Detail | View, Edit, Delete | View, Edit | View |
| Settings | Full access | — | — |

## 4. Key UI flows

<!-- Describe 2-4 complete user journeys through the application. -->

### Flow 1: [Flow name, e.g., "Create and schedule a session"]

1. User navigates to [starting point]
2. Clicks [action]
3. Fills in [form fields]
4. Submits → system [validates, creates, notifies...]
5. User is redirected to [destination]
6. [Additional steps if any]

**Verification guide**:
1. Navigate to `/[path]`
2. Click "[Button text]"
3. Fill required fields: [list them]
4. Click "Save" → verify redirect to [path]
5. Verify [entity] appears in the list
6. Verify [related data] is correct

### Flow 2: [Flow name]

1. [Steps...]

**Verification guide**:
1. [Steps...]

## 5. Export and download UX

<!-- Define patterns for data exports and file downloads. -->

| Export | Format | Trigger | Content |
|--------|--------|---------|---------|
| [Entity] list export | [Excel / CSV / PDF] | Button on list page | [What data is included] |
| [Report name] | [Excel / PDF] | Button on [page] | [What data is included] |

**UX pattern**:
- Export button shows loading indicator while generating
- File downloads automatically when ready
- Error toast shown if export fails
- [Large exports: background job with notification when ready]
