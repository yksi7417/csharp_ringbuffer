# Directory Update Log

## 2026-08-19
* **Creation**: Added [the implementation loop](/practices/implementation-loop.md), covering
  how to work `IMPL/PLAN.md` and why the plan is deliberately kept outside this bundle.
* **Update**: Project state in [the root index](/index.md) now points at the plan.

## 2026-08-19
* **Initialization**: Established the OKF v0.2 knowledge bundle, decomposed from the research
  report at `docs/RESEARCH.md` (git history). Created the decisions, findings, concepts,
  architecture, testing, practices, risks, playbooks and references sections.
* **Decision**: All ten design decisions accepted following review — see
  [decisions](decisions/index.md). Notably [D1](decisions/d1-claim-commit-with-padding.md)
  (claim-a-bound / commit-the-actual / pad-the-remainder) and
  [D2](decisions/d2-spsc-and-mpsc.md) (build both SPSC and MPSC).
* **Creation**: Added the conformance validator `scripts/ci/checks/okf_validate.py` so the
  bundle's structure is enforced by CI rather than by memory.
