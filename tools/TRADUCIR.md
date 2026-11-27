[English](TRANSLATING.md) · **Español** · [Português (Brasil)](TRANSLATING.pt-BR.md) · [Français](TRANSLATING.fr.md) · [Deutsch](TRANSLATING.de.md) · [Русский](TRANSLATING.ru.md)

# Cómo se traduce TURBiX

TURBiX se muestra en el idioma de Windows: español (`es`), inglés (`en`), portugués de Brasil (`pt`), francés (`fr`),
alemán (`de`) y ruso (`ru`). Cualquier otro idioma usa inglés. Para probar otro idioma: `TURBiX.exe --lang de`
(o el valor `Language` en `HKCU\Software\TURBiX`).

## Dónde viven los textos

- `src/Turbix/Strings/<área>.<idioma>.txt`, incrustados en el exe (sin DLL satélite: sigue siendo un único archivo).
- Una línea por texto: `clave = texto`. `\n` = salto de línea, ` ` = espacio duro, `#` = comentario.
- Cada área tiene sus 6 archivos con **exactamente las mismas claves**. Una clave no se repite en dos áreas.
- `common.*.txt` tiene lo compartido (nombres de secciones, botones, estados, unidades): **reutilizarlo**, no duplicarlo.
- Claves: `área.cosa.detalle` en camelCase. Ej.: `cleanup.item.tempUser.name`, `cleanup.item.tempUser.help`.

## En el código

```csharp
using Turbix.Localization;

L.T("apps.title")                         // texto
L.F("apps.removed", name)                 // con valores {0}, {1}…
L.P("apps.count", n)                      // plural: {0} es n ya formateado; {1}… son extras: L.P("k", n, extra)
```

```xml
xmlns:l="clr-namespace:Turbix.Localization"
<TextBlock Text="{l:T apps.title}"/>       <!-- también Content, Tag, ToolTip, HelpTip.Text… -->
```

- Plurales: `clave.one` y `clave.other`; en ruso `clave.one`, `clave.few`, `clave.many` (1 файл, 2 файла, 5 файлов;
  21 → one, 22 → few, 11–14 → many). En francés y portugués `one` vale para 0 y 1. Nunca «archivo(s)».
- No armar frases pegando trozos («Se quitaron » + n + « apps»): una clave con marcadores. El orden de las palabras
  cambia entre idiomas.
- Fechas y números con `L.Culture`; tamaños con `FileCleaner.FormatSize` (ya traduce las unidades).
- Los `Id`, `Tag` de lógica, `CommandParameter`, rutas del registro, nombres de servicios y de archivos **no se traducen**.
- Los mensajes de error que da Windows (`ex.Message`) ya vienen en el idioma del sistema: se insertan con `{0}`.
- La autoprueba (`Testing/SelfTest.cs`) no se traduce: no la ve ningún usuario.

## Cómo escribir

- **Natural, como lo escribiría una persona** que conoce Windows y escribe para gente normal. Frases cortas y palabras
  simples. Nada de tono publicitario, exclamaciones, «¡Optimiza al máximo!», ni frases de relleno típicas de IA
  («Descubre», «potente», «sin esfuerzo», «en un solo clic», «seamless», «unleash»…). No traducir palabra por palabra:
  reescribir como lo diría un nativo.
- Tratamiento: `es` tú · `en` you · `pt` você · `fr` vous · `de` Sie · `ru` вы (minúscula).
- Comillas: `es` «…» · `en` “…” · `pt` “…” · `fr` « … » (con ` ` dentro) · `de` „…“ · `ru` «…».
- Usar los nombres **oficiales de Windows en ese idioma** (los del propio Windows): Administrador de tareas / Task
  Manager / Gerenciador de Tarefas / Gestionnaire des tâches / Task-Manager / Диспетчер задач; Configuración / Settings /
  Configurações / Paramètres / Einstellungen / Параметры; Hibernar / Hibernate / Hibernar / Mettre en veille prolongée /
  Ruhezustand / Гибернация; Explorador de archivos / File Explorer / Explorador de Arquivos / Explorateur de fichiers /
  Datei-Explorer / Проводник. Lo mismo con los nombres de las apps de la Tienda (Noticias / News / Notícias /
  Actualités / Nachrichten / Новости…). Marcas y nombres propios (TURBiX, Microsoft Store, Edge, OneDrive, Xbox) igual.
- Las secciones de TURBiX se nombran como en `common` (`nav.*`): si un texto dice «desde Restaurar», en inglés es
  “from Restore”, en alemán „unter Wiederherstellen“, etc.
- Reglas de contenido que valen en todos los idiomas: «No usa Internet», nunca algo que suene a «sin conexión/offline»;
  liberar RAM o limpiar el registro **no acelera** un equipo al que le sobra; «Bien» en Salud significa «sin señales de
  fallo», no una garantía.
- Las ayudas «?» siguen siendo breves.
- Alemán y ruso son ~30 % más largos: en XAML, nada de anchos fijos para texto (`Width` en botones, pestañas o
  columnas con texto): usar `MinWidth` y `Padding`, o `TextWrapping="Wrap"` donde pueda haber dos líneas.

## Comprobar

```
python tools/check_strings.py                      # todo
python tools/check_strings.py Views/AppsView.xaml  # solo esos archivos
```

Avisa de claves que faltan en algún idioma, marcadores `{0}` distintos, plurales incompletos, claves repetidas y
texto en español que quedó escrito en el código.
