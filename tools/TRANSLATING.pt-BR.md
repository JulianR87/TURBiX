[English](TRANSLATING.md) · [Español](TRADUCIR.md) · **Português (Brasil)** · [Français](TRANSLATING.fr.md) · [Deutsch](TRANSLATING.de.md) · [Русский](TRANSLATING.ru.md)

# Como o TURBiX é traduzido

O TURBiX aparece no idioma do Windows: espanhol (`es`), inglês (`en`), português do Brasil (`pt`), francês (`fr`),
alemão (`de`) e russo (`ru`). Qualquer outro idioma usa o inglês. Para testar outro idioma: `TURBiX.exe --lang de`
(ou o valor `Language` em `HKCU\Software\TURBiX`).

## Onde ficam os textos

- `src/Turbix/Strings/<área>.<idioma>.txt`, incorporados ao exe (sem DLLs satélite: continua sendo um único arquivo).
- Uma linha por texto: `chave = texto`. `\n` = quebra de linha, ` ` = espaço não separável, `#` = comentário.
- Cada área tem seus 6 arquivos com **exatamente as mesmas chaves**. Uma chave não se repete em duas áreas.
- `common.*.txt` tem o que é compartilhado (nomes de seções, botões, estados, unidades): **reutilize**, não duplique.
- Chaves: `área.coisa.detalhe` em camelCase. Ex.: `cleanup.item.tempUser.name`, `cleanup.item.tempUser.help`.

## No código

```csharp
using Turbix.Localization;

L.T("apps.title")                         // texto
L.F("apps.removed", name)                 // com valores {0}, {1}…
L.P("apps.count", n)                      // plural: {0} é n já formatado; {1}… são extras: L.P("k", n, extra)
```

```xml
xmlns:l="clr-namespace:Turbix.Localization"
<TextBlock Text="{l:T apps.title}"/>       <!-- também Content, Tag, ToolTip, HelpTip.Text… -->
```

- Plurais: `chave.one` e `chave.other`; em russo `chave.one`, `chave.few`, `chave.many` (1 файл, 2 файла, 5 файлов;
  21 → one, 22 → few, 11–14 → many). Em francês e português, `one` vale para 0 e 1. Nunca “arquivo(s)”.
- Não monte frases juntando pedaços (“Foram removidos ” + n + “ aplicativos”): use uma chave com marcadores. A ordem
  das palavras muda de um idioma para outro.
- Datas e números com `L.Culture`; tamanhos com `FileCleaner.FormatSize` (já traduz as unidades).
- Os `Id`, os `Tag` de lógica, `CommandParameter`, caminhos do Registro, nomes de serviços e de arquivos **não são traduzidos**.
- As mensagens de erro que o Windows fornece (`ex.Message`) já vêm no idioma do sistema: são inseridas com `{0}`.
- O autoteste (`Testing/SelfTest.cs`) não é traduzido: nenhum usuário o vê.

## Como escrever

- **Natural, como uma pessoa escreveria**: alguém que conhece o Windows e escreve para gente comum. Frases curtas e
  palavras simples. Nada de tom publicitário, exclamações, “Otimize ao máximo!” nem frases de enchimento típicas de IA
  (“Descubra”, “poderoso”, “sem esforço”, “com um só clique”, “seamless”, “unleash”…). Não traduza palavra por palavra:
  reescreva como um nativo diria.
- Tratamento: `es` tú · `en` you · `pt` você · `fr` vous · `de` Sie · `ru` вы (minúscula).
- Aspas: `es` «…» · `en` “…” · `pt` “…” · `fr` « … » (com ` ` dentro) · `de` „…“ · `ru` «…».
- Use os nomes **oficiais do Windows naquele idioma** (os do próprio Windows): Administrador de tareas / Task
  Manager / Gerenciador de Tarefas / Gestionnaire des tâches / Task-Manager / Диспетчер задач; Configuración / Settings /
  Configurações / Paramètres / Einstellungen / Параметры; Hibernar / Hibernate / Hibernar / Mettre en veille prolongée /
  Ruhezustand / Гибернация; Explorador de archivos / File Explorer / Explorador de Arquivos / Explorateur de fichiers /
  Datei-Explorer / Проводник. O mesmo vale para os nomes dos aplicativos da Microsoft Store (Noticias / News / Notícias /
  Actualités / Nachrichten / Новости…). Marcas e nomes próprios (TURBiX, Microsoft Store, Edge, OneDrive, Xbox) também.
- As seções do TURBiX são chamadas como em `common` (`nav.*`): se o texto em espanhol diz «desde Restaurar», em inglês é
  “from Restore”, em alemão „unter Wiederherstellen“ etc.
- Regras de conteúdo que valem em todos os idiomas: “Não usa a internet”, nunca algo que soe como “sem conexão/offline”;
  liberar RAM ou limpar o Registro **não acelera** um computador que tem memória de sobra; “Bom” em Saúde significa “sem
  sinais de falha”, não uma garantia.
- As ajudas “?” continuam curtas.
- Alemão e russo são cerca de 30% mais longos: no XAML, nada de larguras fixas para texto (`Width` em botões, guias ou
  colunas com texto): use `MinWidth` e `Padding`, ou `TextWrapping="Wrap"` onde possa haver duas linhas.

## Verificar

```
python tools/check_strings.py                      # tudo
python tools/check_strings.py Views/AppsView.xaml  # só esses arquivos
```

Avisa sobre chaves que faltam em algum idioma, marcadores `{0}` diferentes, plurais incompletos, chaves repetidas e
texto em espanhol que ficou escrito no código.
