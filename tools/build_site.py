#!/usr/bin/env python3
"""Builds the project website into docs/ (static HTML, no external requests).

    python tools/build_site.py

Pages: docs/index.html (English) and docs/<lang>/index.html for es, pt-BR, fr, de, ru.
Texts live in tools/site_strings.py. Images are converted from assets/ to WebP.
"""
import html
import os
import shutil
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from site_strings import LANGS, NATIVE, HTML_LANG, OG_LOCALE, S  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "docs")
BASE = "https://julianr87.github.io/TURBiX/"
REPO = "https://github.com/JulianR87/TURBiX"
SHOTS = ["privacy", "cleanup", "performance", "fixes"]
SUFFIX = {"en": "", "es": ".es", "pt-BR": ".pt-BR", "fr": ".fr", "de": ".de", "ru": ".ru"}
TRANSLATING = {"en": "tools/TRANSLATING.md", "es": "tools/TRADUCIR.md", "pt-BR": "tools/TRANSLATING.pt-BR.md",
               "fr": "tools/TRANSLATING.fr.md", "de": "tools/TRANSLATING.de.md", "ru": "tools/TRANSLATING.ru.md"}
CONTRIBUTING = {"en": "CONTRIBUTING.md"}
for _l in LANGS[1:]:
    CONTRIBUTING[_l] = "docs/CONTRIBUTING.%s.md" % _l
README = {"en": "README.md", "es": "README.es.md", "pt-BR": "README.pt-BR.md", "fr": "README.fr.md", "de": "README.de.md", "ru": "README.ru.md"}
COINS = [("BTC", "bc1q527vjd3vcj6vwg6dg8g0dengh6hd9944l73jqe"),
         ("ETH", "0xC1162610b1A6b8532f4233Db598F624D8EF4d2C4"),
         ("USDT", "0xC1162610b1A6b8532f4233Db598F624D8EF4d2C4")]


def e(t):
    return html.escape(t, quote=True)


def prefix(lang):
    return "" if lang == "en" else "../"


def page_url(lang):
    return BASE if lang == "en" else BASE + lang + "/"


def build_assets():
    from PIL import Image
    shots = os.path.join(OUT, "assets", "shots")
    brand = os.path.join(OUT, "assets", "brand")
    os.makedirs(shots, exist_ok=True)
    os.makedirs(brand, exist_ok=True)
    for lang in LANGS:
        for name in SHOTS:
            src = os.path.join(ROOT, "assets", "screenshots", "%s%s.png" % (name, SUFFIX[lang]))
            dst = os.path.join(shots, "%s%s.webp" % (name, SUFFIX[lang]))
            Image.open(src).convert("RGB").save(dst, "WEBP", quality=86, method=6)
        shutil.copyfile(os.path.join(ROOT, "assets", "brand", "turbix-banner%s.png" % SUFFIX[lang]),
                        os.path.join(brand, "turbix-banner%s.png" % SUFFIX[lang]))
    mark = Image.open(os.path.join(ROOT, "assets", "brand", "turbix-mark-512.png")).convert("RGBA")
    mark.resize((192, 192), Image.LANCZOS).save(os.path.join(brand, "mark-192.png"), optimize=True)
    mark.resize((64, 64), Image.LANCZOS).save(os.path.join(brand, "mark-64.png"), optimize=True)
    mark.resize((32, 32), Image.LANCZOS).save(os.path.join(brand, "favicon-32.png"), optimize=True)


