"""
Verificador de textos traducidos de Turbix. Solo lee; no modifica nada.

    python tools/check_strings.py            # todo el proyecto
    python tools/check_strings.py Views/AppsView.xaml Core/AppxOps.cs   # solo esos archivos (rutas relativas a src/Turbix)

Comprueba:
  1. Cada clave usada en el código (L.T / L.F / L.P y {l:T ...} en XAML) existe en los 6 idiomas.
  2. Los marcadores {0}, {1}… son los mismos en todos los idiomas.
  3. Los plurales tienen sus formas (.one/.other; en ruso .one/.few/.many).
  4. No hay claves repetidas en dos archivos distintos del mismo idioma.
  5. Textos en español que siguen escritos en el código en lugar de usar una clave (aproximado).
"""
import os
import re
import sys

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'src', 'Turbix')
ROOT = os.path.normpath(ROOT)
LANGS = ['es', 'en', 'pt', 'fr', 'de', 'ru']
PLURAL_FORMS = {'ru': ['one', 'few', 'many']}
DEFAULT_FORMS = ['one', 'other']
SKIP_DIRS = {'bin', 'obj', 'Testing'}


def load_strings():
    tables = {l: {} for l in LANGS}
    origin = {l: {} for l in LANGS}
    problems = []
    folder = os.path.join(ROOT, 'Strings')
    for name in sorted(os.listdir(folder)):
        m = re.match(r'^(.+)\.([a-z]{2})\.txt$', name)
        if not m or m.group(2) not in LANGS:
            continue
        lang = m.group(2)
        with open(os.path.join(folder, name), encoding='utf-8') as f:
            for n, line in enumerate(f, 1):
                t = line.strip()
                if not t or t.startswith('#'):
                    continue
                if '=' not in t:
                    problems.append(f'{name}:{n}: línea sin «=»')
                    continue
                key, value = t.split('=', 1)
                key = key.strip()
                value = value[1:] if value.startswith(' ') else value
                if key in origin[lang] and origin[lang][key] != name:
                    problems.append(f'{name}:{n}: clave «{key}» repetida (ya está en {origin[lang][key]})')
                elif key in tables[lang]:
                    problems.append(f'{name}:{n}: clave «{key}» repetida en el mismo archivo')
                tables[lang][key] = value.rstrip()
                origin[lang][key] = name
    return tables, origin, problems


def source_files(only):
    if only:
        for rel in only:
            yield os.path.normpath(os.path.join(ROOT, rel))
        return
    for dirpath, dirnames, filenames in os.walk(ROOT):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        for fn in filenames:
            if fn.endswith('.cs') or fn.endswith('.xaml'):
                yield os.path.join(dirpath, fn)


KEY_T = re.compile(r'\bL\.(T|F|Has)\(\s*"([^"]+)"')
KEY_P = re.compile(r'\bL\.P\(\s*"([^"]+)"')
KEY_X = re.compile(r'\{l:T\s+([^}\s,]+)\s*\}')
PLACEHOLDER = re.compile(r'\{(\d+)(?:[,:][^}]*)?\}')

# Señales de texto en español escrito a mano (no exhaustivo).
SPANISH = re.compile(r'[áéíóúñ¿¡«»]|\b(el|la|los|las|de|del|que|para|con|una?|este|esta|desde|sin|más|puede|se|no|tu|tus|al)\b', re.I)
CS_STRING = re.compile(r'(?<![@$\w])"((?:[^"\\\n]|\\.)*)"|\$"((?:[^"\\\n]|\\.)*)"')
XAML_TEXT = re.compile(r'\b(Text|Content|Tag|ToolTip|Title|Header)="([^"{][^"]*)"')


def looks_spanish(s):
    if len(s) < 3 or not re.search(r'[A-Za-zÁÉÍÓÚáéíóúñÑ]', s):
        return False
    if re.fullmatch(r'[\w.\\/:*%$-]+', s) and ' ' not in s:
        return False   # identificadores, rutas, claves
    return bool(SPANISH.search(s))


