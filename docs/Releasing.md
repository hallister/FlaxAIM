# Versioning and releases

`VERSION` is the authoritative plugin release number. Run `./Scripts/Release.ps1 -Mode Sync` to copy it into
`FlaxAIM.flaxproj`, the examples project and `Source/FlaxAIM/MyPlugin.cs`. Commit those copies with the release.

## Development versions

Use `v0.1`, `v0.2`, and subsequent minor versions for breaking development releases. Compatible additions and
fixes can use patch versions such as `v0.2.1`. `VERSION` contains the number without the `v` prefix. The release
script accepts two or three numeric components and uses that exact number for metadata and the tag.

Each breaking release documents the manual changes consumers need. v0.2 requires a one-time rebuild of IMC
bindings and updates to C# code that constructs contexts. There is no asset schema or automatic conversion
layer; see the [changelog and upgrade instructions](../CHANGELOG.md).

The annotated `v0.1` tag points to `3adddeb`, the selected pre-change `origin/main` commit. Its source retains
the old template `1.0` metadata. v0.2 introduces the new input mapping format. Release tags are created after
the changes are committed and ready to release. Creating a local tag does not publish it.

## Release workflow

1. Edit `VERSION`, run `./Scripts/Release.ps1 -Mode Sync`, and add a matching changelog entry such as
   `## [0.2] - Unreleased`.
2. Run `./Scripts/Release.ps1 -Mode Check`, `dotnet test Tests/FlaxAIM.Tests`, and the
   [engine tests](EngineTests.md).
3. Replace `Unreleased` in the changelog heading with the release date (`YYYY-MM-DD`), review the changes, and
   commit them. The working tree must be clean, including untracked files, before tagging.
4. Run `./Scripts/Release.ps1 -Mode Tag`. It checks metadata and the dated changelog, requires a clean tree,
   and creates an annotated tag such as `v0.2` at HEAD. It refuses to overwrite an existing tag and never pushes.
5. When ready to publish, push the intended branch and the specific tag, for example `git push origin v0.2`.
   The baseline can be published separately with `git push origin v0.1`.

Pin a published tag when installing the plugin:

```powershell
git clone --branch v0.2 https://github.com/hallister/FlaxAIM.git Plugins/FlaxAIM
```

The command becomes usable once v0.2 is published. Never move or reuse a published tag. Restore the matching
IMCs when downgrading across the v0.2 format change.