def head(lang, t):
    p = prefix(lang)
    url = page_url(lang)
    out = []
    out.append('<!doctype html>')
    out.append('<html lang="%s">' % HTML_LANG[lang])
    out.append('<head>')
    out.append('<meta charset="utf-8">')
    out.append('<meta name="viewport" content="width=device-width, initial-scale=1">')
    out.append('<title>%s</title>' % e(t["title"]))
    out.append('<meta name="description" content="%s">' % e(t["desc"]))
    out.append('<meta name="color-scheme" content="light dark">')
    out.append('<meta name="theme-color" content="#12140F">')
    out.append('<link rel="canonical" href="%s">' % url)
    for l in LANGS:
        out.append('<link rel="alternate" hreflang="%s" href="%s">' % (HTML_LANG[l], page_url(l)))
    out.append('<link rel="alternate" hreflang="x-default" href="%s">' % BASE)
    out.append('<link rel="icon" type="image/png" sizes="32x32" href="%sassets/brand/favicon-32.png">' % p)
    out.append('<link rel="apple-touch-icon" href="%sassets/brand/mark-192.png">' % p)
    out.append('<meta property="og:type" content="website">')
    out.append('<meta property="og:site_name" content="TURBiX">')
    out.append('<meta property="og:title" content="%s">' % e(t["title"]))
    out.append('<meta property="og:description" content="%s">' % e(t["desc"]))
    out.append('<meta property="og:url" content="%s">' % url)
    out.append('<meta property="og:locale" content="%s">' % OG_LOCALE[lang])
    out.append('<meta property="og:image" content="%sassets/brand/turbix-banner%s.png">' % (BASE, SUFFIX[lang]))
    out.append('<meta name="twitter:card" content="summary_large_image">')
    out.append('<link rel="stylesheet" href="%sassets/site.css">' % p)
    out.append('<script type="application/ld+json">')
    out.append('{"@context":"https://schema.org","@type":"SoftwareApplication","name":"TURBiX","applicationCategory":"UtilitiesApplication",'
               '"operatingSystem":"Windows 10, Windows 11","description":%s,"url":"%s","license":"https://opensource.org/licenses/MIT",'
               '"offers":{"@type":"Offer","price":"0","priceCurrency":"USD"},"downloadUrl":"%s/releases/latest",'
               '"codeRepository":"%s","inLanguage":["en","es","pt-BR","fr","de","ru"]}'
               % (__import__("json").dumps(t["desc"], ensure_ascii=False), url, REPO, REPO))
    out.append('</script>')
    out.append('</head>')
    return "\n".join(out)


def header(lang, t):
    p = prefix(lang)
    langs = []
    for l in LANGS:
        href = ("index.html" if l == "en" else "%s/index.html" % l) if lang == "en" else ("../index.html" if l == "en" else "../%s/index.html" % l)
        if l == lang:
            href = "index.html" if True else href
            langs.append('<li><a href="%s" lang="%s" aria-current="true">%s</a></li>' % (href, HTML_LANG[l], e(NATIVE[l])))
        else:
            langs.append('<li><a href="%s" lang="%s" hreflang="%s">%s</a></li>' % (href, HTML_LANG[l], HTML_LANG[l], e(NATIVE[l])))
    return """<a class="skip" href="#main">%s</a>
<header class="top">
  <div class="wrap bar">
    <a class="brand" href="index.html" aria-label="TURBiX"><img src="%sassets/brand/mark-64.png" width="32" height="32" alt=""><span>TURBiX</span></a>
    <nav class="links" aria-label="Main">
      <a href="#features">%s</a><a href="#safety">%s</a><a href="#download">%s</a><a href="#faq">%s</a><a href="%s">%s</a>
    </nav>
    <details class="lang">
      <summary aria-label="%s"><svg width="16" height="16" viewBox="0 0 16 16" aria-hidden="true"><circle cx="8" cy="8" r="6.5" fill="none" stroke="currentColor" stroke-width="1.3"/><path d="M1.5 8h13M8 1.5c2.2 2 2.2 11 0 13M8 1.5c-2.2 2-2.2 11 0 13" fill="none" stroke="currentColor" stroke-width="1.3"/></svg><span>%s</span></summary>
      <ul>%s</ul>
    </details>
  </div>
</header>""" % (e(t["skip"]), p, e(t["nav_features"]), e(t["nav_safety"]), e(t["nav_download"]), e(t["nav_faq"]),
                REPO, e(t["nav_github"]), e(t["lang_label"]), e(NATIVE[lang]), "".join(langs))


def img(lang, name, alt, eager=False):
    p = prefix(lang)
    return '<img src="%sassets/shots/%s%s.webp" width="1200" height="800" alt="%s"%s>' % (
        p, name, SUFFIX[lang], e(alt), ' fetchpriority="high"' if eager else ' loading="lazy" decoding="async"')


