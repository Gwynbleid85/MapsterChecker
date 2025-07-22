# Publishing MapsterChecker.Analyzer to NuGet

This document provides step-by-step instructions for publishing the MapsterChecker analyzer package to NuGet.

## Prerequisites

1. **NuGet Account**: Create an account at [nuget.org](https://www.nuget.org/)
2. **API Key**: Generate an API key from your NuGet account settings
3. **.NET SDK**: Ensure you have .NET 9.0 SDK or later installed
4. **Git**: Ensure all changes are committed and pushed

## Pre-Publishing Checklist

Before publishing, ensure you have:

- [ ] Updated the version number in `MapsterChecker.Analyzer.csproj`
- [ ] Updated the `README.md` with any new features or changes
- [ ] All tests are passing (`dotnet test`)
- [ ] Code builds successfully in Release mode (`dotnet build -c Release`)
- [ ] Reviewed the package contents (`dotnet pack` and inspect the `.nupkg` file)

## Step-by-Step Publishing Process

### 1. Update Version Number

Edit `MapsterChecker.Analyzer/MapsterChecker.Analyzer.csproj` and increment the version:

```xml
<PackageVersion>1.0.1</PackageVersion>  <!-- Increment this -->
```

**Version Guidelines:**
- **Patch** (1.0.1): Bug fixes, minor improvements
- **Minor** (1.1.0): New features, backward compatible changes  
- **Major** (2.0.0): Breaking changes, major refactoring

### 2. Build and Test

```bash
# Navigate to the solution directory
cd /Users/miloshegr/Documents/Playgound/MapsterChecker

# Clean previous builds
dotnet clean

# Restore dependencies
dotnet restore

# Run all tests
dotnet test

# Build in Release mode
dotnet build --configuration Release
```

### 3. Create the NuGet Package

```bash
# Navigate to the analyzer project
cd MapsterChecker.Analyzer

# Create the package
dotnet pack --configuration Release --output ../nupkgs
```

This creates a `.nupkg` file in the `nupkgs` directory.

### 4. Inspect the Package (Optional but Recommended)

```bash
# Install NuGet Package Explorer (if not already installed)
dotnet tool install --global NuGetPackageExplorer

# Open the package for inspection
nuget-package-explorer ../nupkgs/MapsterChecker.Analyzer.1.0.0.nupkg
```

Verify that the package contains:
- `analyzers/dotnet/cs/MapsterChecker.Analyzer.dll`
- `build/MapsterChecker.Analyzer.props`
- `README.md`
- Proper metadata (authors, description, tags, etc.)

### 5. Set Up NuGet API Key

```bash
# Set your NuGet API key (replace with your actual key)
dotnet nuget setapikey [YOUR-API-KEY] --source https://api.nuget.org/v3/index.json
```

**Security Note**: Store your API key securely. Consider using environment variables:

```bash
# Set as environment variable
export NUGET_API_KEY="your-api-key-here"

# Use in command
dotnet nuget setapikey $NUGET_API_KEY --source https://api.nuget.org/v3/index.json
```

### 6. Publish to NuGet

```bash
# Navigate back to solution directory
cd ..

# Publish the package
dotnet nuget push nupkgs/MapsterChecker.Analyzer.1.0.0.nupkg --source https://api.nuget.org/v3/index.json
```

### 7. Verify Publication

1. Visit [nuget.org/packages/MapsterChecker.Analyzer](https://nuget.org/packages/MapsterChecker.Analyzer)
2. Verify the package appears and metadata is correct
3. Test installation in a separate project:

```bash
# Create test project
mkdir test-installation
cd test-installation
dotnet new console
dotnet add package MapsterChecker.Analyzer --version 1.0.0
dotnet build
```

## Advanced Publishing Options

### Publishing Pre-release Versions

For pre-release versions (alpha, beta, rc):

```xml
<PackageVersion>1.1.0-alpha.1</PackageVersion>
```

```bash
dotnet nuget push nupkgs/MapsterChecker.Analyzer.1.1.0-alpha.1.nupkg --source https://api.nuget.org/v3/index.json
```

### Symbol Package Publishing

To publish symbol packages for debugging:

```bash
# Create symbols package
dotnet pack --configuration Release --include-symbols --include-source

# Push both packages
dotnet nuget push nupkgs/MapsterChecker.Analyzer.1.0.0.nupkg --source https://api.nuget.org/v3/index.json
dotnet nuget push nupkgs/MapsterChecker.Analyzer.1.0.0.symbols.nupkg --source https://nuget.smbsrc.net/
```

### Automated Publishing with GitHub Actions

Create `.github/workflows/publish.yml`:

```yaml
name: Publish to NuGet

on:
  release:
    types: [published]

jobs:
  publish:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '9.0.x'
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Build
      run: dotnet build --configuration Release --no-restore
    
    - name: Test
      run: dotnet test --configuration Release --no-build
    
    - name: Pack
      run: dotnet pack --configuration Release --no-build --output nupkgs
    
    - name: Publish to NuGet
      run: dotnet nuget push nupkgs/*.nupkg --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json
```

## Post-Publishing Tasks

1. **Tag the Release**: Create a Git tag for the published version
   ```bash
   git tag v1.0.0
   git push origin v1.0.0
   ```

2. **Update Documentation**: Update any documentation referencing version numbers

3. **Announce**: Share the release on relevant channels (social media, forums, etc.)

## Troubleshooting Common Issues

### Package Already Exists
If you get an error that the package version already exists:
- Increment the version number
- You cannot overwrite existing versions on nuget.org

### Missing Dependencies
Ensure all required dependencies are listed in the `.csproj` file with proper version constraints.

### Analyzer Not Loading
Check that:
- The analyzer DLL is in the correct path: `analyzers/dotnet/cs/`
- The `build` props file is included
- Target framework is `netstandard2.0`

### API Key Issues
- Verify your API key has the correct permissions
- Check if the key has expired
- Ensure you're using the correct source URL

## Support

For issues with publishing:
- [NuGet Documentation](https://docs.microsoft.com/en-us/nuget/)
- [NuGet Support](https://www.nuget.org/contact)
- [MapsterChecker Issues](https://github.com/mapsterchecker/mapsterchecker/issues)