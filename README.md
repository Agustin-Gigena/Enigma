# Enigma

Repositorio principal que contiene los submódulos del proyecto Enigma.

## Estructura

```
Enigma/
├── Client/   → https://github.com/Agustin-Gigena/Enigma.Client
├── Server/   → https://github.com/Agustin-Gigena/Enigma.Server
└── Shared/   → https://github.com/Agustin-Gigena/Enigma.Shared
```

## Clonar con submódulos

```bash
# Opción 1: Clonar recursivamente
git clone --recursive https://github.com/Agustin-Gigena/Enigma.git

# Opción 2: Inicializar submódulos después de clonar
git clone https://github.com/Agustin-Gigena/Enigma.git
cd Enigma
git submodule update --init --recursive
```

## Actualizar submódulos

```bash
# Actualizar todos los submódulos a la última versión
git submodule update --remote --recursive

# O entrar a cada submódulo y hacer pull
cd Client && git pull && cd ..
cd Server && git pull && cd ..
cd Shared && git pull && cd ..
```