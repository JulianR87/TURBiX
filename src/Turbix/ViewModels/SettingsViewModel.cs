using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Turbix.Core;
using Turbix.Localization;
using Turbix.Themes;
using Turbix.Views;

namespace Turbix.ViewModels
{
    /// <summary>Una opción del selector de idioma (Code vacío = el idioma de Windows).</summary>
    public sealed class LanguageOptionViewModel : ObservableObject
    {
        private readonly Action<string> _choose;
        private bool _isSelected;

        public LanguageOptionViewModel(string code, string name, Action<string> choose)
        {
            Code = code;
            Name = name;
            _choose = choose;
        }

        public string Code { get; }
        public string Name { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (!Set(ref _isSelected, value)) return;
                if (value) _choose(Code);
            }
        }

        internal void Mark(bool selected) => Set(ref _isSelected, selected, nameof(IsSelected));
    }

    /// <summary>Ajustes generales de Turbix (página «Ajustes»).</summary>
    public sealed partial class MainViewModel
    {
        public const string TrayIconSetting = "TrayIcon";
        public const string CloseToTraySetting = "CloseToTray";
        public const string ConfirmExitSetting = "ConfirmExit";
        public const string TrayHintSetting = "TrayHintShown";

        private bool _showTrayIcon, _closeToTray, _confirmExit;
        private string _languageChoice;

        public List<LanguageOptionViewModel> LanguageOptions { get; private set; }

        /// <summary>Carpeta donde Turbix guarda copias de seguridad y registros.</summary>
        public string DataFolder => AppPaths.Root;

        public ICommand RestartCommand { get; private set; }
        public ICommand OpenDataFolderCommand { get; private set; }
        public ICommand ResetSettingsCommand { get; private set; }

        private void InitSettings()
        {
            _showTrayIcon = Settings.GetBool(TrayIconSetting, true);
            _closeToTray = Settings.GetBool(CloseToTraySetting, true);
            _confirmExit = Settings.GetBool(ConfirmExitSetting, true);
            _languageChoice = Settings.Disabled ? "" : (L.SavedChoice ?? "");

            LanguageOptions = new List<LanguageOptionViewModel>
            {
                new LanguageOptionViewModel("", L.F("settings.language.auto", L.NativeName(L.WindowsLanguage)), ChooseLanguage),
            };
            foreach (var code in new[] { "es", "en", "pt", "fr", "de", "ru" })
                LanguageOptions.Add(new LanguageOptionViewModel(code, L.NativeName(code), ChooseLanguage));
            foreach (var o in LanguageOptions) o.Mark(o.Code == _languageChoice);

            RestartCommand = new RelayCommand(() => App.Restart(), () => !IsBusy);
            OpenDataFolderCommand = new RelayCommand(() => OpenFolder(AppPaths.Root));
            ResetSettingsCommand = new RelayCommand(ResetSettings, () => !IsBusy);
        }

        // ------------------------------------------------------------------ icono y cierre

        /// <summary>Mostrar el icono de Turbix junto al reloj mientras está abierto.</summary>
        public bool ShowTrayIcon
        {
            get => _showTrayIcon;
            set
            {
                if (!Set(ref _showTrayIcon, value)) return;
                Settings.SetBool(TrayIconSetting, value);
                TrayIcon.SetPersistent(value);
                OnPropertyChanged(nameof(CloseKeepsRunning));
                Logger.Info(L.T(value ? "settings.tray.log.on" : "settings.tray.log.off"));
            }
        }

        /// <summary>La X esconde la ventana y Turbix sigue funcionando junto al reloj.</summary>
        public bool CloseToTray
        {
            get => _closeToTray;
            set
            {
                if (!Set(ref _closeToTray, value)) return;
                Settings.SetBool(CloseToTraySetting, value);
                OnPropertyChanged(nameof(CloseKeepsRunning));
            }
        }

        /// <summary>Sin el icono junto al reloj no habría forma de volver a la ventana, así que la X cierra.</summary>
        public bool CloseKeepsRunning => _showTrayIcon && _closeToTray;

        public bool ConfirmExit
        {
            get => _confirmExit;
            set
            {
                if (!Set(ref _confirmExit, value)) return;
                Settings.SetBool(ConfirmExitSetting, value);
            }
        }

        // ------------------------------------------------------------------ idioma

        /// <summary>El idioma elegido no es el que se está mostrando: hace falta volver a abrir Turbix.</summary>
        public bool LanguageNeedsRestart =>
            (string.IsNullOrEmpty(_languageChoice) ? L.WindowsLanguage : _languageChoice) != L.Lang;

        private void ChooseLanguage(string code)
        {
            if (code == _languageChoice) return;
            _languageChoice = code;
            foreach (var o in LanguageOptions) o.Mark(o.Code == code);
            if (!Settings.Disabled) L.SaveChoice(code);
            Logger.Info(L.F("settings.language.log", string.IsNullOrEmpty(code)
                ? L.F("settings.language.auto", L.NativeName(L.WindowsLanguage))
                : L.NativeName(code)));
            OnPropertyChanged(nameof(LanguageNeedsRestart));
        }

        // ------------------------------------------------------------------ datos

        /// <summary>Vuelve todas las opciones de Turbix a como vienen. No toca copias de seguridad ni registros.</summary>
        private void ResetSettings()
        {
            if (!Dialogs.Confirm(L.T("settings.reset.confirm.title"), L.T("settings.reset.confirm.text"),
                                 L.T("settings.reset.confirm.button"))) return;

            ThemeManager.SetMode(ThemeMode.System);
            ChooseLanguage("");
            StartWithWindows = false;
            AutoRamEnabled = false;
            AutoRamMinutes = 15;
            AutoRamOnlyHigh = true;
            AutoRamNotify = true;
            ShowTrayIcon = true;
            CloseToTray = true;
            ConfirmExit = true;
            Settings.Delete(TrayHintSetting);
            SettingsReset?.Invoke();
            Logger.Success(L.T("settings.reset.log"));
        }

        // ------------------------------------------------------------------ menú del icono junto al reloj

        /// <summary>«Liberar memoria RAM» desde el icono: lo mismo que el botón de la pestaña Memoria, con su aviso.</summary>
        public System.Threading.Tasks.Task CleanMemoryFromTrayAsync() => CleanMemoryAsync(false);

        /// <summary>
        /// «Limpiar archivos temporales» desde el icono: solo las carpetas de temporales de Windows y del usuario,
        /// con la misma regla que en Limpieza (archivos de más de un día que no estén en uso).
        /// </summary>
        public async System.Threading.Tasks.Task CleanTempFromTrayAsync()
        {
            if (IsBusy) return;
            var ids = new[] { "user-temp", "windows-temp" };
            var items = Cleanup.CleanupCatalog.All.Where(c => ids.Contains(c.Id)).ToList();
            if (items.Count == 0) return;
            try
            {
                var results = await RunBusyAsync(L.T("main.cleanup.busyClean"), p => Cleanup.CleanupEngine.Clean(items, false, p));
                foreach (var r in results)
                    CleanupItems.FirstOrDefault(c => c.Model.Id == r.Id)?.SetFreed(r.Bytes);
                RefreshLiveInfo();
                long freed = results.Where(r => r.Bytes.HasValue).Sum(r => r.Bytes.Value);
                if (freed > 0)
                    Notifications.Show(new ToastMessage
                    {
                        Kind = ToastKind.Success,
                        Glyph = "",
                        Title = L.F("tray.temp.freed.title", FileCleaner.FormatSize(freed)),
                        Text = L.T(results.Sum(r => r.Failed) > 0 ? "tray.temp.freed.textInUse" : "tray.temp.freed.text"),
                    });
                else
                    Notifications.Show(new ToastMessage
                    {
                        Kind = ToastKind.Info,
                        Glyph = "",
                        Title = L.T("tray.temp.nothing.title"),
                        Text = L.T("tray.temp.nothing.text"),
                    });
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                Notifications.Show(new ToastMessage { Kind = ToastKind.Warning, Glyph = "", Title = L.T("tray.temp.failed.title"), Text = ex.Message });
            }
        }

        /// <summary>Para que las vistas vuelvan a marcar el tema y el intervalo.</summary>
        public event Action SettingsReset;
    }
}
