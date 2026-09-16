// Tabs de módulos: mide cuántos entran en la barra y devuelve ese número.
// El resto cae al menú "Más +" (decisión en Blazor: NavEscritorio).
window.EnigmaNavTabs = (() => {
  let ref = null;

  function medir(contSelector) {
    const cont = document.querySelector(contSelector);
    if (!cont) return 0;
    const tabs = [...cont.querySelectorAll('.app-nav__modulo')];
    if (tabs.length === 0) return 0;
    const mas = cont.querySelector('.app-nav__mas');
    const anchoMas = mas && !mas.classList.contains('app-nav__mas--oculto')
      ? mas.offsetWidth
      : 64; // ancho estimado del botón "Más +" antes de renderizarse
    const limite = cont.clientWidth - 8;
    let acc = 0;
    let n = 0;
    for (const t of tabs) {
      acc += t.offsetWidth + 4; // gap del contenedor
      if (acc + anchoMas <= limite) n++;
      else break;
    }
    return Math.max(n, 1); // al menos un tab visible
  }

  return {
    init: (dotnetRef, contSelector) => {
      ref = dotnetRef;
      const avisar = () => ref && ref.invokeMethodAsync('OnResize');
      window.addEventListener('resize', avisar);
      // Re-medir cuando las fuentes terminan de cargar (cambia el ancho de los tabs).
      if (document.fonts && document.fonts.ready) {
        document.fonts.ready.then(avisar);
      }
      setTimeout(avisar, 120);
    },
    medir,
  };
})();