def body(lang, t):
    setup = REPO + "/releases/latest/download/TURBiX-Setup.exe"
    exe = REPO + "/releases/latest/download/TURBiX.exe"
    h = []
    h.append('<main id="main">')
    # Hero
    h.append("""<section class="hero">
  <div class="wrap hero-grid">
    <div class="hero-copy">
      <h1>%s</h1>
      <p class="lead">%s</p>
      <p class="cta"><a class="btn primary" href="%s">%s</a><a class="btn" href="%s">%s</a></p>
      <ul class="meta"><li>%s</li><li>%s</li><li>%s</li></ul>
    </div>
    <figure class="hero-shot">%s</figure>
  </div>
</section>""" % (e(t["h1"]), e(t["lead"]), setup, e(t["cta_installer"]), exe, e(t["cta_portable"]),
                 e(t["meta"][0]), e(t["meta"][1]), e(t["meta"][2]), img(lang, "privacy", t["shot_hero"], True)))
    # Facts
    h.append('<section class="facts" aria-label="%s"><div class="wrap"><dl>' % e(t["nav_safety"]))
    for title, text in t["facts"]:
        h.append('<div><dt>%s</dt><dd>%s</dd></div>' % (e(title), e(text)))
    h.append('</dl></div></section>')
    # Features
    h.append('<section id="features" class="features"><div class="wrap"><h2>%s</h2>' % e(t["features_h2"]))
    for i, (title, text, bullets) in enumerate(t["features"]):
        h.append('<article class="row%s"><div class="row-text"><h3>%s</h3><p>%s</p><ul class="ticks">%s</ul></div><figure>%s</figure></article>' % (
            " rev" if i % 2 else "", e(title), e(text), "".join("<li>%s</li>" % e(b) for b in bullets),
            img(lang, SHOTS[i], title)))
    h.append('<p class="also">%s</p></div></section>' % e(t["also"]))
    # Safety
    h.append('<section id="safety" class="safety"><div class="wrap safety-grid"><div><h2>%s</h2><p class="lead">%s</p><ul class="left-alone">%s</ul></div>' % (
        e(t["safety_h2"]), e(t["safety_lead"]), "".join("<li>%s</li>" % e(x) for x in t["never"])))
    h.append('<div><h3>%s</h3><ol class="steps">%s</ol><p class="note">%s</p></div></div></section>' % (
        e(t["undo_h"]), "".join("<li>%s</li>" % e(x) for x in t["undo"]), e(t["undo_note"])))
    # Download
    h.append('<section id="download" class="download"><div class="wrap"><h2>%s</h2><p class="lead">%s</p>' % (e(t["dl_h2"]), e(t["dl_lead"])))
    h.append('<div class="choices"><div><h3>%s</h3><p>%s</p><p><a class="btn primary" href="%s">TURBiX-Setup.exe</a></p></div>'
             '<div><h3>%s</h3><p>%s</p><p><a class="btn" href="%s">TURBiX.exe</a></p></div></div>' % (
                 e(t["installer"][0]), e(t["installer"][1]), setup, e(t["portable"][0]), e(t["portable"][1]), exe))
    h.append('<div class="silent"><h3>%s</h3><pre><code>TURBiX-Setup.exe --silent\nTURBiX.exe --uninstall --silent</code></pre><p>%s</p></div>' % (
        e(t["silent_h"]), e(t["silent_text"])))
    h.append('<div class="notes">%s</div>' % "".join("<div><h3>%s</h3><p>%s</p></div>" % (e(a), e(b)) for a, b in t["notes"]))
    h.append('<p><a class="textlink" href="%s/releases">%s</a></p></div></section>' % (REPO, e(t["all_releases"])))
    # FAQ
    h.append('<section id="faq" class="faq"><div class="wrap faq-grid"><h2>%s</h2><div>%s</div></div></section>' % (
        e(t["faq_h2"]), "".join("<details><summary>%s</summary><p>%s</p></details>" % (e(q), e(a)) for q, a in t["faq"])))
    # Contribute
    links = [REPO + "/issues/new/choose", REPO + "/blob/main/" + TRANSLATING[lang],
             REPO + "/blob/main/" + CONTRIBUTING[lang], REPO + "/discussions"]
    h.append('<section id="contribute" class="contribute"><div class="wrap contrib-grid"><div><h2>%s</h2><p class="lead">%s</p></div><div><ul class="ways">%s</ul>' % (
        e(t["contrib_h2"]), e(t["contrib_text"]),
        "".join('<li><a href="%s"><b>%s</b><span>%s</span></a></li>' % (links[i], e(t["contrib"][i][0]), e(t["contrib"][i][1])) for i in range(4))))
    rows = []
    for i, (coin, addr) in enumerate(COINS):
        rows.append('<tr><th scope="row">%s</th><td>%s</td><td><code>%s</code></td><td><button type="button" class="copy" data-copy="%s" data-done="%s">%s</button></td></tr>' % (
            coin, e(t["coin_net"][i]), addr, addr, e(t["copied"]), e(t["copy"])))
    h.append('<details class="donate"><summary>%s</summary><p>%s</p><div class="table-wrap"><table><tbody>%s</tbody></table></div><p class="warn">%s</p></details>' % (
        e(t["donate_h"]), e(t["donate_text"]), "".join(rows), e(t["donate_warn"])))
    h.append('</div></div></section>')
    h.append('</main>')
    # Footer
    h.append("""<footer class="foot"><div class="wrap">
  <p><a href="%s">GitHub</a> <a href="%s/blob/main/%s">%s</a> <a href="%s/blob/main/src/Turbix/Changelog/%s.md">%s</a> <a href="%s/blob/main/LICENSE">%s</a></p>
  <p class="small">%s</p>
</div></footer>
<script src="%sassets/site.js" defer></script>
</body>
</html>""" % (REPO, REPO, README[lang], e(t["foot_readme"]), REPO,
              "en" if lang == "en" else ("pt" if lang == "pt-BR" else lang), e(t["foot_changes"]),
              REPO, e(t["foot_license"].rstrip(".")), e(t["foot_tm"]), prefix(lang)))
    return "\n".join(h)


