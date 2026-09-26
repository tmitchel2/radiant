// Generates the fixtures Radiant.MaterialColor's tests compare against: the outputs of upstream
// material-color-utilities (TypeScript, compiled) over a spread of inputs.
//
//   node generate.mjs <compiled-upstream-typescript-dir> <output-dir>
//
// Run through generate.sh, which checks out the pinned upstream commit and builds it first. Colours
// are written as unsigned 0xAARRGGBB numbers; the C# side reads them into ints.
import {writeFileSync} from 'node:fs';
import {gzipSync} from 'node:zlib';
import {join, resolve} from 'node:path';
import {pathToFileURL} from 'node:url';

const [upstreamDir, outDir] = process.argv.slice(2).map((p) => resolve(p));
const mcu = await import(pathToFileURL(join(upstreamDir, 'index.js')).href);
const {
  Hct, HctSolver, Cam16, TonalPalette, Blend, Contrast, DislikeAnalyzer, TemperatureCache,
  MaterialDynamicColors,
  DynamicScheme, Variant,
  SchemeTonalSpot, SchemeContent, SchemeFidelity, SchemeVibrant, SchemeExpressive, SchemeNeutral,
  SchemeMonochrome, SchemeRainbow, SchemeFruitSalad, SchemeCmf,
} = mcu;
const {HctSolver: Solver} = await import(pathToFileURL(join(upstreamDir, 'hct/hct_solver.js')).href);

const u32 = (argb) => argb >>> 0;

function write(name, data) {
  const json = JSON.stringify(data);
  const path = join(outDir, name + '.json.gz');
  writeFileSync(path, gzipSync(json, {level: 9}));
  console.log(`${name}: ${json.length} bytes json -> ${path}`);
}

// A deterministic PRNG, so the inputs are the same every run (and stored in the fixture anyway).
function mulberry32(seed) {
  return () => {
    seed |= 0; seed = seed + 0x6D2B79F5 | 0;
    let t = Math.imul(seed ^ seed >>> 15, 1 | seed);
    t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t;
    return ((t ^ t >>> 14) >>> 0) / 4294967296;
  };
}

const seeds = [
  0xFF6750A4, // Material baseline purple
  0xFF4285F4, // Google blue
  0xFFEA4335, // red
  0xFFFBBC05, // yellow
  0xFF34A853, // green
  0xFF000000, // black
  0xFFFFFFFF, // white
  0xFF808080, // grey
  0xFF6B6B00, // dark yellow-green: disliked
  0xFF00FFFF, // cyan
  0xFFFF00FF, // magenta
  0xFF0000FF, // pure blue
  0xFFF97316, // orange
  0xFF14B8A6, // teal
  0xFF5D4037, // brown
  0xFF1E3A8A, // deep navy
].map(u32);

// --- HCT ---------------------------------------------------------------------------------------
{
  const fromInt = [];
  for (let r = 0; r < 256; r += 17) {
    for (let g = 0; g < 256; g += 17) {
      for (let b = 0; b < 256; b += 17) {
        const argb = u32((255 << 24) | (r << 16) | (g << 8) | b);
        const hct = Hct.fromInt(argb);
        fromInt.push([argb, hct.hue, hct.chroma, hct.tone]);
      }
    }
  }
  const solve = [];
  for (let hue = 0; hue < 360; hue += 15) {
    for (const chroma of [0, 5, 16, 36, 48, 60, 100, 150]) {
      for (const tone of [0, 0.5, 1, 10, 25, 40, 50, 60, 75, 90, 99, 99.9, 100]) {
        solve.push([hue, chroma, tone, u32(Solver.solveToInt(hue, chroma, tone))]);
      }
    }
  }
  const cam16 = seeds.map((argb) => {
    const c = Cam16.fromInt(argb);
    return [argb, c.hue, c.chroma, c.j, c.q, c.m, c.s, c.jstar, c.astar, c.bstar];
  });
  write('hct', {fromInt, solve, cam16});
}

