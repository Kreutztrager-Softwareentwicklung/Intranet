// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

document.addEventListener("DOMContentLoaded", function () {

    const submenuButtons =
        document.querySelectorAll("[data-submenu-toggle]");

    submenuButtons.forEach(function (button) {

        button.addEventListener("click", function (event) {

            event.preventDefault();
            event.stopPropagation();

            const submenu =
                button.closest(".dropdown-submenu");

            // Andere geöffnete Untermenüs schließen
            document
                .querySelectorAll(".dropdown-submenu.show")
                .forEach(function (otherSubmenu) {

                    if (otherSubmenu !== submenu) {
                        otherSubmenu.classList.remove("show");
                    }
                });

            // Aktuelles Untermenü öffnen/schließen
            submenu.classList.toggle("show");
        });

    });


    // Untermenüs schließen, wenn Kreutzträger+ geschlossen wird
    document
        .querySelectorAll(".dropdown")
        .forEach(function (dropdown) {

            dropdown.addEventListener(
                "hidden.bs.dropdown",
                function () {

                    dropdown
                        .querySelectorAll(".dropdown-submenu.show")
                        .forEach(function (submenu) {
                            submenu.classList.remove("show");
                        });

                });
        });

});


/* =========================================================
   DARK MODE
   ========================================================= */
(function () {
    const HTML = document.documentElement;
    const STORAGE_KEY = 'kt-theme';

    // Theme beim Laden sofort anwenden (verhindert Flackern)
    const saved = localStorage.getItem(STORAGE_KEY);
    if (saved === 'dark') {
        HTML.setAttribute('data-theme', 'dark');
    }

    document.addEventListener('DOMContentLoaded', function () {
        const btn = document.getElementById('darkModeToggle');
        const icon = document.getElementById('darkModeIcon');

        if (!btn) return;

        function applyTheme(theme) {
            if (theme === 'dark') {
                HTML.setAttribute('data-theme', 'dark');
                icon.textContent = '☀️';
                localStorage.setItem(STORAGE_KEY, 'dark');
            } else {
                HTML.setAttribute('data-theme', '');
                icon.textContent = '🌙';
                localStorage.setItem(STORAGE_KEY, 'light');
            }
        }

        // Initiales Icon setzen
        applyTheme(localStorage.getItem(STORAGE_KEY) === 'dark' ? 'dark' : 'light');

        // Klick-Event
        btn.addEventListener('click', function () {
            const current = HTML.getAttribute('data-theme');
            applyTheme(current === 'dark' ? 'light' : 'dark');
        });

        // Optional: Systemeinstellung des Browsers berücksichtigen (nur beim ersten Besuch)
        if (!localStorage.getItem(STORAGE_KEY)) {
            const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
            if (prefersDark) applyTheme('dark');
        }
    });
})();

function applyTheme(theme) {
    const logo = document.getElementById('kt-logo-img');  // NEU

    if (theme === 'dark') {
        HTML.setAttribute('data-theme', 'dark');
        icon.textContent = '☀️';
        localStorage.setItem(STORAGE_KEY, 'dark');
        if (logo) logo.src = '/Images/LogoKKT_dark.png';  // NEU
    } else {
        HTML.setAttribute('data-theme', '');
        icon.textContent = '🌙';
        localStorage.setItem(STORAGE_KEY, 'light');
        if (logo) logo.src = '/Images/LogoKKT_transparent.png';  // NEU
    }
}

