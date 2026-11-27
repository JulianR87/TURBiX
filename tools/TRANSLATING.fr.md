[English](TRANSLATING.md) · [Español](TRADUCIR.md) · [Português (Brasil)](TRANSLATING.pt-BR.md) · **Français** · [Deutsch](TRANSLATING.de.md) · [Русский](TRANSLATING.ru.md)

# Comment TURBiX est traduit

TURBiX s’affiche dans la langue de Windows : espagnol (`es`), anglais (`en`), portugais du Brésil (`pt`), français (`fr`),
allemand (`de`) et russe (`ru`). Toute autre langue passe à l’anglais. Pour essayer une autre langue : `TURBiX.exe --lang de`
(ou la valeur `Language` dans `HKCU\Software\TURBiX`).

## Où se trouvent les textes

- `src/Turbix/Strings/<domaine>.<langue>.txt`, incorporés à l’exe (pas de DLL satellites : il reste un fichier unique).
- Une ligne par texte : `clé = texte`. `\n` = saut de ligne, ` ` = espace insécable, `#` = commentaire.
- Chaque domaine a ses 6 fichiers avec **exactement les mêmes clés**. Une clé ne se répète pas dans deux domaines.
- `common.*.txt` contient ce qui est partagé (noms des sections, boutons, états, unités) : **réutilisez-le**, ne le dupliquez pas.
- Clés : `domaine.chose.détail` en camelCase. Ex. : `cleanup.item.tempUser.name`, `cleanup.item.tempUser.help`.

## Dans le code

```csharp
using Turbix.Localization;

L.T("apps.title")                         // texte
L.F("apps.removed", name)                 // avec des valeurs {0}, {1}…
L.P("apps.count", n)                      // pluriel : {0} est n déjà mis en forme ; {1}… sont des extras : L.P("k", n, extra)
```

```xml
xmlns:l="clr-namespace:Turbix.Localization"
<TextBlock Text="{l:T apps.title}"/>       <!-- aussi Content, Tag, ToolTip, HelpTip.Text… -->
```

- Pluriels : `clé.one` et `clé.other` ; en russe `clé.one`, `clé.few`, `clé.many` (1 файл, 2 файла, 5 файлов ;
  21 → one, 22 → few, 11–14 → many). En français et en portugais, `one` vaut pour 0 et 1. Jamais « fichier(s) ».
- Ne construisez pas de phrases en collant des morceaux (« Suppression de  » + n + «  applications ») : une seule clé
  avec des espaces réservés. L’ordre des mots change d’une langue à l’autre.
- Dates et nombres avec `L.Culture` ; tailles avec `FileCleaner.FormatSize` (qui traduit déjà les unités).
- Les `Id`, les `Tag` de logique, les `CommandParameter`, les chemins du Registre, les noms de services et de fichiers **ne se traduisent pas**.
- Les messages d’erreur fournis par Windows (`ex.Message`) arrivent déjà dans la langue du système : on les insère avec `{0}`.
- L’autotest (`Testing/SelfTest.cs`) ne se traduit pas : aucun utilisateur ne le voit.

## Comment écrire

- **Naturel, comme l’écrirait une personne** qui connaît Windows et écrit pour des gens ordinaires. Phrases courtes et
  mots simples. Pas de ton publicitaire, pas d’exclamations, pas de « Optimisez au maximum !», ni de formules creuses typiques de l’IA
  (« Découvrez », « puissant », « sans effort », « en un seul clic », « seamless », « unleash »…). Ne traduisez pas mot à mot :
  réécrivez comme le dirait un locuteur natif.
- Forme d’adresse : `es` tú · `en` you · `pt` você · `fr` vous · `de` Sie · `ru` вы (minuscule).
- Guillemets : `es` «…» · `en` “…” · `pt` “…” · `fr` « … » (avec ` ` à l’intérieur) · `de` „…“ · `ru` «…».
- Utilisez les noms **officiels de Windows dans cette langue** (ceux de Windows lui-même) : Administrador de tareas / Task
  Manager / Gerenciador de Tarefas / Gestionnaire des tâches / Task-Manager / Диспетчер задач ; Configuración / Settings /
  Configurações / Paramètres / Einstellungen / Параметры ; Hibernar / Hibernate / Hibernar / Mettre en veille prolongée /
  Ruhezustand / Гибернация ; Explorador de archivos / File Explorer / Explorador de Arquivos / Explorateur de fichiers /
  Datei-Explorer / Проводник. Même chose pour les noms des applications du Microsoft Store (Noticias / News / Notícias /
  Actualités / Nachrichten / Новости…). De même pour les marques et les noms propres (TURBiX, Microsoft Store, Edge, OneDrive, Xbox).
- Les sections de TURBiX se nomment comme dans `common` (`nav.*`) : si le texte espagnol dit «desde Restaurar», en anglais c’est
  “from Restore”, en allemand „unter Wiederherstellen“, etc.
- Règles de contenu valables dans toutes les langues : « N’utilise pas Internet », jamais rien qui évoque « hors connexion/offline » ;
  libérer de la RAM ou nettoyer le Registre **n’accélère pas** un PC qui en a largement assez ; « Bon » dans Santé signifie « aucun signe de
  défaillance », pas une garantie.
- Les aides « ? » restent brèves.
- L’allemand et le russe sont environ 30 % plus longs : en XAML, pas de largeurs fixes pour le texte (`Width` sur les boutons, les onglets ou
  les colonnes contenant du texte) : utilisez `MinWidth` et `Padding`, ou `TextWrapping="Wrap"` là où il peut y avoir deux lignes.

## Vérifier

```
python tools/check_strings.py                      # tout
python tools/check_strings.py Views/AppsView.xaml  # seulement ces fichiers
```

Il signale les clés manquantes dans une langue, les espaces réservés `{0}` différents, les pluriels incomplets, les clés répétées et
le texte en espagnol resté écrit dans le code.
