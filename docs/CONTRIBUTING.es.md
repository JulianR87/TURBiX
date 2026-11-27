[English](../CONTRIBUTING.md) · **Español** · [Português (Brasil)](CONTRIBUTING.pt-BR.md) · [Français](CONTRIBUTING.fr.md) · [Deutsch](CONTRIBUTING.de.md) · [Русский](CONTRIBUTING.ru.md)

# Colaborar con TURBiX

Gracias por querer ayudar. TURBiX es un proyecto público y gratuito que se mantiene con tiempo libre, y toda colaboración suma. No hace falta saber programar.

## Formas de ayudar

- **Avisar de un error.** Abre un [issue](https://github.com/JulianR87/TURBiX/issues/new/choose) y cuenta qué versión de Windows y de TURBiX usas, qué hiciste y qué esperabas. El registro de actividad (página **Registro de actividad**, botón *Copiar*) ayuda mucho.
- **Proponer una mejora** en [Issues](https://github.com/JulianR87/TURBiX/issues) o preguntar lo que sea en [Discussions](https://github.com/JulianR87/TURBiX/discussions).
- **Mejorar una traducción.** Los textos son archivos de texto sencillos en `src/Turbix/Strings`. La guía es [`tools/TRADUCIR.md`](../tools/TRADUCIR.md).
- **Enviar código** con un pull request. Los issues con la etiqueta `good first issue` son un buen punto de partida.
- **Avisar de un falso positivo de antivirus.** Dinos qué antivirus y qué nombre de detección, para reportarlo al fabricante.

## Compilar

Necesitas el [SDK de .NET](https://dotnet.microsoft.com/download) (6 o posterior). Ejecuta `build.cmd`; el resultado es `dist\TURBiX.exe` y `dist\TURBiX-Setup.exe`.

## Reglas para el código

1. **Tiene que funcionar en cualquier PC con Windows 10 (1607+) u 11 recién instalado.** Sin descargas, sin paquetes extra, sin PowerShell dentro del programa y sin usar Internet.
2. **Todo se puede deshacer.** Un ajuste guarda antes el valor original y se revierte desde **Restaurar**.
3. **La seguridad no se toca.** Nada que desactive Windows Update, Microsoft Defender, SmartScreen o el firewall, ni «trucos» que rompen cosas.
4. **Ningún texto escrito a mano en el código.** Todo texto visible va en `Strings`, en los seis idiomas. `python tools/check_strings.py` debe dar `OK`.
5. **Cada opción lleva su botón «?»** con una explicación breve y honesta. Sin promesas que el programa no puede cumplir.
6. **Prueba antes de enviar.** Usa una máquina virtual para todo lo que cambie Windows y cuenta en el pull request qué probaste.

Un cambio por pull request. Los issues y pull requests pueden estar en español o en inglés.

## Problemas de seguridad

No abras un issue público para una vulnerabilidad. Usa **Security › Report a vulnerability** en la página del repositorio, para poder corregirla antes de hacerla pública.
