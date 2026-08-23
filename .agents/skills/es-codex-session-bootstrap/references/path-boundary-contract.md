# External session path boundary contract

This contract applies to every Codex session helper. Project artifacts use project-relative paths; Codex state, handoff, receipt, and temporary data use an explicitly approved external state root under the user profile (`LOCALAPPDATA/ESFramework/CodexSessions` or the approved `.codex` state roots). Absolute paths, parent traversal, unapproved user-profile expansion, and undeclared destinations are rejected before read, write, move, close, or external session operations.

The contract is static evidence of path ownership and rejection policy. It does not authorize a Codex process, terminal, or external session and does not prove runtime cleanup.
