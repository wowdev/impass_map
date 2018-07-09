# impass_map
Generator for minimaps with impassible chunks marked, C#

## building
- Be sure to clone the repository recursively, with `git clone --recursive`
  or a `git submodule update --init --recursive` after cloning.
- install dotnet 2.1
- `dotnet build --configuration Release`

On some platforms, you may need to add platform specific dependencies in
`impass_map.csproj` for `System.Drawing` to work. On macOS, this is for
example `<PackageReference Include="runtime.osx.10.10-x64.CoreCompat.System.Drawing" Version="5.4.0-r8"/>`.

## usage
- `dotnet run --configuration Release -- [arguments]`
  - Use `--help` for a list of all arguments.
  - There is two modes, offline and online storage. Use `--storagepath` to
    specify where to find data on disk. Alternatively, use `--useonline` to
    use online storage. `--onlineregion` and `--onlineproduct` to change
    product or server.
  - Specify `--outputpath` for where to store the output PNGs.
  - Use `--maps a b c` to specify maps to process.
