gwr() {
    local REMOTE='origin'
    if [ -z "${1}" ]; then echo "Usage: gwr <remote-base-branch> [worktree-suffix]"; return 1; fi
    local BRANCH_NAME="${1}${2:+_${2}}"
    local REPO_ROOT
    REPO_ROOT="$(git rev-parse --show-toplevel)" || return 1
    local WORKTREE_DIR="${REPO_ROOT}/../$(basename "${REPO_ROOT}")-worktrees"
    mkdir -p "${WORKTREE_DIR}"
    git fetch -t -P "${REMOTE}" && \
    git worktree add --track -B "${BRANCH_NAME}" "${WORKTREE_DIR}/${BRANCH_NAME}" "${REMOTE}/${1}" && \
    if [ "$TERM_PROGRAM" = "vscode" ]; then code --add "${WORKTREE_DIR}/${BRANCH_NAME}"; fi
    cd "${WORKTREE_DIR}/${BRANCH_NAME}" || return 1
    if [ -f /usr/local/git-extended/pm_detect.sh ]; then
        . /usr/local/git-extended/pm_detect.sh
        run_pm_check
    fi
}
