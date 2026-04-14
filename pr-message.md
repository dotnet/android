[marshal methods] Move marshal method pipeline from outer build to inner (per-RID) build

## Summary

Moves marshal method classification, assembly rewriting, and `.ll` LLVM IR generation from the outer build into the inner (per-RID) build's `RewriteMarshalMethods` task, running after ILLink trimming on already-trimmed assemblies. This eliminates the token staleness problem where classification captured token state, then rewriting mutated tokens, then downstream tasks saw stale data.

When marshal methods are enabled, the task classifies, rewrites assemblies, and generates a full `.ll`. When disabled, it generates an empty/minimal `.ll` with just the structural scaffolding the native runtime links against. `GenerateNativeMarshalMethodSources` is stripped down to P/Invoke preservation only.
