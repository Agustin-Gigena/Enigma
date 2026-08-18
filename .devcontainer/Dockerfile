# syntax=docker/dockerfile:1
#
# Dev image for Enigma. Built by the devcontainer CLI (devcontainer.json uses
# `build.dockerfile`), then the features (node, oh-my-pi, lefthook) are layered
# on top by the CLI.  Bun and git-extended are installed here in the Dockerfile
# because devcontainer features that write to /tmp during installation corrupt
# /tmp permissions (1777 → 755), breaking VS Code's X11 forwarding.
FROM mcr.microsoft.com/dotnet/sdk:10.0

# The base image ships a uid/gid 1000 user named `ubuntu`. We want a fixed dev
# user `dotnet` at uid/gid 1000: that uid matches the host user's uid, so with
# `--userns=keep-id` the host-owned bind mounts (workspace, ~/.omp, ~/.config/gh)
# are owned by `dotnet` inside the container and stay readable/writable.
# Rename ubuntu -> dotnet (keeping 1000); if no uid-1000 user exists, create one.
RUN set -eux; \
    old_user="$(getent passwd 1000 | cut -d: -f1)"; \
    if [ -n "$old_user" ] && [ "$old_user" != "dotnet" ]; then \
        usermod -l dotnet -d /home/dotnet "$old_user"; \
        if [ -d "/home/$old_user" ]; then mv "/home/$old_user" /home/dotnet; \
        else mkdir -p /home/dotnet; fi; \
        old_group="$(getent group 1000 | cut -d: -f1)"; \
        if [ "$old_group" != "dotnet" ]; then groupmod -n dotnet "$old_group"; fi; \
    elif [ -z "$old_user" ]; then \
        groupadd -g 1000 dotnet; \
        useradd -u 1000 -g 1000 -m -s /bin/bash dotnet; \
    fi; \
    chown -R 1000:1000 /home/dotnet; \
    install -d -o 1000 -g 1000 /home/dotnet/.config; \
    install -d -m 0755 /etc/sudoers.d; \
    echo 'dotnet ALL=(root) NOPASSWD:ALL' > /etc/sudoers.d/dotnet; \
    chmod 0440 /etc/sudoers.d/dotnet

# System tools, installed at build time as root.
#   podman   -> client that talks to the HOST daemon over the mounted socket
#   yamllint -> CI lint target
#   sudo     -> lets the non-root `dotnet` user run the privileged postCreate
#               step (`npm install -g lefthook`); NOPASSWD is granted above.
#   Chrome headless deps -> libs que agent-browser (Chrome for Testing) necesita
#                           para arrancar; ausentes en la imagen base (Ubuntu noble).
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      podman unzip yamllint sudo \
      libnss3 libnspr4 libatk1.0-0 libatk-bridge2.0-0 libcups2 libdrm2 \
      libxkbcommon0 libxcomposite1 libxdamage1 libxfixes3 libxrandr2 libgbm1 \
      libxss1 libasound2t64 libpango-1.0-0 libcairo2 libatspi2.0-0 \
 && rm -rf /var/lib/apt/lists/*

# dotnet-ef global tool, installed to a shared path so the non-root `dotnet` user
# can invoke it (a per-user `dotnet tool install -g` would land in root's home).
# `dotnet` is present in the base image, so this works at build time.
RUN dotnet tool install --tool-path /usr/local/share/dotnet-tools dotnet-ef \
 && ln -s /usr/local/share/dotnet-tools/dotnet-ef /usr/local/bin/dotnet-ef
ENV PATH="${PATH}:/usr/local/share/dotnet-tools"

# Bun runtime — installed here (not as a devcontainer feature) because the
# devcontainer feature layer mechanism corrupts /tmp permissions
# (mode changes from 1777 to 755, breaking VS Code's X11 forwarding
# mkdir -p '/tmp/.X11-unix' → Permission denied).
# BUN_INSTALL targets /usr/local so the binary is accessible to all users.
RUN export BUN_INSTALL=/usr/local && curl -fsSL https://bun.sh/install | bash

# Git Extended — shell helpers (gcr, gwr, pm_detect) installed here instead of
# as a devcontainer feature for the same /tmp permissions reason.
COPY scripts/pm_detect.sh /usr/local/git-extended/pm_detect.sh
COPY scripts/gcr.sh /usr/local/git-extended/functions/gcr.sh
COPY scripts/gwr.sh /usr/local/git-extended/functions/gwr.sh
RUN chmod +x /usr/local/git-extended/pm_detect.sh \
 && chmod +x /usr/local/git-extended/functions/*.sh \
 && printf '#!/bin/sh\nif [ -d "/usr/local/git-extended/functions" ]; then\n    for f in /usr/local/git-extended/functions/*.sh; do\n        [ -r "$f" ] && . "$f"\n    done\nfi\n' > /etc/profile.d/git-extended.sh \
 && chmod +x /etc/profile.d/git-extended.sh \
 && printf '#!/bin/sh\n. /usr/local/git-extended/pm_detect.sh\nrun_pm_check\n' > /usr/local/bin/pm_detect \
 && chmod +x /usr/local/bin/pm_detect

# Run as the dev user. (node/npm come from the node FEATURE layered after this
# build, so `lefthook` is registered in postCreateCommand, not here.)
USER dotnet
