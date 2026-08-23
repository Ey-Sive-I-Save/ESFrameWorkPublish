# Lifecycle ownership and recovery static gate

Acceptance id: `module-lifecycle-static`. Profile: `session`.

Cases: `subscription-ownership`, `reload-unbind`, `state-restore`, `interruption-recovery`, `stale-receipt`.

Static assertions cover callback ownership, explicit unbind/disposal, state restoration, interruption recovery, and receipt freshness. Static results do not prove Unity domain reload or PlayMode behavior; those remain Runtime claims.
