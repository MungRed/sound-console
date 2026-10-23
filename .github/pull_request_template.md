## Summary

<!-- What changed, and why? -->

## Related issue

<!-- Closes #... / Relates to #... -->

## Type of change

- [ ] Bug fix
- [ ] New feature
- [ ] Audio/DSP change
- [ ] UI / accessibility
- [ ] Documentation / localization
- [ ] Build / CI / packaging
- [ ] Refactor without behaviour change

## How was this tested?

<!-- Commands, CI result link, manual route setup, hardware, etc. -->

```powershell
dotnet build SoundConsole.sln -c Release
dotnet test SoundConsole.sln -c Release
```

## Manual audio verification (if applicable)

- Interface / driver:
- Input channels:
- Output device(s):
- Mode and buffer:
- Duration and result (dropouts, clipping, etc.):

## Checklist

- [ ] My changes follow the project's [contributing guide](../CONTRIBUTING.md).
- [ ] I added or updated tests for core logic changes.
- [ ] I updated documentation and `CHANGELOG.md` where needed.
- [ ] I did not commit build output, logs, settings, secrets, or audio files.
- [ ] CI passes, or I have explained why a check cannot run.
