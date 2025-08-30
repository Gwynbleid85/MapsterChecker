# GitHub Actions Workflows

This directory contains the CI/CD pipelines for the MapsterChecker.Analyzer project.

## Workflows

### 1. CI Workflow (`ci.yml`)
- **Trigger**: On push to main/master/develop branches and on pull requests
- **Purpose**: Continuous integration for code quality
- **Steps**:
  1. Checkout code
  2. Setup .NET SDK (8.x and 9.x)
  3. Restore dependencies
  4. Build solution
  5. Run tests
  6. Build NuGet package (dry run)
  7. Validate package

### 2. Publish Workflow (`publish.yml`)
- **Trigger**: On pushing a semantic version tag (e.g., `v1.0.3`)
- **Purpose**: Automated release and publishing to NuGet.org
- **Steps**:
  1. **Validate**: Check tag format and extract version
  2. **Test**: Run all tests to ensure quality
  3. **Update Version**: Create release branch and update package version
  4. **Build & Pack**: Build the project and create NuGet package
  5. **Publish**: Push package to NuGet.org
  6. **Release**: Create GitHub release with release notes
  7. **Merge & Cleanup**: Merge release branch to main and cleanup

## Required Secrets

The following secrets must be configured in the GitHub repository settings:

1. **`REPOSITORY_TOKEN`** (Required)
   - Personal Access Token with repo permissions
   - Used for creating releases and managing branches
   - How to create:
     1. Go to GitHub Settings > Developer settings > Personal access tokens
     2. Generate new token (classic)
     3. Select scopes: `repo` (full control)
     4. Copy token and add as repository secret

2. **`NUGET_API_KEY`** (Required)
   - NuGet.org API key for package publishing
   - How to obtain:
     1. Sign in to [NuGet.org](https://www.nuget.org/)
     2. Go to API Keys
     3. Create new API Key with push permissions
     4. Copy key and add as repository secret

## Usage

### Creating a New Release

1. Ensure all changes are merged to main branch
2. Create and push a semantic version tag:
   ```bash
   git tag v1.0.3
   git push origin v1.0.3
   ```
3. The publish workflow will automatically:
   - Run tests
   - Update package version
   - Build and publish to NuGet
   - Create GitHub release
   - Merge changes back to main

### Version Format

Tags must follow semantic versioning: `v{major}.{minor}.{patch}`

Examples:
- ✅ `v1.0.0`
- ✅ `v2.1.3`
- ✅ `v0.1.0`
- ❌ `1.0.0` (missing 'v' prefix)
- ❌ `v1.0` (missing patch version)
- ❌ `v1.0.0-beta` (pre-release versions not supported)

## Workflow Flow Diagram

```
Tag Push (v*.*.*)
    ↓
Validate Tag Format
    ↓
Run Tests
    ↓
Create Release Branch
    ↓
Update Package Version
    ↓
Build & Pack NuGet
    ↓
Publish to NuGet.org
    ↓
Create GitHub Release
    ↓
Merge to Main Branch
    ↓
Cleanup Release Branch
```

## Troubleshooting

### Common Issues

1. **"REPOSITORY_TOKEN secret is not set"**
   - Solution: Add the REPOSITORY_TOKEN secret in repository settings

2. **"NUGET_API_KEY secret is not set"**
   - Solution: Add the NUGET_API_KEY secret in repository settings

3. **"Tag does not follow semantic versioning format"**
   - Solution: Ensure tag follows `v{major}.{minor}.{patch}` format

4. **Package already exists on NuGet**
   - The workflow uses `--skip-duplicate` flag, so this shouldn't fail the pipeline
   - If you need to republish, you'll need to increment the version

### Manual Workflow Trigger

While the workflows are designed to run automatically, you can manually trigger them:

1. Go to Actions tab in GitHub
2. Select the workflow
3. Click "Run workflow"
4. Select branch and provide inputs if required

## Maintenance

- Keep .NET SDK versions up to date in both workflows
- Review and update GitHub Actions versions periodically
- Monitor deprecation notices from GitHub Actions