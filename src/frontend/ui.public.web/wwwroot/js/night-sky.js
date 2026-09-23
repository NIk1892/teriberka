// The home page's decorative sky: pan one catalog projection as the page scrolls.
// No scroll interception, layout writes, network requests or continuous idle loop.
(() => {
    const sky = document.querySelector('.night-sky');
    const home = sky?.closest('.home-page');
    if (!home) return;

    const reducedMotion = matchMedia('(prefers-reduced-motion: reduce)');
    const landscape = matchMedia('(min-width: 700px)');
    const views = [...sky.querySelectorAll('svg')].map(svg => ({
        scene: svg.querySelector('.night-sky-scene'),
        y: Number(svg.dataset.travelY)
    }));
    let view, enabled, start = 0, distance = 1;
    let current = 0, target = 0, frame = 0, previousTime = 0;
    let needsMeasure = true, snap = true;

    function draw() {
        // Scrolling down moves the sky upward with the page, at a slower speed.
        const { scene, y } = view;
        if (current === 0) {
            scene.removeAttribute('transform');
            return;
        }
        scene.setAttribute('transform', `translate(0 ${(y * current).toFixed(3)})`);
    }

    function update(time) {
        frame = 0;
        if (needsMeasure) {
            const box = home.getBoundingClientRect();
            start = box.top + window.scrollY;
            // Finish the journey before the opaque sea zone enters the viewport.
            distance = Math.max(1, box.height - window.innerHeight);
            needsMeasure = false;
        }
        target = Math.max(0, Math.min(1, (window.scrollY - start) / distance));
        const elapsed = previousTime ? Math.min(64, time - previousTime) : 16;
        previousTime = time;
        current = snap ? target : current + (target - current) * (1 - Math.exp(-elapsed / 90));
        snap = false;
        if (Math.abs(target - current) < .00005) current = target;
        draw();
        if (current !== target) frame = requestAnimationFrame(update);
        else previousTime = 0;
    }

    function schedule() {
        if (enabled && !frame) frame = requestAnimationFrame(update);
    }

    function measure() {
        needsMeasure = true;
        schedule();
    }

    function sync() {
        cancelAnimationFrame(frame);
        frame = 0;
        previousTime = 0;
        enabled = !reducedMotion.matches && !document.hidden &&
            document.documentElement.dataset.theme === 'dark';
        view = views[landscape.matches ? 1 : 0];
        sky.classList.toggle('night-sky-paused', !enabled);
        if (reducedMotion.matches) {
            for (const item of views) item.scene.removeAttribute('transform');
        }
        snap = true;
        measure();
    }

    window.addEventListener('scroll', schedule, { passive: true });
    window.addEventListener('resize', measure, { passive: true });
    window.addEventListener('pageshow', sync);
    document.addEventListener('visibilitychange', sync);
    reducedMotion.addEventListener('change', sync);
    landscape.addEventListener('change', sync);
    new MutationObserver(sync).observe(document.documentElement, {
        attributes: true, attributeFilter: ['data-theme']
    });
    new ResizeObserver(measure).observe(home);
    sync();
})();
