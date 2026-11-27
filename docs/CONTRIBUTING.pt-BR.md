[English](../CONTRIBUTING.md) · [Español](CONTRIBUTING.es.md) · **Português (Brasil)** · [Français](CONTRIBUTING.fr.md) · [Deutsch](CONTRIBUTING.de.md) · [Русский](CONTRIBUTING.ru.md)

# Como colaborar com o TURBiX

Obrigado por querer ajudar. O TURBiX é um projeto público e gratuito, mantido no tempo livre, e toda colaboração conta. Não é preciso saber programar.

## Formas de ajudar

- **Avisar sobre um erro.** Abra uma [issue](https://github.com/JulianR87/TURBiX/issues/new/choose) e diga qual versão do Windows e do TURBiX você usa, o que fez e o que esperava. O registro de atividades (página **Registro de atividades**, botão *Copiar*) ajuda bastante.
- **Sugerir uma melhoria** em [Issues](https://github.com/JulianR87/TURBiX/issues) ou tirar qualquer dúvida em [Discussions](https://github.com/JulianR87/TURBiX/discussions).
- **Melhorar uma tradução.** Os textos são arquivos de texto simples em `src/Turbix/Strings`. O guia é [`tools/TRANSLATING.pt-BR.md`](../tools/TRANSLATING.pt-BR.md).
- **Enviar código** com um pull request. As issues com a etiqueta `good first issue` são um bom começo.
- **Avisar sobre um falso positivo de antivírus.** Diga qual antivírus e o nome da detecção, para reportarmos ao fabricante.

## Compilar

Você precisa do [SDK do .NET](https://dotnet.microsoft.com/download) (6 ou posterior). Execute `build.cmd`, o resultado é `dist\TURBiX.exe` e `dist\TURBiX-Setup.exe`.

## Regras para o código

1. **Precisa funcionar em qualquer PC com Windows 10 (1607+) ou 11 recém-instalado.** Sem downloads, sem pacotes extras, sem PowerShell dentro do programa e sem usar a Internet.
2. **Tudo pode ser desfeito.** Um ajuste guarda antes o valor original e pode ser revertido em **Restaurar**.
3. **A segurança não é tocada.** Nada que desative o Windows Update, o Microsoft Defender, o SmartScreen ou o firewall, nem “truques” que quebram coisas.
4. **Nenhum texto escrito direto no código.** Todo texto visível fica em `Strings`, nos seis idiomas. `python tools/check_strings.py` precisa mostrar `OK`.
5. **Toda opção tem seu botão “?”** com uma explicação curta e honesta. Sem promessas que o programa não pode cumprir.
6. **Teste antes de enviar.** Use uma máquina virtual para tudo o que altera o Windows e conte no pull request o que você testou.

Um pull request por mudança. Issues e pull requests podem ser em inglês ou espanhol.

## Problemas de segurança

Não abra uma issue pública para uma vulnerabilidade. Use **Security › Report a vulnerability** na página do repositório, para que ela seja corrigida antes de se tornar pública.
