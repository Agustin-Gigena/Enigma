// EnigmaMenu: comportamiento de los dropdowns de la barra (details[data-menu])
// + overflow "Más +" del navegador de módulos.
//
// - Apertura/cierre animados vía EnigmaGsap (adaptador GSAP: curva de la casa).
// - Cierre al hacer click en cualquier ítem (link/botón) del menú.
// - Cierre por click fuera, por Escape, y apertura exclusiva (acordeón).
// - Delegación en document: sobrevive los re-renders de Blazor sin re-bindear.
window.EnigmaMenu = {
    _globalListo: false,

    init: (id) => {
        // Comportamiento global de dropdowns: una sola vez.
        if (!window.EnigmaMenu._globalListo) {
            window.EnigmaMenu._globalListo = true;
            document.addEventListener("click", (e) => {
                const summary = e.target.closest("details[data-menu] > summary");
                if (summary) {
                    e.preventDefault();
                    const details = summary.parentElement;
                    if (details.open) {
                        window.EnigmaMenu._cerrar(details);
                    } else {
                        for (const abierto of document.querySelectorAll("details[data-menu][open]")) {
                            window.EnigmaMenu._cerrar(abierto, { sinAnimacion: true });
                        }
                        details.open = true; // la animación de apertura la pone EnigmaGsap
                        window.EnigmaGsap?.menuAbrir(
                            details.querySelector("ul"),
                            details.querySelector(".menu__chevron"));
                    }
                    return;
                }

                const item = e.target.closest("details[data-menu] a, details[data-menu] button");
                if (item) {
                    // El click navega/actúa; el menú que lo contiene se cierra.
                    window.EnigmaMenu._cerrar(item.closest("details[data-menu]"), { sinAnimacion: true });
                    return;
                }

                // Click fuera de cualquier menú: cerrar todos.
                if (!e.target.closest("details[data-menu]")) {
                    for (const abierto of document.querySelectorAll("details[data-menu][open]")) {
                        window.EnigmaMenu._cerrar(abierto, { sinAnimacion: true });
                    }
                }
            });

            document.addEventListener("keydown", (e) => {
                if (e.key !== "Escape") return;
                for (const abierto of document.querySelectorAll("details[data-menu][open]")) {
                    window.EnigmaMenu._cerrar(abierto, { sinAnimacion: true });
                    abierto.querySelector("summary")?.focus();
                }
            });
        }

        if (id) window.EnigmaMenu._initOverflow(id);
    },

    // Cierre animado (GSAP): la lista se desvanece y luego se retira open.
    _cerrar: (details, { sinAnimacion = false } = {}) => {
        if (!details || !details.open) return;
        const lista = details.querySelector("ul");
        const chevron = details.querySelector(".menu__chevron");
        const retirar = () => { details.open = false; };
        if (sinAnimacion || !lista || !window.EnigmaGsap) {
            retirar();
            return;
        }
        window.EnigmaGsap.menuCerrar(lista, chevron).then(retirar).catch(retirar);
        setTimeout(retirar, 260); // fallback si GSAP no responde
    },

    // Overflow "Más +": cuando un módulo no entra en el ancho disponible, sus
    // secciones (<li>) se vuelcan al menú "Más +". ResizeObserver recalcula al
    // cambiar el ancho; MutationObserver, al mutar el DOM (re-render de Blazor).
    _initOverflow: (id) => {
        const nav = document.getElementById(id);
        if (!nav || nav.dataset.menuInit) return;
        nav.dataset.menuInit = "1";
        const mas = nav.querySelector(".app-nav__mas");
        const masList = mas.querySelector("ul");
        const modulos = () => [...nav.querySelectorAll(".app-nav__modulo:not(.app-nav__mas)")];

        const recalcular = () => {
            // 1) Devolver las secciones a su módulo y re-mostrar todo antes de medir.
            for (const modulo of modulos()) modulo.hidden = false;
            for (const li of [...masList.children]) {
                nav.querySelector(`.app-nav__modulo[data-modulo="${li.dataset.modulo}"] ul`)
                    ?.appendChild(li);
            }
            // 2) Medir: los módulos que no entran vuelcan sus secciones a "Más +".
            const disponibles = nav.clientWidth - mas.offsetWidth - 8;
            let usado = 0;
            for (const modulo of modulos()) {
                usado += modulo.offsetWidth;
                if (usado > disponibles) {
                    for (const li of [...modulo.querySelector("ul").children]) masList.appendChild(li);
                    modulo.hidden = true;
                }
            }
            mas.hidden = masList.children.length === 0;
        };

        // Coalescer los recálculos en el próximo frame: recalcular MUEVE <li> por el
        // DOM, y hacerlo en el mismo tick del click sobre un link del menú cancela la
        // activación del anchor (la navegación se pierde). rAF queda fuera del tick.
        let pendiente = false;
        const agendar = () => {
            if (pendiente) return;
            pendiente = true;
            requestAnimationFrame(() => {
                pendiente = false;
                recalcular();
            });
        };

        const ro = new ResizeObserver(agendar);
        ro.observe(nav);
        const mo = new MutationObserver((mutaciones, obs) => {
            obs.disconnect();
            agendar();
            mo.observe(nav, { childList: true, subtree: true });
        });
        mo.observe(nav, { childList: true, subtree: true });
        recalcular();
    },
};
