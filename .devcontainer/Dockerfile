# syntax=docker/dockerfile:1
#
# Dev image for Enigma. Built by the devcontainer CLI via compose.json, which
# injects the HOST user identity as build args (LOCAL_USER/GROUP/UID/GID come
# from .devcontainer/.env, written by setup.sh in initializeCommand). The
# container therefore runs as the SAME user (name, group, uid, gid) as the
# host that built it — fully independent of which user/uid that is.
#
# Bun is installed here in the Dockerfile (not as a devcontainer feature)
# because devcontainer features that write to /tmp during installation corrupt
# /tmp permissions (1777 → 755), breaking VS Code's X11 forwarding.
FROM mcr.microsoft.com/dotnet/sdk:10.0

ARG LOCAL_USER
ARG LOCAL_GROUP
ARG LOCAL_UID
ARG LOCAL_GID

# Create the dev user mirroring the host identity exactly.
# Hard-fail if any identity arg is missing, and never run as uid 0.
# Conflict resolution mirrors Build-It:
#   - a user with the same NAME gets removed (its home too),
#   - a user holding the target UID gets removed,
#   - a group holding the target GID under another name gets renamed,
#   - the named group with a different GID is a hard error,
#   - otherwise the group is created (or its GID updated).
# NOTE: the base image ships `ubuntu` (uid/gid 1000); when the host identity
# is 1000 this block deletes and recreates it under the host's name.
# Post-useradd steps: .config pre-creado con ownership del usuario (el bind
# mount de ~/.config/gh apunta adentro y podman crearía el padre root:root si
# falta) y sudo sin password para postCreateCommand (npm -g, agent-browser
# --with-deps).
RUN set -eu; \
    test -n "${LOCAL_USER}" && test -n "${LOCAL_GROUP}" \
        && test -n "${LOCAL_UID}" && test -n "${LOCAL_GID}"; \
    test "${LOCAL_UID}" -ne 0; \
    if getent passwd "${LOCAL_USER}" >/dev/null; then \
        userdel --remove "${LOCAL_USER}"; \
    fi; \
    uid_user="$(getent passwd "${LOCAL_UID}" | cut -d: -f1 || true)"; \
    if [ -n "${uid_user}" ]; then \
        userdel --remove "${uid_user}"; \
    fi; \
    gid_group="$(getent group "${LOCAL_GID}" | cut -d: -f1 || true)"; \
    named_group_gid="$(getent group "${LOCAL_GROUP}" | cut -d: -f3 || true)"; \
    if [ -n "${gid_group}" ] && [ "${gid_group}" != "${LOCAL_GROUP}" ]; then \
        if [ -n "${named_group_gid}" ] && [ "${named_group_gid}" != "${LOCAL_GID}" ]; then \
            echo "Host group name ${LOCAL_GROUP} already exists with GID ${named_group_gid}" >&2; \
            exit 1; \
        fi; \
        groupmod --new-name "${LOCAL_GROUP}" "${gid_group}"; \
    elif [ -z "${gid_group}" ] && [ -n "${named_group_gid}" ]; then \
        groupmod --gid "${LOCAL_GID}" "${LOCAL_GROUP}"; \
    elif [ -z "${gid_group}" ]; then \
        groupadd --gid "${LOCAL_GID}" "${LOCAL_GROUP}"; \
    fi; \
    useradd --uid "${LOCAL_UID}" --gid "${LOCAL_GID}" --create-home --shell /bin/bash "${LOCAL_USER}"; \
    install -d -o "${LOCAL_USER}" -g "${LOCAL_GROUP}" "/home/${LOCAL_USER}/.config"; \
    install -d -m 0755 /etc/sudoers.d; \
    echo "${LOCAL_USER} ALL=(root) NOPASSWD:ALL" > "/etc/sudoers.d/${LOCAL_USER}"; \
    chmod 0440 "/etc/sudoers.d/${LOCAL_USER}"

