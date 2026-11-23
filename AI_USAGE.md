# AI_USAGE.md

## A. AI Usage Overview

AI tools were used as supporting assistants during the development of Access Request Hub (ARH).

**Primary AI tool:** Antigravity.

AI assistance was mainly used for:

- **Formatting:** Improving code readability and consistency.
- **File templates:** Generating initial file structures and component templates.
- **Commenting:** Suggesting code comments and improving explanations.
- **Documentation:** Assisting with README and technical documentation.

Technical decisions, implementation integration, testing, debugging, and final review remained my responsibility.

## B. Significant AI Interactions

### Interaction 1 — Architecture and Project Structure

**Ask:** How should a simple .NET Blazor and MySQL application be structured?

**Suggestion:** Use a modular monolith with separate UI, Data, Domain, DTOs, Endpoints, and Services.

**Decision:** Accepted with simplifications.

**Why:** The structure provides clear separation of responsibilities without unnecessary architectural complexity for an MVP.

### Interaction 2 — Edge Cases and Implementation Review

**Ask:** Review the existing approval workflow implementation and identify potential edge cases or missing safeguards.

**Suggestion:** Check scenarios involving duplicate submissions, unauthorized approvers, self-approval, missing rejection reasons, terminal states, stale versions, concurrent actions, and audit transaction failures.

**Decision:** Accepted and used to expand test coverage.

**Why:** The core business rules were already defined. AI assistance helped identify additional failure scenarios and verify that the implementation handled them correctly.

### Interaction 3 — MySQL Integration Testing

**Ask:** How should integration tests use a real relational database?

**Suggestion:** Use Testcontainers and Docker to provide an isolated MySQL environment.

**Decision:** Changed.

**Why:** I chose the existing Laragon MySQL installation with a separate `arh_test` database. This reduced setup complexity while retaining real MySQL integration testing.

## C. Three Things AI Got Wrong

### 1. Unnecessary Docker Recommendation

AI initially recommended Docker and Testcontainers. This was unnecessary for the local MVP, so I selected Laragon instead.

### 2. Incomplete Code Integration

Some generated code did not match the existing project structure, method signatures, and Blazor Interactive Server implementation. Manual adjustments were required.

### 3. Incomplete Verification

Some suggestions were presented as complete before successful build, test, or security verification. Additional checks and corrections were necessary.

## D. Engineering Ownership

I independently managed the project and made the final engineering decisions.

AI was used as a supporting development tool, not as a replacement for my engineering work or responsibility.

I remain responsible for the submitted code, its behavior, testing, and known limitations.