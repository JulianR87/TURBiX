[English](../CONTRIBUTING.md) · [Español](CONTRIBUTING.es.md) · [Português (Brasil)](CONTRIBUTING.pt-BR.md) · **Français** · [Deutsch](CONTRIBUTING.de.md) · [Русский](CONTRIBUTING.ru.md)

# Contribuer à TURBiX

Merci de vouloir aider. TURBiX est un projet public et gratuit, entretenu sur du temps libre, et chaque contribution compte. Pas besoin de savoir programmer.

## Comment aider

- **Signaler un bug.** Ouvrez une [issue](https://github.com/JulianR87/TURBiX/issues/new/choose) en indiquant votre version de Windows et de TURBiX, ce que vous avez fait et ce que vous attendiez. Le journal d’activité (page **Journal d’activité**, bouton *Copier*) aide beaucoup.
- **Proposer une amélioration** dans les [Issues](https://github.com/JulianR87/TURBiX/issues) ou poser une question dans les [Discussions](https://github.com/JulianR87/TURBiX/discussions).
- **Améliorer une traduction.** Les textes sont de simples fichiers texte dans `src/Turbix/Strings`. Le guide est [`tools/TRANSLATING.fr.md`](../tools/TRANSLATING.fr.md).
- **Envoyer du code** avec une pull request. Les issues portant l’étiquette `good first issue` sont un bon point de départ.
- **Signaler un faux positif d’antivirus.** Indiquez l’antivirus et le nom de la détection, pour que nous le signalions à l’éditeur.

## Compiler

Il vous faut le [SDK .NET](https://dotnet.microsoft.com/download) (6 ou version ultérieure). Lancez `build.cmd`, vous obtenez `dist\TURBiX.exe` et `dist\TURBiX-Setup.exe`.

## Règles pour le code

1. **Cela doit fonctionner sur n’importe quel PC avec un Windows 10 (1607+) ou 11 fraîchement installé.** Aucun téléchargement, aucun paquet supplémentaire, pas de PowerShell dans le programme, pas d’accès à Internet.
2. **Tout peut être annulé.** Un réglage enregistre d’abord la valeur d’origine et se rétablit depuis **Restaurer**.
3. **On ne touche pas à la sécurité.** Rien qui désactive Windows Update, Microsoft Defender, SmartScreen ou le pare-feu, et pas d’« astuces » qui cassent des choses.
4. **Aucun texte écrit en dur dans le code.** Tout texte visible va dans `Strings`, dans les six langues. `python tools/check_strings.py` doit afficher `OK`.
5. **Chaque option a son bouton « ? »** avec une explication courte et honnête. Pas de promesses que le programme ne peut pas tenir.
6. **Testez avant d’envoyer.** Utilisez une machine virtuelle pour tout ce qui modifie Windows et indiquez dans la pull request ce que vous avez testé.

Une modification par pull request. Les issues et pull requests peuvent être rédigées en anglais ou en espagnol.

## Problèmes de sécurité

N’ouvrez pas d’issue publique pour une vulnérabilité. Utilisez **Security › Report a vulnerability** sur la page du dépôt, afin qu’elle soit corrigée avant d’être rendue publique.
