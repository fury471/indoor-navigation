# iOS unsigned builds on Apple Silicon or Intel

Updated 4 November 2026. This changes the CI build host; the exported application still targets an arm64 iPhone and is signed/installed with Sideloadly.

## Select the runner

The `iOS unsigned build` workflow has two inputs:

- `export_tag`: the existing release containing `xcode-export.zip`.
- `build_runner`: `macos-15` by default (Apple Silicon), or `macos-15-intel` as a manual fallback.

GitHub currently documents `macos-15` as an arm64 standard runner. The image repository's filename `macos-15-arm64-Readme.md` is not the workflow label used here. Both options retain `/Applications/Xcode_16.4.app/Contents/Developer`; changing host architecture does not implicitly upgrade Xcode, Unity or the iOS minimum version.

The workflow checks that the selected Xcode exists, records `uname -m` and Xcode/SDK versions, and checks the exported IL2CPP/Bee host executables with `file` and `lipo` before compilation. These host executables are distinct from the arm64 iPhone output verified after compilation. The official upload-artifact action supports arm64 runners.

## Compatibility of this project's export

Unity 6000.3.24f1's inspected `position-016` export contains both:

- `Il2CppOutputProject/IL2CPP/build/deploy_arm64`, including native arm64 IL2CPP, Bee and .NET host libraries;
- `Il2CppOutputProject/IL2CPP/build/deploy_x86_64`, for Intel hosts.

The export's generated build script selects its host deployment directory using the host architecture. The workflow restores executable permissions after unpacking the Windows-created ZIP, including the arm64 symbol-tool name when present. It does not install Unity or assume an Intel-only executable will run natively on Apple Silicon.

An existing release ZIP can be reused if it contains this matching host-tool layout. A runner-only workflow change does not require a new Unity export, release tag or application build number. Exports from other Unity versions may need different handling; a host-tool preflight failure should be investigated rather than patched by changing the iPhone target architecture.

## Switch a waiting Intel job

1. Commit and push the workflow/documentation update to the branch from which the workflow will be dispatched.
2. Cancel the old queued Intel workflow run. The workflow serialises iOS builds with `cancel-in-progress: false`, so an old run can hold the concurrency slot. Nothing in this local change cancels a remote job.
3. Start a fresh **Run workflow** on the updated branch, reuse the intended `export_tag`, and select `build_runner: macos-15`.
4. Confirm that the new job requests `macos-15` and the environment log shows `arm64` and Xcode 16.4.
5. After a successful run, download the unsigned IPA artifact and install using the existing Sideloadly process. Keep the same signing account and bundle identifier when updating the installed app.

Using **Re-run jobs** on the old run preserves its original workflow revision/inputs; it does not pick up the new runner choice. The currently waiting Intel run cannot be migrated in place by editing the file locally.

## Limits and validation

Changing runner pools may help with Intel provisioning delays, but there is no measured queue-time improvement or guaranteed immediate start. The standard arm64 runner has different CPU/RAM resources from Intel (currently 3 M1 cores / 7 GB versus 4 cores / 14 GB); it is not automatically faster for every build. If an actual build encounters memory/resource or tool compatibility errors, retain its diagnostics and try the selectable Intel fallback. No automatic queue-based fallback is implemented.

Local validation covers YAML parsing, embedded Bash syntax and the actual export's host executable architecture/selection. An actual Apple Silicon Xcode build is still required to confirm end-to-end success; Windows cannot execute that CI build. No workflow has been pushed, dispatched or cancelled by this change.

## Sources

- [GitHub-hosted runner specifications](https://docs.github.com/en/actions/reference/runners/github-hosted-runners): runner labels, architectures, resource sizes and arm64 limitations.
- [macOS 15 arm64 installed software](https://github.com/actions/runner-images/blob/main/images/macos/macos-15-arm64-Readme.md): Xcode 16.4 availability, verified on 4 November 2026. Hosted images can change, hence the workflow's preflight check.