// --- Palettes, contrast, dislike, blend, temperature -------------------------------------------
{
  const tones = [0, 4, 5, 6, 10, 12, 15, 17, 20, 22, 24, 25, 30, 35, 40, 50, 60, 70, 80, 87, 90, 92, 94, 95, 96, 98, 99, 100];
  const palettes = seeds.map((argb) => {
    const p = TonalPalette.fromInt(argb);
    return {argb, hue: p.hue, chroma: p.chroma, keyColor: u32(p.keyColor.toInt()), tones: tones.map((t) => u32(p.tone(t)))};
  });
  const hueChroma = [];
  for (let hue = 0; hue < 360; hue += 30) {
    for (const chroma of [0, 8, 16, 48, 80, 200]) {
      const p = TonalPalette.fromHueAndChroma(hue, chroma);
      hueChroma.push({hue, chroma, keyColor: u32(p.keyColor.toInt()), tones: tones.map((t) => u32(p.tone(t)))});
    }
  }
  write('palettes', {tones, palettes, hueChroma});

  const contrastTones = [0, 10, 20, 30, 40, 49, 50, 51, 60, 70, 80, 90, 100];
  const ratios = [1, 1.5, 3, 4.5, 7, 11, 21];
  const contrast = {ratioOfTones: [], lighter: [], darker: [], lighterUnsafe: [], darkerUnsafe: []};
  for (const a of contrastTones) {
    for (const b of contrastTones) contrast.ratioOfTones.push([a, b, Contrast.ratioOfTones(a, b)]);
    for (const r of ratios) {
      contrast.lighter.push([a, r, Contrast.lighter(a, r)]);
      contrast.darker.push([a, r, Contrast.darker(a, r)]);
      contrast.lighterUnsafe.push([a, r, Contrast.lighterUnsafe(a, r)]);
      contrast.darkerUnsafe.push([a, r, Contrast.darkerUnsafe(a, r)]);
    }
  }
  write('contrast', contrast);

  const dislike = [];
  for (let hue = 60; hue <= 130; hue += 5) {
    for (const chroma of [10, 20, 40]) {
      for (const tone of [30, 50, 64, 66, 80]) {
        const hct = Hct.from(hue, chroma, tone);
        dislike.push([u32(hct.toInt()), DislikeAnalyzer.isDisliked(hct), u32(DislikeAnalyzer.fixIfDisliked(hct).toInt())]);
      }
    }
  }
  write('dislike', dislike);

  const blend = [];
  for (const design of seeds) {
    for (const source of seeds.slice(0, 8)) {
      const row = {design, source, harmonize: u32(Blend.harmonize(design, source)), hctHue: [], cam16Ucs: []};
      for (const amount of [0, 0.25, 0.5, 0.8, 1]) {
        row.hctHue.push(u32(Blend.hctHue(design, source, amount)));
        row.cam16Ucs.push(u32(Blend.cam16Ucs(design, source, amount)));
      }
      blend.push(row);
    }
  }
  write('blend', blend);

  const temperature = seeds.map((argb) => {
    const hct = Hct.fromInt(argb);
    const cache = new TemperatureCache(hct);
    return {
      argb,
      rawTemperature: TemperatureCache.rawTemperature(hct),
      inputRelativeTemperature: cache.inputRelativeTemperature,
      complement: u32(cache.complement.toInt()),
      warmest: u32(cache.warmest.toInt()),
      coldest: u32(cache.coldest.toInt()),
      analogous: cache.analogous().map((h) => u32(h.toInt())),
      analogous3of6: cache.analogous(3, 6).map((h) => u32(h.toInt())),
    };
  });
  write('temperature', temperature);
}

