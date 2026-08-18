// Полноэкранный просмотр фотографий на страницах мест. Ссылки .lightbox-link
// ведут на сам файл изображения (фоллбек без JavaScript — открытие в новой
// вкладке); скрипт перехватывает клик и показывает кадр в оверлее поверх
// страницы. Закрытие — клик в любом месте или Esc.
(() => {
    const links = document.querySelectorAll("a.lightbox-link");
    if (!links.length) return;

    const overlay = document.createElement("div");
    overlay.className = "lightbox";
    overlay.hidden = true;
    const img = document.createElement("img");
    img.alt = "";
    overlay.append(img);
    document.body.append(overlay);

    const close = () => {
        overlay.hidden = true;
        img.src = "";
        document.documentElement.classList.remove("lightbox-open");
    };

    overlay.addEventListener("click", close);
    document.addEventListener("keydown", (e) => {
        if (e.key === "Escape" && !overlay.hidden) close();
    });

    for (const link of links) {
        link.addEventListener("click", (e) => {
            e.preventDefault();
            img.src = link.href;
            overlay.hidden = false;
            document.documentElement.classList.add("lightbox-open");
        });
    }
})();
