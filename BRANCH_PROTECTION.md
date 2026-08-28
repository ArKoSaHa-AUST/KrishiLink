# 🔒 GitHub Branch Protection Rules Setup Guide

To enforce high code quality, security, and stability on **KrishiLink**, configure Branch Protection Rules in the GitHub repository settings.

---

## 🛠️ Step-by-Step Configuration

Go to: **GitHub Repo > Settings > Branches** (`https://github.com/ArKoSaHa-AUST/KrishiLink/settings/branches`) and click **"Add branch protection rule"**.

---

### Rule 1: Protection for `main` Branch

- **Branch pattern name**: `main`
- [x] **Require a pull request before merging**
  - Require approvals: `1`
  - Dismiss stale pull request approvals when new commits are pushed
  - Require review from Code Owners
- [x] **Require status checks to pass before merging**
  - Require branches to be up to date before merging
  - Search and select these required status checks:
    - `build-and-test`
    - `codeql-analysis`
    - `secret-scan`
- [x] **Require conversation resolution before merging**
- [x] **Block force pushes** (Prevent `git push --force` to `main`)
- [x] **Do not allow bypassing the above settings** (Enforce for repository admins)

---

### Rule 2: Protection for `develop` Branch

- **Branch pattern name**: `develop`
- [x] **Require a pull request before merging** (Require 1 approval)
- [x] **Require status checks to pass before merging** (`build-and-test`)
- [x] **Block force pushes**

---

## 🔐 Repository Security Settings

Navigate to **Settings > Code security and analysis** (`https://github.com/ArKoSaHa-AUST/KrishiLink/settings/security_analysis`):
1. **Dependency graph**: `Enabled`
2. **Dependabot alerts**: `Enabled`
3. **Dependabot security updates**: `Enabled`
4. **Secret scanning**: `Enabled`
5. **Secret scanning push protection**: `Enabled`
