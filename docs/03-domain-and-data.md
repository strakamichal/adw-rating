# Domain and Data Model

<!-- AI AGENT: To fill this document:
1. Start with the glossary — define every domain term BEFORE writing entities
2. Derive entities from the scope in 02-to-be.md
3. Each entity needs: fields with types, validation rules, and acceptance criteria
4. Define enums early — they're referenced by many entities
5. For rewrites: add mapping from AS-IS entities to the new model
6. Relationships should describe cardinality and ownership (who "owns" whom)
-->

## 1. Glossary

<!-- Define all domain terms used in this project. This prevents naming confusion. -->

**[Category 1, e.g., "Organization"]**
- **[Term]**: [Definition, e.g., "Organization — top-level entity that owns all data"]
- **[Term]**: [Definition]

**[Category 2, e.g., "Core business"]**
- **[Term]**: [Definition]
- **[Term]**: [Definition]

**[Category 3, e.g., "Users and access"]**
- **[Term]**: [Definition]
- **[Term]**: [Definition]

## 2. Core entities

<!-- Define each entity with fields, rules, and acceptance criteria. -->

### [EntityName]

**Description**: [What this entity represents — 1 sentence]

**Fields**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Id` | int/guid | yes | Primary key |
| `Name` | string(100) | yes | [Description] |
| `[Field]` | [type] | [yes/no] | [Description] |
| `[Field]` | [type] | [yes/no] | [Description] |

**Rules**:
- [Validation rule, e.g., "Name must be unique within the parent entity"]
- [Business rule, e.g., "Status can only transition forward: Draft → Active → Archived"]

**Acceptance criteria**:
- [ ] Entity can be created with all required fields
- [ ] Validation rejects missing required fields
- [ ] [Specific rule, e.g., "Duplicate name within same parent is rejected"]
- [ ] [Specific rule]

---

<!-- Copy the entity template above for each entity in scope. -->

### [EntityName 2]

**Description**: [What this entity represents]

**Fields**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Id` | int/guid | yes | Primary key |

**Rules**:
- [Rule]

**Acceptance criteria**:
- [ ] [Criterion]

---

## 3. Enums

<!-- Define all enumeration types with their allowed values. -->

| Enum | Values | Used by |
|------|--------|---------|
| `[EnumName]` | `Value1`, `Value2`, `Value3` | [Which entity/field] |
| `[EnumName]` | `Value1`, `Value2` | [Which entity/field] |

## 4. Relationships

<!-- Describe how entities relate to each other. -->

| Relationship | Type | Description |
|-------------|------|-------------|
| [Entity A] → [Entity B] | 1:N | [e.g., "One Organization has many Users"] |
| [Entity C] ↔ [Entity D] | M:N | [e.g., "Positions and Requirements linked via PositionRequirement"] |
| [Entity E] → [Entity F] | 1:1 | [e.g., "Each User has one Profile"] |

**Ownership and cascading**:
- [e.g., "Deleting an Organization cascades to all its Users and data"]
- [e.g., "Deleting a Position removes its requirement assignments but not the requirements themselves"]

## 5. Mapping from AS-IS

<!-- Only for REWRITE scenario. Delete this section for greenfield. -->

| AS-IS entity/table | TO-BE entity | Notes |
|---------------------|-------------|-------|
| `[OldTable]` | `[NewEntity]` | [What changed, e.g., "Split into two entities"] |
| `[OldTable]` | `[NewEntity]` | [e.g., "Renamed, fields restructured"] |
| `[OldTable]` | — (removed) | [Why] |
| — (new) | `[NewEntity]` | [Why added] |
