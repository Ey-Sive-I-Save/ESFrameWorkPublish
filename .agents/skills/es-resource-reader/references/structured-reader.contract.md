# Structured and package reader contract

The structured reader is a bounded, read-only projection layer.

- SQLite is opened in read-only mode and emits table/view names and bounded column metadata; it never executes project SQL, triggers, extensions, or writes.
- TOML and INI emit root/section keys only; values are not treated as authoritative configuration and interpolation is disabled for INI.
- ZIP, TAR, TAR.GZ and related archives emit member paths and sizes without extraction or execution. Absolute paths and traversal members fail closed.
- A projection is hash-bound to the source bytes and parser version. Truncation is explicit in `summary.truncated`; no omitted entry may be interpreted as absent.
- Runtime import, network access, decompression to project paths, and AssetPackage mutation are non-claims and require separate authorization.
