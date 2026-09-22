# External recipe fixtures

These templates are the subset of
[NetCoreTypewriterRecipes](https://github.com/AdaskoTheBeAsT/NetCoreTypewriterRecipes)
used by the engine compatibility and snapshot tests. They were copied from commit
`5ee2b82473534cc7c8458663cff66656c8d66a6e`; the upstream MIT license is included in
`LICENSE`.

The model fixtures use `DateLibrary.Legacy` and `UseGuidType("string")` rather than
the upstream Temporal and Uint8Array selections. Dates therefore remain `Date`
and GUIDs remain `string`. Other recipe settings are preserved.

The upstream directory layout is retained. Tests read only these checked-in
fixtures, not a sibling checkout, and fail if a required template is missing.
Changes in the external repository do not affect tests until explicitly copied
here.

## Updating fixtures and snapshots

1. Copy the required templates from a reviewed upstream revision, retaining the
   legacy date and string GUID settings.
2. Update the revision above and review the fixture changes.
3. From the repository root, regenerate the external snapshots in PowerShell:

   ```powershell
   $env:TYPEWRITER_ACCEPT_EXTERNAL_RECIPE_SNAPSHOTS = '1'
   try {
       dotnet test --project tests/unit/Typewriter.SnapshotTests/Typewriter.SnapshotTests.csproj --configuration Debug --filter-class '*ExternalRecipeSnapshotTests'
   }
   finally {
       Remove-Item Env:TYPEWRITER_ACCEPT_EXTERNAL_RECIPE_SNAPSHOTS
   }
   ```

   This replaces the external snapshot directories. Preserve any uncommitted
   snapshot work before running it.

4. Review every snapshot diff, then run the tests without snapshot acceptance:

   ```powershell
   dotnet test --solution AdaskoTheBeAsT.Typewriter.slnx --configuration Debug
   ```
