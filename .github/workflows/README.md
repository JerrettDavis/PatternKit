# Workflows

This directory contains GitHub Actions workflows for the PatternKit repository.

## Workflows

### CI/CD Workflows

- **ci.yml** - Main CI/CD pipeline that runs on push to main and pull requests. Handles building, testing, packaging, and releasing.
- **pr-validation.yml** - Comprehensive PR validation including builds, tests, documentation, and dry-run packaging.
- **docs.yml** - Publishes documentation to GitHub Pages.

### Code Quality

- **codeql-analysis.yml** - CodeQL security scanning for vulnerability detection.
- **dependency-review.yml** - Reviews dependencies in pull requests for security issues and license compliance.

### Automation

- **labeler.yml** - Automatically labels PRs based on changed files.
- **stale.yml** - Marks and closes stale issues and PRs.
- **update-packages.yml** - Weekly check for outdated NuGet packages and creates PRs for updates.

## Workflow Triggers

### On Pull Request
- ci.yml (pr-checks job)
- pr-validation.yml
- dependency-review.yml
- labeler.yml
- codeql-analysis.yml

### On Push to Main
- ci.yml (release job)
- docs.yml
- codeql-analysis.yml

### Scheduled
- stale.yml (daily at midnight UTC)
- update-packages.yml (weekly on Sunday at midnight UTC)
- codeql-analysis.yml (weekly on Sunday at noon UTC)

### Manual Trigger (workflow_dispatch)
- stale.yml
- update-packages.yml

## Configuration Files

Related configuration files in `.github/`:
- **dependabot.yml** - Dependabot configuration for automated dependency updates
- **labeler.yml** - Configuration for the labeler workflow
- **CODEOWNERS** - Defines code owners for automatic review requests
- **PULL_REQUEST_TEMPLATE.md** - Template for pull request descriptions
- **QUICK_REFERENCE.md** - Quick reference guide for CI/CD workflows

## Notes

- All workflows use .NET 9.0 and 10.0
- Workflows are designed to be compatible with the PatternKit project structure
- Security scanning runs automatically on all pull requests and pushes
