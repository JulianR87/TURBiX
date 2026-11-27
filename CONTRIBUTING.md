**English** · [Español](docs/CONTRIBUTING.es.md) · [Português (Brasil)](docs/CONTRIBUTING.pt-BR.md) · [Français](docs/CONTRIBUTING.fr.md) · [Deutsch](docs/CONTRIBUTING.de.md) · [Русский](docs/CONTRIBUTING.ru.md)

# Contributing to TURBiX

Thanks for wanting to help. TURBiX is a free, public project kept going in spare time, and every contribution counts. You don't need to know how to code.

## Ways to help

- **Report a bug.** Open an [issue](https://github.com/JulianR87/TURBiX/issues/new/choose) and say which Windows version and TURBiX version you use, what you did and what you expected. The activity log (**Activity log** page, *Copy* button) helps a lot.
- **Suggest an improvement** in [Issues](https://github.com/JulianR87/TURBiX/issues) or ask anything in [Discussions](https://github.com/JulianR87/TURBiX/discussions).
- **Improve a translation.** The texts are plain text files in `src/Turbix/Strings`. The guide is [`tools/TRANSLATING.md`](tools/TRANSLATING.md).
- **Send code** with a pull request. Issues labelled `good first issue` are a good place to start.
- **Report an antivirus false positive.** Tell us the antivirus and the detection name so we can report it to the vendor.

## Building

You need the [.NET SDK](https://dotnet.microsoft.com/download) (6 or later). Run `build.cmd`, the result is `dist\TURBiX.exe` and `dist\TURBiX-Setup.exe`.

## Rules for code

1. **It has to work on any PC with a clean Windows 10 (1607+) or 11.** No downloads, no extra packages, no PowerShell inside the program, no Internet access.
2. **Everything can be undone.** A tweak saves the original value first and can be reverted from **Restore**.
3. **Security is never touched.** Nothing that turns off Windows Update, Microsoft Defender, SmartScreen or the firewall, and no “tricks” that break things.
4. **No hard-coded text.** Every visible text goes in `Strings`, in all six languages. `python tools/check_strings.py` must print `OK`.
5. **Every option has its “?” button** with a short, honest explanation. No promises the program can't keep.
6. **Test before you send.** Use a virtual machine for anything that changes Windows, and say in the pull request what you tested.

Keep each pull request to one change. English and Spanish are both fine for issues and pull requests.

## Security problems

Please don't open a public issue for a vulnerability. Use **Security › Report a vulnerability** on the repository page so it can be fixed before it's made public.
