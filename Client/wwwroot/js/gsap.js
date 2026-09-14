// EnigmaGsap: adaptador de GSAP para la app — la única puerta de entrada a
// animaciones. Las vistas NUNCA llaman a gsap (ni a este archivo) por JS interop:
// consumen Client/Services/Gsap.cs, que delega acá. La infraestructura JS propia
// (enigma-menu.js) también pasa por este adaptador para abrir/cerrar menúes.
//
// Curva de la casa: cubic-bezier(0.16, 1, 0.3, 1) ≈ "power3.out" de GSAP.
// Duraciones: dropdowns 180ms (estado), entradas de vista 320ms.
// prefers-reduced-motion: todo se resuelve en 1ms sin desplazamientos.
window.EnigmaGsap = (() => {
    const reduce = () => window.matchMedia("(prefers-reduced-motion: reduce)").matches;

    const listo = (cb) => {
        if (window.gsap) return cb();
        // gsap.min.js carga antes en el HTML; por robustez, esperar un tick.
        return new Promise((res) => {
            const t = setInterval(() => {
                if (window.gsap) { clearInterval(t); res(cb()); }
            }, 10);
        });
    };

    const gsapTo = (targets, vars) => window.gsap.to(targets, {
        duration: reduce() ? 0.001 : (vars.duracion ?? 0.18),
        ease: reduce() ? "none" : "power3.out",
        ...vars,
    });

    return {
        /// Entrada de vista (contenido de página tras navegar): fade + slide suave.
        vistaEntrada: (selector) => listo(() => {
            window.gsap.fromTo(selector,
                { opacity: 0, y: reduce() ? 0 : 10 },
                { duration: reduce() ? 0.001 : 0.32, ease: "power3.out", clearProps: "all" });
        }),

        /// Apertura de dropdown de menú (lista + chevron del trigger).
        menuAbrir: (lista, chevron) => listo(() => {
            gsapTo(lista, { duracion: 0.18, opacity: 1, y: 0, filter: "blur(0px)",
                            startAt: { opacity: 0, y: -6, filter: "blur(2px)" } });
            if (chevron) gsapTo(chevron, { duracion: 0.18, rotate: 180 });
        }),

        /// Cierre de dropdown: devuelve una promesa que resuelve al terminar.
        menuCerrar: (lista, chevron) => listo(() => new Promise((res) => {
            if (reduce()) { res(); return; }
            gsapTo(lista, { duracion: 0.15, opacity: 0, y: -4, filter: "blur(1px)" }).then(res);
            if (chevron) gsapTo(chevron, { duracion: 0.15, rotate: 0 });
        })),

        /// Entrada escalonada de elementos (filas de tabla, tarjetas).
        entradaEscalonada: (selector, base = 0.06) => listo(() => {
            const els = window.gsap.utils.toArray(selector);
            if (!els.length) return;
            window.gsap.fromTo(els,
                { opacity: 0, y: reduce() ? 0 : 8 },
                { duration: reduce() ? 0.001 : 0.26, ease: "power3.out", stagger: base, clearProps: "all" });
        }),

        /// Resaltar un elemento (feedback de acción: guardado, selección).
        pulso: (selector) => listo(() => {
            if (reduce()) return;
            window.gsap.fromTo(selector,
                { backgroundColor: "rgba(20, 102, 92, 0.18)" },
                { duration: 0.5, ease: "power2.out", backgroundColor: "rgba(20, 102, 92, 0)", clearProps: "backgroundColor" });
        }),
    };
})();
