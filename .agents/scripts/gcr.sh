gcr() {
    local REMOTE='origin'
    if [ -z "${1}" ]; then echo "Usage: gcr <remote-base-branch> [new-branch-suffix]"; return 1; fi
    local BRANCH_NAME="${1}${2:+_${2}}"
    git fetch -t -P "${REMOTE}" && \
    git checkout -t "${REMOTE}/${1}" -B "${BRANCH_NAME}" && \
    if [ -f /usr/local/git-extended/pm_detect.sh ]; then
        . /usr/local/git-extended/pm_detect.sh
        run_pm_check
    fi
}
