# Lab scenarios and detailed validation

Detailed operational ownership moves to the private [AvaloNero/Spherewright-Lab](https://github.com/AvaloNero/Spherewright-Lab/tree/14478a03c64c550e644c559405c8496b647552e7) repository. Access requires authorization to that private repository. The product remains independently buildable and does not fetch or execute Lab during build, test or package operations.

The immutable Lab commit for this transfer is `14478a03c64c550e644c559405c8496b647552e7`. Its exact product submodule is `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9`. Because no official v0.4.0 tag exists, its source lock says `releaseTag: null` and `status: provisional`. The imported operational text comes from product fact commit `e7d5f7d8661092b7c8089710488c448f290638be`, not from the earlier candidate source.

```powershell
git clone --recurse-submodules https://github.com/AvaloNero/Spherewright-Lab.git
git -C Spherewright-Lab checkout 14478a03c64c550e644c559405c8496b647552e7
git -C Spherewright-Lab submodule update --init --recursive
cd Spherewright-Lab
pwsh -File scripts/bootstrap.ps1
pwsh -File scripts/verify-source-lock.ps1
pwsh -File scripts/verify-evidence.ps1
```

- [v0.4 real scenario](https://github.com/AvaloNero/Spherewright-Lab/tree/14478a03c64c550e644c559405c8496b647552e7/scenarios/v0.4-final)
- [final summary](https://github.com/AvaloNero/Spherewright-Lab/blob/14478a03c64c550e644c559405c8496b647552e7/releases/v0.4.0/validation-summary.md)
- [complete evidence](https://github.com/AvaloNero/Spherewright-Lab/tree/14478a03c64c550e644c559405c8496b647552e7/releases/v0.4.0/evidence)
- [import manifest](https://github.com/AvaloNero/Spherewright-Lab/blob/14478a03c64c550e644c559405c8496b647552e7/releases/v0.4.0/manifests/import-manifest.json) and [normalized manifest](https://github.com/AvaloNero/Spherewright-Lab/blob/14478a03c64c550e644c559405c8496b647552e7/releases/v0.4.0/manifests/evidence-manifest.json)
- [migration verification](https://github.com/AvaloNero/Spherewright-Lab/blob/14478a03c64c550e644c559405c8496b647552e7/releases/v0.4.0/migration-validation.md)

Safe originals are archived byte-for-byte before normalization. By explicit owner choice, one original experience ledger remains protected-local because of a historical absolute system path; only its sanitized derivative is in Git. CI verifies the declared Git scope and reports the unavailable private original honestly. Private raw game records, Saves, tickets, identities and credentials were not imported.

Old product operational paths retain short immutable redirects, including the audit target referenced by the unchanged embedded playbook. Product documentation and the packaging README point to the fixed Lab commit; the packaging README receives link changes only, without regenerating the frozen candidate ZIPs. Historical tags and original Git history remain untouched. Follow the Lab AGENTS and current handoff for a future approved game run; an old snapshot is never a fresh execution plan.