def main():
    only = sys.argv[1:]
    tables, origin, problems = load_strings()
    used_t, used_p = {}, {}
    leftovers = []

    for path in source_files(only):
        rel = os.path.relpath(path, ROOT)
        with open(path, encoding='utf-8') as f:
            text = f.read()
        for m in KEY_T.finditer(text):
            if not m.group(2).endswith('.'):   # clave armada en tiempo de ejecución («log.level.» + nivel)
                used_t.setdefault(m.group(2), rel)
        for m in KEY_X.finditer(text):
            used_t.setdefault(m.group(1), rel)
        for m in KEY_P.finditer(text):
            used_p.setdefault(m.group(1), rel)

        for n, line in enumerate(text.splitlines(), 1):
            s = line.strip()
            if s.startswith('//') or s.startswith('///') or s.startswith('<!--') or s.startswith('*') or s.startswith('#'):
                continue
            if path.endswith('.cs'):
                if 'Logger.Debug' in s or 'DllImport' in s or 'Registry' in s and '\\' in s:
                    pass
                code = line.split('//')[0] if '"' not in line.split('//')[0][-1:] else line
                for m in CS_STRING.finditer(code):
                    lit = m.group(1) if m.group(1) is not None else m.group(2)
                    if lit and looks_spanish(lit) and 'L.' not in code[max(0, m.start() - 8):m.start()]:
                        leftovers.append(f'{rel}:{n}: "{lit[:70]}"')
            else:
                for m in XAML_TEXT.finditer(line):
                    if looks_spanish(m.group(2)):
                        leftovers.append(f'{rel}:{n}: {m.group(1)}="{m.group(2)[:70]}"')

    missing = []
    for key, rel in sorted(used_t.items()):
        for lang in LANGS:
            if key not in tables[lang]:
                missing.append(f'{key} [{lang}]  (usada en {rel})')
    for key, rel in sorted(used_p.items()):
        for lang in LANGS:
            forms = PLURAL_FORMS.get(lang, DEFAULT_FORMS)
            for form in forms:
                if f'{key}.{form}' not in tables[lang] and f'{key}.other' not in tables[lang]:
                    missing.append(f'{key}.{form} [{lang}]  (plural, usada en {rel})')

    mismatch = []
    for key, es_value in tables['es'].items():
        base = PLACEHOLDER.findall(es_value)
        for lang in LANGS[1:]:
            v = tables[lang].get(key)
            if v is None:
                continue
            if sorted(set(PLACEHOLDER.findall(v))) != sorted(set(base)) and not re.search(r'\.(one|few|many|other)$', key):
                mismatch.append(f'{key} [{lang}]: es={sorted(set(base))} {lang}={sorted(set(PLACEHOLDER.findall(v)))}')

    not_in_others = []
    if not only:
        for key in sorted(tables['es']):
            base = re.sub(r'\.(one|few|many|other)$', '', key)
            for lang in LANGS[1:]:
                if key not in tables[lang]:
                    if re.search(r'\.(one|other)$', key) and lang == 'ru':
                        if any(f'{base}.{f}' in tables['ru'] for f in ('one', 'few', 'many')):
                            continue
                    not_in_others.append(f'{key} [{lang}]')

    def section(title, items):
        print(f'== {title}: {len(items)}')
        for i in items[:400]:
            print('   ' + i)

    section('Problemas en archivos de textos', problems)
    section('Claves usadas que faltan', missing)
    section('Marcadores {n} distintos entre idiomas', mismatch)
    if not only:
        section('Claves del español que faltan en otro idioma', not_in_others)
    section('Posible texto en español sin clave (revisar a mano)', leftovers)
    total = len(problems) + len(missing) + len(mismatch) + len(not_in_others)
    print('OK' if total == 0 else f'{total} problemas (sin contar los textos a revisar a mano)')
    return 1 if total else 0


if __name__ == '__main__':
    sys.exit(main())