// --- Quantize and score inputs -----------------------------------------------------------------
{
  const images = [];
  // Clustered: a few colours with noise, in uneven proportions.
  for (const [seed, count] of [[1, 2000], [2, 5000], [3, 800]]) {
    const random = mulberry32(seed);
    const centres = Array.from({length: 2 + Math.floor(random() * 6)}, () => [random() * 255, random() * 255, random() * 255, random()]);
    const pixels = [];
    for (let i = 0; i < count; i++) {
      let pick = random() * centres.reduce((s, c) => s + c[3], 0);
      let c = centres[0];
      for (const candidate of centres) { if ((pick -= candidate[3]) <= 0) { c = candidate; break; } }
      const jitter = () => (random() - 0.5) * 30;
      const ch = (v) => Math.max(0, Math.min(255, Math.round(v + jitter())));
      pixels.push(u32((255 << 24) | (ch(c[0]) << 16) | (ch(c[1]) << 8) | ch(c[2])));
    }
    images.push(pixels);
  }
  // Uniform noise, and a greyscale ramp (every colour rejected by scoring's chroma filter).
  {
    const random = mulberry32(4);
    images.push(Array.from({length: 3000}, () => u32((255 << 24) | Math.floor(random() * 0xFFFFFF))));
    images.push(Array.from({length: 1024}, (_, i) => { const v = i & 255; return u32((255 << 24) | (v << 16) | (v << 8) | v); }));
  }
  // Quantized by upstream's Java port (see QuantizeFixtures.java): its k-means is seeded, the
  // TypeScript one is not. One image per line, pixels as signed ints.
  writeFileSync(join(outDir, 'quantize-inputs.txt'), images.map((pixels) => pixels.map((p) => p | 0).join(' ')).join('\n'));
}

// --- Dynamic schemes ---------------------------------------------------------------------------
{
  const colors = new MaterialDynamicColors();
  const roles = colors.allColors.filter((c) => c !== undefined).map((c) => c.name);
  const schemeClasses = {
    [Variant.MONOCHROME]: SchemeMonochrome, [Variant.NEUTRAL]: SchemeNeutral, [Variant.TONAL_SPOT]: SchemeTonalSpot,
    [Variant.VIBRANT]: SchemeVibrant, [Variant.EXPRESSIVE]: SchemeExpressive, [Variant.FIDELITY]: SchemeFidelity,
    [Variant.CONTENT]: SchemeContent, [Variant.RAINBOW]: SchemeRainbow, [Variant.FRUIT_SALAD]: SchemeFruitSalad,
    [Variant.CMF]: SchemeCmf,
  };
  const variantName = (v) => Variant[v];
  const schemes = [];
  const add = (sourceArgbs, variant, isDark, contrastLevel, specVersion, platform) => {
    const Cls = schemeClasses[variant];
    const hcts = sourceArgbs.map((a) => Hct.fromInt(a));
    let scheme;
    try {
      scheme = new Cls(hcts.length === 1 ? hcts[0] : hcts, isDark, contrastLevel, specVersion, platform);
    } catch (e) {
      // Some combinations are refused (CMF needs spec 2026); the port must refuse them too.
      schemes.push({source: sourceArgbs, variant: variantName(variant), isDark, contrastLevel, specVersion, platform, throws: true});
      return;
    }
    const values = colors.allColors.filter((c) => c !== undefined).map((c) => {
      const argb = c.getArgb(scheme);
      return argb === undefined ? null : u32(argb);
    });
    schemes.push({
      source: sourceArgbs, variant: variantName(variant), isDark, contrastLevel, specVersion, platform,
      resolvedSpec: scheme.specVersion, values,
    });
  };
  const variants = Object.keys(schemeClasses).map(Number);
  for (const seed of seeds) {
    for (const variant of variants) {
      for (const isDark of [false, true]) {
        for (const contrastLevel of [-1, 0, 0.5, 1]) {
          for (const specVersion of ['2021', '2025', '2026']) {
            add([seed], variant, isDark, contrastLevel, specVersion, 'phone');
          }
        }
      }
    }
  }
  for (const seed of seeds.slice(0, 4)) {
    for (const variant of variants) {
      for (const isDark of [false, true]) {
        for (const contrastLevel of [0, 1]) {
          for (const specVersion of ['2025', '2026']) {
            add([seed], variant, isDark, contrastLevel, specVersion, 'watch');
          }
        }
      }
    }
  }
  // Several source colours (used by the CMF variant; the others take the first).
  for (const sources of [[seeds[0], seeds[3]], [seeds[1], seeds[2], seeds[4]]]) {
    for (const variant of variants) {
      for (const isDark of [false, true]) {
        for (const specVersion of ['2021', '2025', '2026']) {
          add(sources, variant, isDark, 0, specVersion, 'phone');
        }
      }
    }
  }
  write('schemes', {roles, schemes});
}
