# ADR 0001: Semantic date library profiles

## Status

Accepted for Typewriter 4.8.0.

## Context

Typewriter 4.7.0 can replace the generated date-time, date-only, and time-only TypeScript type names and initializers. Those string settings cannot distinguish an instant from a local or zoned date-time, manage a coherent frontend-library configuration, or express the difference between NodaTime `Duration` and `Period`.

The runtime schema and date backend operate on semantic kinds. A frontend library is selected once for a generated client and once per runtime transformation, so selecting different libraries on individual C# members would produce incoherent models.

## Decision

Typewriter adds an opt-in global `DateLibrary` profile with these values:

- `Legacy`
- `NativeDate`
- `Temporal`
- `Moment`
- `Luxon`
- `DateFns`
- `DayJs`
- `JsJoda`

Profiles provide matching TypeScript types, initializers, and import text for:

- instant
- plain date
- plain time
- plain date-time
- zoned date-time
- elapsed duration
- calendar period
- plain year-month
- plain month-day

CLR and NodaTime types are first resolved to one of these semantic kinds. `System.DateTime` defaults to plain date-time because its CLR type does not prove that it represents an instant. `System.DateTimeOffset` and `NodaTime.Instant` resolve to instant. NodaTime `Duration` and `Period` remain distinct.

`FrontendRuntimeTypeAttribute` is a member-level semantic override. Its neutral values such as `Instant` and `PlainDateTime` do not select a JavaScript library. The existing `Temporal*` enum names remain aliases for source compatibility.

The selected library remains global. Typewriter will not add a member-level `FrontendDateLibraryAttribute`. Templates can select a profile with `Settings.UseDateLibrary(...)`, and configuration can select one with `output.dateLibrary`.

Existing `UseDateType`, `UseDateInitializer`, date-only, and time-only methods remain available. In legacy mode they retain their 4.7.0 behavior. After a template calls `UseDateLibrary`, calling one of those methods can override the corresponding low-level profile values.

## Compatibility

`DateLibrary.Legacy` is the default. Existing configuration files, template APIs, constructors, deconstruction, and generated output remain unchanged unless a profile or new annotation is explicitly used. The change is backward compatible and suitable for a 4.8.0 minor release.

## Consequences

- Templates can configure one coherent frontend date library instead of coordinating unrelated strings.
- Runtime schemas preserve temporal meaning independently of the selected library.
- Libraries that cannot represent a semantic kind use `string` for that kind.
- Profile imports are exposed through `Settings.DateLibraryImportsGeneration`; templates decide where and when to emit them.
- JSON conversion must use the matching `date-interceptors` backend.
