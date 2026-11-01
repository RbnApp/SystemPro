#!/usr/bin/env node
/*
 * RbnSystemPro — yerelleştirme denetimi (DAPP-41).
 *
 * Ne yapar:
 *   1) Resources/Strings.en.xaml ve Resources/Strings.tr.xaml anahtarlarını okur.
 *   2) İki dilin anahtar kümesini karşılaştırır (eşit olmalı).
 *   3) XAML'deki {l:T anahtar} ve C# içindeki anahtar kullanımlarını (Loc.T("..."),
 *      *Key = "...", AddEventKey("...") ve ConverterParameter=...) toplar.
 *   4) Kullanılan ama tanımsız anahtarı ve tanımlı ama kullanılmayan anahtarı listeler.
 *
 * Kullanım:  node tools/i18n-check.js     (rapor yazar; hata varsa çıkış kodu 1)
 * Rapor ayrıca Logs/i18n-check.txt dosyasına yazılır.
 */

const fs = require('fs');
const path = require('path');

const ROOT = path.resolve(__dirname, '..');
const EN = path.join(ROOT, 'Resources', 'Strings.en.xaml');
const TR = path.join(ROOT, 'Resources', 'Strings.tr.xaml');

// Kasıtlı olarak tanımsız bırakılan anahtar (eksik-anahtar yedeğini sınayan test).
const INTENTIONAL = new Set(['this.key.does.not.exist']);

function readKeys(file) {
  const xml = fs.readFileSync(file, 'utf8');
  const keys = new Map();
  const re = /<sys:String x:Key="([^"]+)">([\s\S]*?)<\/sys:String>/g;
  let m;
  while ((m = re.exec(xml)) !== null) keys.set(m[1], m[2]);
  return keys;
}

function walk(dir, out = []) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    if (['bin', 'obj', 'node_modules', '.git', 'Logs'].includes(entry.name)) continue;
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) walk(full, out);
    else out.push(full);
  }
  return out;
}

const en = readKeys(EN);
const tr = readKeys(TR);

const onlyEn = [...en.keys()].filter((k) => !tr.has(k)).sort();
const onlyTr = [...tr.keys()].filter((k) => !en.has(k)).sort();
const emptyValues = [...en.keys()]
  .filter((k) => tr.has(k))
  .filter((k) => !en.get(k).trim() || !tr.get(k).trim())
  .sort();

const used = new Map(); // key -> Set(dosya)

function mark(key, rel) {
  if (!used.has(key)) used.set(key, new Set());
  used.get(key).add(rel);
}

const files = walk(ROOT).filter((f) => /\.(xaml|cs)$/.test(f));
const knownPrefix = /^(nav|main|dash|cleaner|programs|drivers|drv|gpu|prog|cat|network|perf|security|wifi|settings|timeline|event|alert|status|deploy|hw|internal|common|loading|error)\.[A-Za-z0-9_.]+$/;

for (const file of files) {
  const rel = path.relative(ROOT, file);
  const text = fs.readFileSync(file, 'utf8');

  let m;
  if (file.endsWith('.xaml')) {
    // XAML: {l:T key}
    const xamlRe = /\{l:[TB]\s+([A-Za-z0-9_.]+)\s*\}/g;
    while ((m = xamlRe.exec(text)) !== null) mark(m[1], rel);

    // XAML: ConverterParameter=<anahtar>
    const convRe = /ConverterParameter=([A-Za-z0-9_.]+)/g;
    while ((m = convRe.exec(text)) !== null) mark(m[1], rel);
  } else {
    // C#: yorum satırlarını atla, sonra anahtar biçimindeki tüm dizgi sabitlerini topla.
    for (const line of text.split(/\r?\n/)) {
      const trimmed = line.trimStart();
      if (trimmed.startsWith('//')) continue;
      const lit = /"([a-z][A-Za-z0-9_.]*)"/g;
      let k;
      while ((k = lit.exec(line)) !== null) {
        if (knownPrefix.test(k[1])) mark(k[1], rel);
      }
    }
  }
}

const missing = [...used.keys()].filter((k) => !INTENTIONAL.has(k) && (!en.has(k) || !tr.has(k))).sort();
const unused = [...en.keys()].filter((k) => !used.has(k)).sort();

const lines = [];
lines.push('RbnSystemPro i18n denetimi');
lines.push('==========================');
lines.push(`kaynak dosya : Resources/Strings.en.xaml, Resources/Strings.tr.xaml`);
lines.push(`en anahtar   : ${en.size}`);
lines.push(`tr anahtar   : ${tr.size}`);
lines.push(`sadece en    : ${onlyEn.length}${onlyEn.length ? ' -> ' + onlyEn.join(', ') : ''}`);
lines.push(`sadece tr    : ${onlyTr.length}${onlyTr.length ? ' -> ' + onlyTr.join(', ') : ''}`);
lines.push(`boş değer    : ${emptyValues.length}${emptyValues.length ? ' -> ' + emptyValues.join(', ') : ''}`);
lines.push(`kullanılan   : ${used.size}`);
lines.push(`tanımsız (!) : ${missing.length}${missing.length ? ' -> ' + missing.map((k) => `${k} (${[...used.get(k)].join(', ')})`).join(' | ') : ''}`);
lines.push(`kullanılmayan: ${unused.length}${unused.length ? ' -> ' + unused.join(', ') : ''}`);
lines.push('');

const ok = onlyEn.length === 0 && onlyTr.length === 0 && emptyValues.length === 0 && missing.length === 0;
lines.push(ok ? 'SONUÇ: OK' : 'SONUÇ: HATA');

const report = lines.join('\n');
console.log(report);

try {
  fs.mkdirSync(path.join(ROOT, 'Logs'), { recursive: true });
  fs.writeFileSync(path.join(ROOT, 'Logs', 'i18n-check.txt'), report + '\n', 'utf8');
} catch {
  /* rapor yazılamazsa yalnız konsol */
}

process.exit(ok ? 0 : 1);
