// Переключатели темы и языка — обычные ссылки на /set-theme и /set-culture с
// редиректом на ту же страницу: это полный рефреш, и прокрутка сбрасывается
// наверх. Скрипт запоминает позицию перед таким переходом (sessionStorage) и
// восстанавливает её после загрузки. Без JavaScript поведение остаётся прежним.
(() => {
    const KEY = "kola-restore-scroll";

    document.addEventListener("click", (e) => {
        const link = e.target.closest('a[href^="/set-theme"], a[href^="/set-culture"]');
        if (!link) return;
        try {
            sessionStorage.setItem(KEY, JSON.stringify({
                path: location.pathname,
                y: Math.round(window.scrollY),
            }));
        } catch { /* приватный режим или квота — просто не восстановим позицию */ }
    });

    let saved = null;
    try {
        const raw = sessionStorage.getItem(KEY);
        if (raw) {
            sessionStorage.removeItem(KEY);
            saved = JSON.parse(raw);
        }
    } catch { /* повреждённое значение игнорируем */ }

    if (saved && saved.path === location.pathname && saved.y > 0) {
        // временно глушим scroll-behavior: smooth, чтобы страница не «доезжала» на глазах
        const html = document.documentElement;
        const prev = html.style.scrollBehavior;
        html.style.scrollBehavior = "auto";
        window.scrollTo(0, saved.y);
        html.style.scrollBehavior = prev;
    }
})();
