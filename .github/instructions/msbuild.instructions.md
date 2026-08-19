---
applyTo: "**/*.targets,**/*.props,**/*.proj,**/*.csproj"
---

# MSBuild conventions

Rules for authoring MSBuild targets, wherever they live — `.targets`, `.props`, `.proj`, and the
18 `.csproj` files in this repo that declare their own `<Target>`. They matter most for the product
targets under `src/Xamarin.Android.Build.Tasks/Microsoft.Android.Sdk/targets/` and
`src/Xamarin.Android.Build.Tasks/Xamarin.Android.Common.targets`, which ship to customers.

## Every target that produces a file must be incremental

Any target invoking a task that writes a file needs `Inputs` and `Outputs`. A target without them
re-runs on every build, which shows up directly in customer inner-loop build times.

```xml
<Target Name="_AndroidGenerateSomething"
    AfterTargets="_SomeUpstreamTarget"
    DependsOnTargets="_AndroidGenerateSomethingInputs"
    Inputs="@(_AndroidSomethingInput)"
    Outputs="$(_AndroidSomethingOutput)">
  <GenerateSomething Input="@(_AndroidSomethingInput)" OutputFile="$(_AndroidSomethingOutput)" />
</Target>
```

### `Outputs` should be the real output file

Write the output file directly from the task and use that file as `Outputs`. Do **not** reach for a
temp file + `Files.CopyIfChanged` + a stamp file unless there is a concrete reason the timestamp
must be preserved (e.g. the file feeds another incremental target that would otherwise cascade).

`CopyIfChanged` deliberately leaves the timestamp alone when content is unchanged, which means the
real output can never serve as `Outputs` — you are then forced to invent a
`$(...)Stamp` file, `<MakeDir/>` it, `<Touch/>` it, and add it to `@(FileWrites)`. That is a lot of
machinery to buy back something you did not need. Prefer the simple version.

## Compute `Inputs` in a `<TargetName>Inputs` target

Do not compute the input `ItemGroup` inside the target that consumes it — `Inputs` is evaluated
before the target body runs, so the item group would be empty on the first evaluation. Put it in a
separate target pulled in via `DependsOnTargets`, named `<TargetName>Inputs`:

```xml
<Target Name="_AndroidGenerateSomethingInputs">
  <ItemGroup>
    <_AndroidSomethingInput Include="@(_SomeBigList)" Condition=" '%(Filename)' == '$(TargetName)' " />
  </ItemGroup>
</Target>
```

`DependsOnTargets` targets run **before** the parent target's `Condition`, `Inputs`, and `Outputs`
are evaluated, so this works.

### Output paths belong in the same target

`$(TargetName)`, `$(TargetFileName)`, `$(IntermediateOutputPath)`, `$(OutDir)` and friends are
**not final** when our `.targets` files are evaluated — `$(TargetName)` is often blank, and
`$(IntermediateOutputPath)` has not yet had the RID/TFM subdirectory appended. A top-level
`<PropertyGroup>` computing an output path from them silently produces garbage such as
`obj\Release\.mibc` instead of `obj\Release\net11.0\android-arm64\MyApp.mibc`.

Compute output paths in a `<PropertyGroup>` inside the `<TargetName>Inputs` target alongside the
input items. `DependsOnTargets` guarantees it runs before the parent's `Outputs` is evaluated.

```xml
<Target Name="_AndroidGenerateSomethingInputs">
  <PropertyGroup>
    <_AndroidSomethingOutput>$(IntermediateOutputPath)$(TargetName).ext</_AndroidSomethingOutput>
  </PropertyGroup>
  <ItemGroup>
    ...
  </ItemGroup>
</Target>
```

### `Inputs` must be the actual inputs, not a superset

Never list a whole upstream item group as `Inputs` when the task only consumes one item from it.
Doing so makes the target re-run whenever *any* unrelated file in that list changes. Filter first,
then use the filtered item group as `Inputs`.

Filter in MSBuild — using `%(Filename)` batching and `@(X->Distinct())` — rather than
passing everything to the task and filtering in C#. Prefer the simplest well-known property for the
comparison (`%(Filename)` vs `$(TargetName)`, not `%(Filename)%(Extension)` vs `$(TargetFileName)`).
Keep the task's surface area minimal; a task that takes `[Required] string MainAssembly` is easier
to reason about and unit test than one that takes an `ITaskItem[]` plus a filter property.

## Item groups in a skipped target *are* still evaluated

If a target is skipped as **up to date** (its `Inputs` are older than its `Outputs`), MSBuild still
evaluates the `<ItemGroup>` and `<PropertyGroup>` elements inside it. Downstream targets see those
items. So do **not** split item population into a second `AfterTargets` target "so it still runs on
incremental builds" — that is unnecessary indirection.

