// Run from any directory: node docs/design/mobile-night-sky/generate.mjs
// Input: a small, checked-in extract of CDS V/50, not a runtime network request.
import fs from 'node:fs';
import { fileURLToPath } from 'node:url';

const source = JSON.parse(fs.readFileSync(new URL('./stars.json', import.meta.url), 'utf8'));
const radians = Math.PI / 180;
// Orient the catalog once along a vertical route, with no sideways drift.
// Start at the Bootes end so scrolling upward through the field reveals new stars below.
const angle = 75.18531992131979 * radians - Math.PI / 2 - Math.atan2(440, 610);
const declination = 55 * radians;
const travel = landscape => 900 * (landscape ? 900 : 390) / 520;
const project = (star, landscape) => {
    const a = (star.ra - 330) * radians, d = star.dec * radians;
    const k = 2 / (1 + Math.sin(declination) * Math.sin(d) + Math.cos(declination) * Math.cos(d) * Math.cos(a));
    // East is left, north is up before the common rotation. No per-constellation transforms.
    const x = -k * Math.cos(d) * Math.sin(a);
    const y = -k * (Math.cos(declination) * Math.sin(d) - Math.sin(declination) * Math.cos(d) * Math.cos(a));
    const rotatedX = x * Math.cos(angle) - y * Math.sin(angle);
    const rotatedY = x * Math.sin(angle) + y * Math.cos(angle);
    // Both views use the same orientation and a uniform scale, with a portrait crop.
    return landscape
        ? { ...star, x: 630 + 900 * rotatedX, y: 400 + travel(true) + 900 * rotatedY }
        : { ...star, x: 125 + 390 * rotatedX, y: 320 + travel(false) + 390 * rotatedY };
};
const constellations = {
    cassiopeia: [[21, 168, 264, 403, 542]],
    cygnus: [[7924, 7796, 7615, 7417], [7328, 7420, 7528, 7796, 7949, 8115]],
    lyra: [[7001, 7051, 7056, 7001], [7056, 7106, 7178, 7139, 7056]],
    cepheus: [[8238, 8974, 8694, 8238, 8162, 8316, 8494, 8465, 8571], [8162, 7957, 7850]],
    hercules: [[6220, 6418, 6324, 6212, 6220, 6168, 6092], [6418, 6485, 6695, 6588], [6212, 6148, 6095], [6324, 6410, 6406, 6148]],
    corona_borealis: [[5778, 5747, 5793, 5849, 5889, 5947, 5971]],
    bootes: [[5340, 5506, 5681, 5602, 5435, 5429, 5340, 5235, 5185], [5435, 5351, 5404]],
};
const connected = new Set(Object.values(constellations).flat(2));
const n = value => value.toFixed(2);
const parts = [
    '@* Generated from CDS Bright Star Catalogue V/50, J2000. See docs/design/mobile-night-sky.',
    '   One stereographic projection preserves the shapes and relative positions of the stars.',
    '   Decorative sky, not a live chart for a particular observing time. *@',
    '<div class="night-sky" aria-hidden="true">',
];
for (const landscape of [false, true]) {
    const layout = landscape ? 'landscape' : 'portrait';
    const stars = source.stars.map(star => project(star, landscape));
    const byId = new Map(stars.map(s => [s.hr, s]));
    for (const hr of connected) if (!byId.has(hr)) throw new Error(`Missing HR ${hr}`);
    parts.push(
        `    <svg class="night-sky-${layout}" viewBox="${landscape ? '0 0 1440 900' : '0 0 390 844'}" preserveAspectRatio="xMidYMid ${landscape ? 'meet' : 'slice'}" data-travel-y="${n(-travel(landscape))}" fill="none" focusable="false">`,
        '        <defs>',
        `            <radialGradient id="night-star-glow-${layout}">`,
        '                <stop stop-color="#deefff" stop-opacity=".7" />',
        '                <stop offset=".2" stop-color="#b5d8f4" stop-opacity=".25" />',
        '                <stop offset="1" stop-color="#b5d8f4" stop-opacity="0" />',
        '            </radialGradient>',
        `            <radialGradient id="night-star-warm-glow-${layout}">`,
        '                <stop stop-color="#fff0d8" stop-opacity=".7" />',
        '                <stop offset=".2" stop-color="#e9d2aa" stop-opacity=".25" />',
        '                <stop offset="1" stop-color="#e9d2aa" stop-opacity="0" />',
        '            </radialGradient>',
        '        </defs>',
        '        <g class="night-sky-scene">',
    );
    for (const [name, paths] of Object.entries(constellations)) {
        const d = paths.map(path => 'M' + path.map(hr => {const s = byId.get(hr); return `${n(s.x)} ${n(s.y)}`;}).join(' ')).join(' ');
        parts.push(`        <path class="night-sky-lines" data-constellation="${name}" d="${d}" />`);
    }
    parts.push('        <g class="night-sky-field">');
    for (const s of stars.filter(s => !connected.has(s.hr))) {
        // Do not draw separate unresolved companions on top of constellation stars.
        if (stars.some(main => connected.has(main.hr) && Math.hypot(s.x - main.x, s.y - main.y) < 1.5 * (landscape ? 900 / 520 : 390 / 520))) continue;
        const r = Math.max(.35, 1.45 - s.mag * .19);
        const opacity = Math.max(.2, .68 - s.mag * .08);
        parts.push(`            <circle cx="${n(s.x)}" cy="${n(s.y)}" r="${n(r)}" opacity="${n(opacity)}" />`);
    }
    parts.push('        </g>');
    let phase = 0;
    for (const hr of connected) {
        const s = byId.get(hr), bright = s.mag < 2.6, warm = s.bv >= .9;
        const r = Math.max(.65, 2.05 - .28 * s.mag);
        const halo = Math.max(4, 15 - 2.1 * s.mag);
        parts.push(`        <g class="night-sky-star${bright ? ' night-sky-star-bright' : ''}${s.mag < 1.5 ? ' night-sky-star-beacon' : ''}${warm ? ' night-sky-star-warm' : ''} night-sky-phase-${phase++ % 5}" data-hr="${hr}" data-magnitude="${s.mag}" transform="translate(${n(s.x)} ${n(s.y)})">`,
            `            <circle class="night-sky-halo" r="${n(halo * (landscape ? 1.25 : 1))}" fill="url(#night-star-${warm ? 'warm-' : ''}glow-${layout})" />`,
            `            <circle class="night-sky-core" r="${n(r * (landscape ? 1.12 : 1))}" />`,
            '        </g>');
    }
    parts.push('        </g>', '    </svg>');
}
parts.push('</div>', '');
const output = new URL('../../../src/frontend/ui.public.web/Components/NightSky.razor', import.meta.url);
fs.writeFileSync(output, parts.join('\n'));
console.log(`Generated ${fileURLToPath(output)}: 2 layouts, ${source.stars.length} catalog stars, ${connected.size} connected stars per layout.`);
