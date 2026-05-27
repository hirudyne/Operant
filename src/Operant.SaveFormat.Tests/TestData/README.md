# TestData

Place real save files here to run the integration tests in
`SaveFileTests.cs`. Expected filenames are referenced as constants in the
test class; the saves themselves are not committed to the repo (they
contain personal game-state data).

If `TestData/*.sav` is absent, the integration tests will fail with a
file-not-found error. The unit tests in `SpookyHashV2Tests.cs` and
`Hash128Tests.cs` do not need these files and will pass without them.