def main():
    if os.path.isdir(OUT):
        for name in os.listdir(OUT):
            if name.startswith("CONTRIBUTING."):
                continue
            path = os.path.join(OUT, name)
            if name == "assets" and os.path.isdir(path):
                # css/js are hand-written; only regenerate images
                for sub in ("shots", "brand"):
                    shutil.rmtree(os.path.join(path, sub), ignore_errors=True)
                continue
            if os.path.isdir(path) and name in LANGS:
                shutil.rmtree(path)
            elif os.path.isfile(path) and name in ("index.html", "404.html", "sitemap.xml", "robots.txt", ".nojekyll"):
                os.remove(path)
    os.makedirs(os.path.join(OUT, "assets"), exist_ok=True)
    build_assets()
    for lang in LANGS:
        t = S[lang]
        d = OUT if lang == "en" else os.path.join(OUT, lang)
        os.makedirs(d, exist_ok=True)
        page = head(lang, t) + "\n<body>\n" + header(lang, t) + "\n" + body(lang, t)
        with open(os.path.join(d, "index.html"), "w", encoding="utf-8", newline="\n") as f:
            f.write(page)
    # 404, sitemap, robots, .nojekyll
    with open(os.path.join(OUT, "404.html"), "w", encoding="utf-8", newline="\n") as f:
        f.write('<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">'
                '<title>TURBiX</title><link rel="stylesheet" href="/TURBiX/assets/site.css"></head><body class="nf">'
                '<main><h1>404</h1><p>This page does not exist.</p><p><a class="btn primary" href="/TURBiX/">TURBiX</a></p></main></body></html>')
    sm = ['<?xml version="1.0" encoding="UTF-8"?>',
          '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9" xmlns:xhtml="http://www.w3.org/1999/xhtml">']
    for lang in LANGS:
        sm.append("<url><loc>%s</loc>" % page_url(lang))
        for l in LANGS:
            sm.append('<xhtml:link rel="alternate" hreflang="%s" href="%s"/>' % (HTML_LANG[l], page_url(l)))
        sm.append('<xhtml:link rel="alternate" hreflang="x-default" href="%s"/></url>' % BASE)
    sm.append("</urlset>")
    with open(os.path.join(OUT, "sitemap.xml"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(sm))
    with open(os.path.join(OUT, "robots.txt"), "w", encoding="utf-8", newline="\n") as f:
        f.write("User-agent: *\nAllow: /\nSitemap: %ssitemap.xml\n" % BASE)
    open(os.path.join(OUT, ".nojekyll"), "w").close()
    print("site built in", OUT)


if __name__ == "__main__":
    main()
