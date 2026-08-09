# The foothold query adapter stays a hand-written system

The package could invert control at the foothold seam — own the probe loop itself and call consumer code per candidate point via Burst function pointers or a generic job — which would be the deeper module by every measure we use. We are not doing that. The query adapter stays a plain `ISystem` the consumer writes, reads top to bottom, and replaces whole.

The seam's documented promise is that where foothold observations come from is entirely the consumer's business, and the course teaches it by having a student write that system by hand. An interface that consumers implement and register would be smaller on paper and worse at both jobs. Anything the package absorbs on its side of the seam must therefore be optional to call: a fact an adapter may read, never a callback it must provide.

## Consequences

Predicted home, probe geometry and buffer lifecycle can only be offered, not enforced. We accept that a consumer can still write an adapter that ignores the **probe frame** and derives its own aim — so the package makes the raw path *safe* (gait detects **stale evidence** itself) rather than making a safe path mandatory.
