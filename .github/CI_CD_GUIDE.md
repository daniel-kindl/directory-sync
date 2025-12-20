# GitHub Actions CI/CD Setup Guide

This document describes the GitHub Actions workflows configured for DirectorySync.

## Overview

The CI/CD pipeline consists of multiple workflows:

1. **CI (Continuous Integration)** - Runs on every push and PR
2. **Release** - Builds and publishes releases
3. **Nightly** - Extended tests and dependency checks
4. **Documentation** - Generates and publishes API docs

## Workflows

### 1. CI Workflow (`.github/workflows/ci.yml`)

**Triggers:** Push to `main`/`develop`, Pull Requests

**Jobs:**
- **build-and-test**: Multi-platform build and test
  - Runs on Ubuntu, Windows, and macOS
  - Executes all unit tests with code coverage
  - Uploads coverage to Codecov
  - Uploads test results as artifacts

- **code-quality**: Static code analysis
  - Verifies code formatting with `dotnet format`
  - Runs Roslyn analyzers with warnings as errors

- **security-scan**: Security analysis
  - Runs CodeQL for vulnerability detection
  - Scans for vulnerable NuGet packages
  - Requires `security-events: write` permission

### 2. Release Workflow (`.github/workflows/release.yml`)

**Triggers:** Git tags matching `v*.*.*` (e.g., `v1.0.0`), Manual dispatch

**Jobs:**
- **build-and-publish**: Creates release artifacts
  - Builds self-contained single-file executables for:
    - Windows x64
    - Linux x64
    - macOS x64
    - macOS ARM64
  - Creates compressed archives (.zip for Windows, .tar.gz for Unix)
  - Generates SHA-256 checksums
  - Creates GitHub Release with all artifacts
  - Uploads release notes

- **publish-nuget**: Publishes Engine to NuGet.org
  - Only runs for tagged releases
  - Requires `NUGET_API_KEY` secret

**How to create a release:**
```bash
# Tag and push
git tag -a v1.0.0 -m "Release version 1.0.0"
git push origin v1.0.0

# Or use manual dispatch in GitHub UI
```

### 3. Nightly Workflow (`.github/workflows/nightly.yml`)

**Triggers:** Daily at 2 AM UTC, Manual dispatch

**Jobs:**
- **extended-tests**: Long-running tests
  - Runs comprehensive test suite
  - Multi-platform execution
  - Extended test timeout

- **dependency-review**: Package maintenance
  - Lists outdated packages
  - Lists deprecated packages
  - Generates reports

- **performance-benchmark**: Performance testing
  - Creates test dataset (1000 small files, 10 large files)
  - Measures sync performance
  - Tracks performance over time

### 4. Documentation Workflow (`.github/workflows/docs.yml`)

**Triggers:** Push to `main` affecting code or docs

**Jobs:**
- **generate-api-docs**: API documentation
  - Uses DocFX to generate API docs from XML comments
  - Deploys to GitHub Pages
  - Requires GitHub Pages enabled in repo settings

- **validate-docs**: Documentation quality
  - Checks for missing XML documentation
  - Validates README links

## Secrets Required

Configure these secrets in your GitHub repository settings:

| Secret | Required For | Description |
|--------|-------------|-------------|
| `CODECOV_TOKEN` | CI (optional) | Codecov.io upload token |
| `NUGET_API_KEY` | Release | NuGet.org API key for publishing |
| `GITHUB_TOKEN` | All | Automatically provided by GitHub |

## Permissions

Workflows require the following permissions:

```yaml
permissions:
  contents: write          # For creating releases
  security-events: write   # For CodeQL security scanning
  pages: write            # For GitHub Pages deployment
```

## Dependabot

Automatic dependency updates are configured in `.github/dependabot.yml`:

- **NuGet packages**: Weekly updates on Monday
- **GitHub Actions**: Monthly updates
- Grouped updates for related packages (Serilog, testing frameworks)

## Branch Protection Rules

Recommended branch protection for `main`:

- ✅ Require pull request reviews (1 reviewer)
- ✅ Require status checks to pass:
  - `build-and-test (ubuntu-latest)`
  - `build-and-test (windows-latest)`
  - `build-and-test (macos-latest)`
  - `code-quality`
  - `security-scan / CodeQL`
- ✅ Require branches to be up to date
- ✅ Require linear history
- ✅ Do not allow bypassing the above settings

## Status Badges

Add these badges to your README:

```markdown
[![CI](https://github.com/daniel-kindl/directory-sync/actions/workflows/ci.yml/badge.svg)](https://github.com/daniel-kindl/directory-sync/actions/workflows/ci.yml)
[![Release](https://github.com/daniel-kindl/directory-sync/actions/workflows/release.yml/badge.svg)](https://github.com/daniel-kindl/directory-sync/actions/workflows/release.yml)
[![codecov](https://codecov.io/gh/daniel-kindl/directory-sync/branch/main/graph/badge.svg)](https://codecov.io/gh/daniel-kindl/directory-sync)
[![License](https://img.shields.io/github/license/daniel-kindl/directory-sync)](LICENSE)
```

## Local Testing

Test workflows locally using [act](https://github.com/nektos/act):

```bash
# Install act
# Windows: choco install act-cli
# macOS: brew install act
# Linux: curl -s https://raw.githubusercontent.com/nektos/act/master/install.sh | sudo bash

# Test CI workflow
act -j build-and-test

# Test with specific event
act push --eventpath .github/test-event.json
```

## Monitoring

- **Failed builds**: Check Actions tab in GitHub
- **Code coverage**: View trends in Codecov dashboard
- **Performance**: Review nightly benchmark artifacts
- **Dependencies**: Check Dependabot PRs and security alerts

## Troubleshooting

### Build fails on macOS
- Check .NET SDK availability for macOS ARM64
- Verify runtime identifiers in publish commands

### CodeQL warnings
- Review security findings in Security tab
- Update code or mark false positives

### Release upload fails
- Verify `GITHUB_TOKEN` has `contents: write` permission
- Check artifact file paths and names

### NuGet publish fails
- Verify `NUGET_API_KEY` secret is set correctly
- Check package version doesn't already exist
- Ensure package metadata is complete

## Future Enhancements

Consider adding:
- **Docker**: Containerized builds and deployment
- **Integration tests**: Database, external API tests
- **Load testing**: Stress tests with large datasets
- **Preview releases**: Deploy to staging environment
- **Change log**: Automated generation from commits
- **Semantic versioning**: Automatic version bumps