```xml
<Target Name="_AndroidGenerateSomething" Inputs="..." Outputs="...">
  <GenerateSomething ... />
  <!-- Still evaluated when the target is skipped as up to date. -->
  <ItemGroup Condition=" Exists('$(_AndroidSomethingOutput)') ">
    <_SomeConsumerList Include="$(_AndroidSomethingOutput)" />
    <FileWrites Include="$(_AndroidSomethingOutput)" />
  </ItemGroup>
</Target>
```

This is **not** true for a target skipped by `Condition="false"` — that skips the entire target
body, item groups included.

## Read late-set properties from a target `Condition`, never at evaluation time

Properties set by NuGet `buildTransitive` `.targets` (from packages like `Microsoft.Maui.Controls`)
are assigned **after** our `.targets` are evaluated. A top-level `<PropertyGroup>` that reads such a
property will see it blank.

```xml
<!-- WRONG: $(PublishReadyToRunCrossgen2ExtraArgs) may not be set yet. -->
<PropertyGroup>
  <_AndroidDoThing Condition=" $(PublishReadyToRunCrossgen2ExtraArgs.Contains('...')) ">true</_AndroidDoThing>
</PropertyGroup>

<!-- RIGHT: evaluated when the target runs, after everything is assigned. -->
<Target Name="_AndroidDoThing" Condition=" $(PublishReadyToRunCrossgen2ExtraArgs.Contains('...')) ">
```

A helper property that merely aliases a condition is usually not worth it — inline the check in the
target's `Condition`.

## A target's `Condition` is evaluated *before* its `DependsOnTargets`

So a `Condition` can never read a property that one of its dependencies sets — the dependency is
never built. This is tempting when a decision has several inputs and you want to resolve it into a
single property in the `Inputs` helper target; it silently never runs.

Giving the helper the same `AfterTargets`/`BeforeTargets` hooks does work (targets sharing a hook
run in declaration order), but it makes correctness depend on declaration order. Prefer keeping the
whole decision inline in the `Condition`, even when it needs nested parentheses.

## Test item emptiness with `->Count()`

`'@(SomeItem)' == ''` batches the whole list into a string just to see whether it is empty. Use
`'@(SomeItem->Count())' == '0'` instead.

## Don't invent public properties

Prefer keying off properties that already exist. Do not add a public `$(AndroidFooBar)` opt-in/
opt-out knob unless a customer genuinely needs it — every public property is documentation,
localization, and a compatibility commitment.

When an internal escape hatch is needed (typically so a test can force a codepath), add a
**private** `$(_Android*)` property that is **blank by default** — no `<PropertyGroup>` default for
it anywhere — and `or` it into the existing condition:

```xml
Condition=" '$(PublishReadyToRun)' == 'true' and '$(_AndroidReadyToRunMainAssembly)' != 'false' and ('$(_AndroidReadyToRunMainAssembly)' == 'true' or ($(PublishReadyToRunCrossgen2ExtraArgs.Contains('--partial')) and '@(PublishReadyToRunPgoFiles->Count())' == '0')) "
```

Give the hatch three states rather than two: `true` forces the codepath on, `false` forces it off,
and blank means "decide automatically". A force-on-only hatch leaves no way to build the baseline
it is meant to be compared against.

Public properties go in `Documentation/docs-mobile/building-apps/build-properties.md`; private
`_`-prefixed ones must not.

## Naming

* Private targets, properties, and items are `_Android`-prefixed: `_AndroidGenerateMibcProfile`,
  `$(_AndroidMibcProfile)`, `@(_AndroidMibcMainAssembly)`.
* An `Inputs`-computing helper target is `<TargetName>Inputs`, not `_AndroidPrepareXxx` or similar.
* Names should say what the thing *is*, not how it is used.

## `--` is illegal inside an XML comment

Writing `--partial`, `--map`, or any `--` sequence inside an `<!-- ... -->` block produces an XML
parse error. Escape it, reword it, or move it out of the comment. Always validate after editing:

```powershell
try { [xml]$x = Get-Content path\to\File.targets; "ok" } catch { "INVALID: $($_.Exception.Message)" }
```

## Other verified semantics

* If `Inputs` evaluates to empty and `Outputs` is non-empty, MSBuild skips the target for having no
  inputs. Guard accordingly if an empty input list is legitimate.
* `$(UndefinedProperty.Contains('x'))` safely evaluates to `false`; no null check is needed.
* `@(X->Distinct())` is a valid item function and is the right way to dedupe overlapping item lists.
* Add generated files to `@(FileWrites)` so `Clean` removes them.
* Use `TaskFactory="TaskHostFactory"` and `Runtime="NET"` on `<UsingTask/>` for **internal**
  build-time tasks (`xa-prep-tasks`, `BootstrapTasks`) only — never on tasks shipped to customers.