# System tools, installed at build time as root.
#   podman   -> client that talks to the HOST daemon over the mounted socket
#   yamllint -> CI lint target
#   sudo     -> lets the non-root dev user run the privileged postCreate step.
#   Chrome headless deps -> libs que agent-browser (Chrome for Testing) necesita
#                           para arrancar; ausentes en la imagen base (Ubuntu noble).
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      podman gh unzip yamllint sudo \
      libnss3 libnspr4 libatk1.0-0 libatk-bridge2.0-0 libcups2 libdrm2 \
      libxkbcommon0 libxcomposite1 libxdamage1 libxfixes3 libxrandr2 libgbm1 \
      libxss1 libasound2t64 libpango-1.0-0 libcairo2 libatspi2.0-0 \
 && rm -rf /var/lib/apt/lists/*

# dotnet-ef global tool, installed to a shared path so the non-root dev user
# can invoke it (a per-user `dotnet tool install -g` would land in root's home).
# `dotnet` is present in the base image, so this works at build time.
RUN dotnet tool install --tool-path /usr/local/share/dotnet-tools dotnet-ef \
 && ln -s /usr/local/share/dotnet-tools/dotnet-ef /usr/local/bin/dotnet-ef
ENV PATH="${PATH}:/usr/local/share/dotnet-tools"

# MAUI Android workload, pre-horneado en la imagen (root, build time).
# El SDK vive root-owned en /usr/share/dotnet: un `dotnet workload install`
# del dev user en runtime intenta escribir ahí y muere con EACCES sugiriendo
# sudo (que contamina $HOME con archivos root). En runtime el workload SOLO
# SE LEE (los manifests son input del build); NuGet y obj van al $HOME del
# usuario.
# El SDK Android SÍ va horneado: el build headless NO auto-provisiona
# (XA5300 — verificado en build real; el auto-provision era del entorno
# interactivo viejo). cmdline-tools + plataformas 35/36 + licenses aceptadas
# en build time, con ownership del dev user (sdkmanager escribe licenses y
# el usuario puede actualizar paquetes). JAVA_HOME del sistema: gradle/
# jarsigner lo necesitan y es read-only para todos.
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    JAVA_HOME=/usr/lib/jvm/java-17-openjdk-amd64 \
    ANDROID_HOME=/usr/local/android-sdk
RUN apt-get update \
 && apt-get install -y --no-install-recommends openjdk-17-jdk-headless \
 && rm -rf /var/lib/apt/lists/* \
 && dotnet workload install maui-android \
 && mkdir -p "${ANDROID_HOME}/cmdline-tools" \
 && curl -fsSL https://dl.google.com/android/repository/commandlinetools-linux-11076708_latest.zip -o /tmp/clt.zip \
 && unzip -q /tmp/clt.zip -d "${ANDROID_HOME}/cmdline-tools" \
 && mv "${ANDROID_HOME}/cmdline-tools/cmdline-tools" "${ANDROID_HOME}/cmdline-tools/latest" \
 && rm /tmp/clt.zip \
 && yes | "${ANDROID_HOME}/cmdline-tools/latest/bin/sdkmanager" --licenses > /dev/null \
 && "${ANDROID_HOME}/cmdline-tools/latest/bin/sdkmanager" "platform-tools" "platforms;android-35" "platforms;android-36" "build-tools;35.0.0" "build-tools;36.0.0" > /dev/null \
 && chown -R "${LOCAL_USER}:${LOCAL_GROUP}" "${ANDROID_HOME}"

# Bun runtime — installed here (not as a devcontainer feature) because the
# devcontainer feature layer mechanism corrupts /tmp permissions
# (mode changes from 1777 to 755, breaking VS Code's X11 forwarding
# mkdir -p '/tmp/.X11-unix' → Permission denied).
# BUN_INSTALL targets /usr/local so the binary is accessible to all users.
RUN export BUN_INSTALL=/usr/local && curl -fsSL https://bun.sh/install | bash

# Git Extended — wrappers (profile hook + pm_detect CLI) built here; the actual
# helpers (gcr, gwr, pm_detect) live in the repo at .agents/scripts/ and are
# installed by postCreateCommand (workspace bind-mount). They cannot be
# COPYed at build time: podman-compose resolves relative build contexts against
# the devcontainer CLI's override file (/tmp), so any context COPY fails.
# Installing at postCreate also avoids the /tmp permission corruption that
# devcontainer features cause (1777 → 755, breaking VS Code's X11 forwarding).
RUN install -d -m 0755 /usr/local/git-extended/functions \
 && printf '#!/bin/sh\nif [ -d "/usr/local/git-extended/functions" ]; then\n    for f in /usr/local/git-extended/functions/*.sh; do\n        [ -r "$f" ] && . "$f"\n    done\nfi\n' > /etc/profile.d/git-extended.sh \
 && chmod +x /etc/profile.d/git-extended.sh \
 && printf '#!/bin/sh\nif [ -f /usr/local/git-extended/pm_detect.sh ]; then\n    . /usr/local/git-extended/pm_detect.sh\n    run_pm_check\nfi\n' > /usr/local/bin/pm_detect \
 && chmod +x /usr/local/bin/pm_detect

# Run as the host-mirrored dev user. (node/npm come from the node FEATURE
# layered after this build, so `lefthook` is registered in postCreateCommand,
# not here.)
USER ${LOCAL_USER}
