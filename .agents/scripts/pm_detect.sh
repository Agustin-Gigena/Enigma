#!/bin/sh
# pm_detect.sh - Automatic Package Manager Detection
PM_DETECTORS="
npm:package.json:npm install --save-exact
composer:composer.json:composer install
dotnet:*.csproj:dotnet restore
dotnet:*.sln:dotnet restore
bundle:Gemfile:bundle install
cargo:Cargo.toml:cargo build
"
run_pm_check() {
    REPO_ROOT=$(git rev-parse --show-toplevel 2>/dev/null) || {
        echo "[WARN] Not a git repository, skipping package detection" >&2
        return 0
    }
    FOUND=0
    while IFS=: read -r KEY DETECT_FILE INSTALL_CMD; do
        [ -z "$KEY" ] && continue
        if find "$REPO_ROOT" -maxdepth 3 -name "$DETECT_FILE" -not -path '*/.*' 2>/dev/null | grep -q .; then
            FOUND=1
            echo "Detected: ${KEY} -> Executing: ${INSTALL_CMD}"
            (cd "$REPO_ROOT" && eval "$INSTALL_CMD") || {
                echo "[ERROR] Failed: ${INSTALL_CMD}" >&2
                continue
            }
        fi
    done <<DETECT_EOF
$PM_DETECTORS
DETECT_EOF
    if [ "$FOUND" -eq 1 ]; then
        echo "Dependency installation completed"
    fi
}
if [ "${1:-}" = "--run" ]; then
    run_pm_check
fi
