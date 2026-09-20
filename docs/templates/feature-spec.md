# F-<id>: <Feature name>

Status: Draft | Approved (<name>, <date>)
Module(s): <Ticketing, Fare, …>
Related: FR-xxx, UC-x, ADR-xxxx

Decision owner: <named human / approving authority>

Authoritative sources: <documents, approved decisions, or "none; business decision required">

## 0. Discovery notes

Record the discovery-agent findings and unresolved contradictions here. Each note must link to its source file/section.

## 1. Goal
One or two sentences: who needs what, and why.

## 2. Actors and permissions
| Actor | Permission | Notes |
|---|---|---|

## 3. Business rules
Label every rule. Include the source for FACT and BUSINESS DECISION, or the ADR for ENGINEERING DECISION.

| # | Rule | Label | Source |
|---|---|---|---|
| R1 | | FACT / ASSUMPTION / OPEN QUESTION / BUSINESS DECISION / ENGINEERING DECISION | |

**Blocking open questions:** list any OPEN QUESTION that stops implementation. If there is one, the spec cannot be Approved.

## 4. Scenarios (Given / When / Then)
- Happy path
- Each failure → error code
- Concurrency / retry, if financial

## 5. State changes
| Entity | From | Event | Guard | To |
|---|---|---|---|---|

## 6. API
Method, path, request, response, error codes, idempotency (yes/no).

## 7. Data
New or changed tables, columns, constraints, indexes.

## 8. Audit, logging, metrics

## 9. Out of scope
