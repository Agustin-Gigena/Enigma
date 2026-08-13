#!/usr/bin/env bash
# Container-side (postCreateCommand): set the git identity from the GitHub CLI so
# commits are never a generic "Developer" placeholder. The workspace is a bind-mount
# of the host repo, so .git/config is shared host<->container; we set BOTH the
# container's global config and this repo's local config. No hardcoded values.
#
# Runs from postCreateCommand. Fails soft (warns, leaves config untouched) if gh is
# not installed or not authenticated — never blocks container creation.
set -uo pipefail

_ts() { date -u +%H:%M:%S; }
info() { echo "[$(_ts)] git-id: $*"; }
warn() { echo "[$(_ts)] git-id: WARN — $*" >&2; }

# True if $1 looks like an email (guards against gh emitting an error JSON body).
looks_like_email() { case "$1" in *@*.*) return 0 ;; *) return 1 ;; esac; }

# 1. Login detection.
if ! command -v gh >/dev/null 2>&1; then
  warn "gh CLI not installed; cannot derive git identity."
  exit 0
fi
if ! gh auth status >/dev/null 2>&1; then
  warn "gh is not authenticated. Fix on the HOST (the container mounts ~/.config/gh):"
  warn "  gh auth login -h github.com -s user"
  warn "Leaving git identity untouched."
  exit 0
fi

login="$(gh api user --jq '.login' 2>/dev/null | tr -d '\r\n')"
info "gh authenticated as ${login:-?}."

# 2. Name: display name, fallback to login.
git_name="$(gh api user --jq '.name // .login' 2>/dev/null | tr -d '\r\n')"
if [ -z "$git_name" ]; then
  warn "Could not resolve name from gh; leaving identity untouched."
  exit 0
fi

# 3. Email: primary verified email (needs 'user' scope) -> public profile email -> noreply.
git_email=""
candidate="$(gh api user/emails --jq '[.[] | select(.primary==true)][0].email' 2>/dev/null | tr -d '\r\n')"
if looks_like_email "$candidate"; then git_email="$candidate"; fi
if [ -z "$git_email" ]; then
  candidate="$(gh api user --jq '.email' 2>/dev/null | tr -d '\r\n')"
  if looks_like_email "$candidate"; then git_email="$candidate"; fi
fi
if [ -z "$git_email" ]; then
  uid="$(gh api user --jq '.id' 2>/dev/null | tr -d '\r\n')"
  if [ -n "$uid" ] && [ -n "$login" ]; then
    git_email="${uid}+${login}@users.noreply.github.com"
    warn "No 'user' scope and no public profile email; using the GitHub noreply address."
    warn "  For your real email, refresh the token (on the host): gh auth refresh -h github.com -s user"
  fi
fi

if [ -z "$git_email" ]; then
  warn "Could not resolve email from gh; leaving identity untouched."
  exit 0
fi

# 4. Apply: container global + this repo's local (.git/config is shared via bind-mount).
git config --global user.name "$git_name"
git config --global user.email "$git_email"
repo_dir="${WORKSPACE_FOLDER:-$PWD}"
if git -C "$repo_dir" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  git -C "$repo_dir" config user.name "$git_name"
  git -C "$repo_dir" config user.email "$git_email"
fi

info "git identity = $git_name <$git_email>"
