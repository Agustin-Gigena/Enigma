// Overflow "Más +": cuando un módulo no entra en el ancho disponible, sus
// secciones (<li>) se vuelcan al menú "Más +". ResizeObserver recalcula al
// cambiar el ancho; MutationObserver, al mutar el DOM (re-render de Blazor).
window.EnigmaMenu = {
    init: (id) => {
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

        new ResizeObserver(recalcular).observe(nav);
        // Sin el disconnect, recalcular observaría sus propios moves de <li>
        // (childList) y entraría en un ciclo infinito de mutación → observer.
        const mo = new MutationObserver(() => {
            mo.disconnect();
            recalcular();
            mo.observe(nav, { childList: true, subtree: true });
        });
        mo.observe(nav, { childList: true, subtree: true });
        recalcular();
    },
};
