#!/usr/bin/env bash
# Container-side (postCreateCommand): set the git identity from the GitHub CLI so
# commits are never a generic "Developer" placeholder. The workspace is a bind-mount
# of the host repo, so .git/config is shared host<->container; we set BOTH the
# container's global config and this repo's local config. No hardcoded values.
#
# HARD FAIL: if it cannot configure the identity (gh missing, not authenticated, or
# name/email unresolvable) it exits non-zero, so postCreateCommand fails and the
# container creation ABORTS — forcing you to fix gh auth before the container is
# usable. Identity is a hard requirement, not best-effort.
set -uo pipefail

_ts() { date -u +%H:%M:%S; }
info() { echo "[$(_ts)] git-id: $*"; }
die() { echo "[$(_ts)] git-id: ERROR — $*" >&2; exit 1; }

# True if $1 looks like an email (guards against gh emitting an error JSON body).
looks_like_email() { case "$1" in *@*.*) return 0 ;; *) return 1 ;; esac; }

# 1. Login detection (HARD requirement).
command -v gh >/dev/null 2>&1 || die "gh CLI not installed; cannot derive git identity. Container creation aborted."
gh auth status >/dev/null 2>&1 || die "gh is not authenticated. Fix on the HOST (the container mounts ~/.config/gh): gh auth login -h github.com -s user — then recreate the container."

login="$(gh api user --jq '.login' 2>/dev/null | tr -d '\r\n')"
info "gh authenticated as ${login:-?}."

# 2. Name: display name, fallback to login.
git_name="$(gh api user --jq '.name // .login' 2>/dev/null | tr -d '\r\n')"
[ -n "$git_name" ] || die "Could not resolve name from gh. Container creation aborted."

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
    info "No 'user' scope and no public profile email; using the GitHub noreply address."
    info "  For your real email, refresh the token (on the host): gh auth refresh -h github.com -s user"
  fi
fi
[ -n "$git_email" ] || die "Could not resolve email from gh. Container creation aborted."

# 4. Apply: container global + this repo's local (.git/config is shared via bind-mount).
git config --global user.name "$git_name"
git config --global user.email "$git_email"
repo_dir="${WORKSPACE_FOLDER:-$PWD}"
if git -C "$repo_dir" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  git -C "$repo_dir" config user.name "$git_name"
  git -C "$repo_dir" config user.email "$git_email"
fi

info "git identity = $git_name <$git_email>"
