#!/bin/bash
# free-ports.sh - Libera los puertos 8080 y 8081

PORTS=(8080 8081)

for port in "${PORTS[@]}"; do
    hex_port=$(printf '%04X' "$port")

    # Obtener inodos de sockets que escuchan en este puerto
    inodes=$(awk -v pat=":${hex_port}" '$2 ~ pat {print $10}' /proc/net/tcp /proc/net/tcp6 2>/dev/null)

    for inode in $inodes; do
        [ -z "$inode" ] && continue
        # Buscar qué PID tiene ese inode abierto
        for pid_dir in /proc/[0-9]*/fd; do
            pid=${pid_dir#/proc/}
            pid=${pid%/fd}
            [ -r "$pid_dir" ] || continue
            if ls -la "$pid_dir" 2>/dev/null | grep -q "socket:\[$inode\]"; then
                cmd=$(cat "/proc/$pid/comm" 2>/dev/null || echo "?")
                echo "[free-dev-ports] Killing PID $pid ($cmd) on port $port"
                kill -9 "$pid" 2>/dev/null || true
                break
            fi
        done
    done
done

sleep 1
echo "[free-dev-ports] 8080/8081 freed"
