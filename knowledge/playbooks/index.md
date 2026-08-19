# Playbooks

Step-by-step runbooks for recurring tasks. Each has a trigger, the steps, and the gotchas
that actually bite.

# Setup and build

* [Bootstrap the environment](bootstrap-environment.md) - Get a machine or agent session able to build and test. Includes why `dotnet-install.sh` will not work.
* [Regenerate the SBE codecs](regenerate-codecs.md) - After a schema change, or when codegen freshness fails. Includes the two pins that move together.

# Testing

* [Add a conformance case](add-conformance-case.md) - Add a golden binary fixture to the replay corpus, without turning it into a rubber stamp.
* [Run the gate](run-the-gate.md) - The one command before pushing, and how to read each failure.

# Documentation

* [Validate this knowledge bundle](validate-the-bundle.md) - Run the OKF conformance check, and what its house rules add beyond the spec.
