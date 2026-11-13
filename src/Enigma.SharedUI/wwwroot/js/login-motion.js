// Pausa la vida ambiental (.panel-vivo) cuando la pestaña no está visible.
(function () {
    window.EnigmaMotion = {
        init: function () {
            if (window.__enigmaMotionBound) {
                return;
            }
            var panels = document.querySelectorAll('.panel-vivo');
            if (!panels.length) {
                return;
            }
            window.__enigmaMotionBound = true;
            var update = function () {
                var hidden = document.hidden;
                panels.forEach(function (p) {
                    p.classList.toggle('panel-vivo--hidden', hidden);
                });
            };
            document.addEventListener('visibilitychange', update);
            update();
        }
    };
})();
