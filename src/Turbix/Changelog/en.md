# Changes

This file is shown inside TURBiX, on the “What's new” page. Each version has its own section
with the changes that matter, kept short. Each version starts with
`## X.Y.Z` (no date) and each change is a bullet `- **Title.** Short description.`

## 1.2.0

- **New project website.** TURBiX now has its own page, in six languages: https://julianr87.github.io/TURBiX/
- **Cleaner texts.** The punctuation of help texts, warnings and messages was tidied up in all six languages.

## 1.1.10

- **Silent install.** `TURBiX-Setup.exe --silent` installs without showing any window and `--uninstall --silent` uninstalls without asking. Useful for setting up several PCs and for package managers.
- **Fixes after testing on Windows 10 and 11.** Opening the window while TURBiX was already running next to the clock showed an “Unexpected error” message, it no longer does. On small screens the window no longer goes off the top. Windows notifications now show the name TURBiX.

## 1.1.9

- **New name: TURBiX.** The program, the installer, the shortcuts and the documentation are now written TURBiX. The files are now called `TURBiX.exe` and `TURBiX-Setup.exe`, your settings and backups stay where they were.

## 1.1.8

- **Documentation in six languages.** The project page can now be read in the same languages as TURBiX, with more detail on each feature. The program works the same as 1.1.7.

## 1.1.7

- **Installer.** Besides the usual `TURBiX.exe`, there is now a `TURBiX-Setup.exe` that installs it to Program Files, creates the Start menu shortcut (and a desktop one, if you want) and adds it to the Windows apps list.
- **Uninstaller.** TURBiX is removed from Settings › Apps, like any other program. Uninstalling doesn't undo the changes made to Windows (that's what “Restore” is for) and keeps your backups.
- **Fixes.** In Settings, “Same as Windows” now really shows the Windows language. The activity log moves to a new file each day even if TURBiX stays open. The icon's menu no longer lets you start a task or exit while a question is waiting for an answer. In “Windows Registry”, the same entry no longer shows up twice.
## 1.1.6

- **New Settings page.** In the sidebar, “Appearance” is now “Settings”. It has the theme, the language (which you can now pick yourself), starting with Windows, automatic memory cleanup, the data folder and a button to put everything back to how it came.
- **Icon next to the clock.** While TURBiX is open, its icon shows in the notification area. Double-click opens the window, right-click lets you free up RAM, clean temporary files or exit. You can hide it in Settings.
- **The X no longer closes TURBiX.** Closing the window hides it and TURBiX keeps running next to the clock (you can change this in Settings). To close it completely, use “Exit TURBiX” in the icon's menu, which asks you to confirm. At Windows startup, TURBiX now goes straight to the clock without opening the window.

## 1.1.5

- **Windows notifications.** The freed-memory notice now arrives as a Windows notification, with TURBiX's name and icon, instead of showing up inside the app.
- **Open TURBiX when Windows starts.** New option under Performance › Windows startup (and in Memory). TURBiX opens minimized when you sign in, so the automatic memory cleanup always works. Because it needs administrator rights, it uses a Task Scheduler task called “TURBiX”, which is deleted when you turn the option off.

## 1.1.4

- **Notice after freeing memory.** When you clean up RAM, a notice shows how much was freed and how much is now available. If the automatic cleanup runs while TURBiX is minimized, it arrives as a Windows notification, you can turn it off under Automatic cleanup.

## 1.1.3

- **Checked on Windows 11.** Everything was tested again on a fresh Windows 11: applying the settings, restarting and undoing them from Restore. Fixed a backup name that could show up untranslated.
- **Small screens.** In Processes, long names are now shortened with “…” instead of being cut off, and the links on Home no longer get cut in narrow windows.

## 1.1.2

- **What's new in your language.** With Windows in Spanish, this page appeared in English. It now follows the same language as the rest of TURBiX.

## 1.1.1

- **TURBiX in your language.** The app now uses the language Windows is set to: Spanish, English, Portuguese, French, German or Russian. With any other language it shows in English.
- **Grouped processes.** A new tab in Performance lists every running process, like Task Manager does, but with the processes each program started tucked underneath it. It shows CPU, memory and threads, tells you which service runs inside each “Service Host” and lets you end a task. Processes Windows needs are locked.
- **The log remembers past sessions.** The Activity log now also shows what TURBiX did in earlier sessions, grouped by day.
- **Readable names in dark mode.** In “Installed programs” the names showed up black on the dark background.

## 1.1.0

- **New Apps section.** Removes the apps Windows installs on its own (Candy Crush, Solitaire with ads, Clipchamp, News, TikTok and other promotions) in one click. Only known apps that are safe to remove are listed, never the Store, Xbox or parts of Windows. If you miss one later, you can reinstall it for free from Microsoft Store.
- **Uninstall programs.** A list of installed programs with their size, a search box and a button that opens each program's own uninstaller.
- **More Windows tweaks.** Taskbar and Start (the Meet Now, Chat, Cortana and Task View buttons, search as an icon, left-aligned taskbar, recent files, and the Edge shortcut that comes back after every update), optional services almost nobody uses (Fax, retail demo mode, offline maps, print spooler, indexing…) and turning off hibernation, which frees up several GB. You can undo all of it from “Restore”.
- **Backups no longer overwrite each other.** If two groups of tweaks were applied in the same second (for example, performance and Windows tweaks), the backup of the first one was lost and it couldn't be undone from “Restore”. Now each session gets its own file.
- **Tested on Windows 11 too.** Correct names in Windows startup (Cortana showed up as a code) and text that no longer gets cut off in Restore, Repair and the Home screen.
- **Windows.old cleanup.** Deletes the previous Windows installation and the leftovers from big updates, which usually take up 10 to 30 GB, using Windows' own Disk Cleanup.

## 1.0.10

- **Thoroughly tested on a fresh Windows install.** Every tweak, the cleanup, memory and Windows startup were applied, checked again after a restart and then undone. Everything stuck, and undoing it put the PC back exactly as it was.

## 1.0.9

- **More careful registry repair.** On a fresh Windows install it flagged some .NET entries as broken, even though Windows ships them that way. Now it never touches anything that belongs to the Windows folder, and it no longer shows duplicate entries.
- **Game Mode detected correctly.** Windows has it on by default, but TURBiX showed it as “Not applied”.
- **Processor in Health.** Counts cores correctly on PCs with more than one processor and shows the full model name.
- **Clearer messages.** If the restore point can't be created, the activity log now says why instead of staying blank.

## 1.0.8

- **Correct icons on Windows 10.** The RAM icon didn't exist on Windows 10 and showed up as an empty box. It now looks right on every version.
- **What's new without dates.** The history shows only the version number.

## 1.0.7

- **New Health section.** Shows the condition of each component: drives (health, wear, temperature and hours in use), battery, RAM, processor, graphics card and its driver, devices with problems, and blue screens or unexpected shutdowns in the last month. Each warning tells you what to do.
- **Repair the Windows registry.** In Fixes › Windows registry: finds entries that point to programs that no longer exist (broken startup items, “ghost” programs in the uninstall list, leftovers from deleted programs) and removes them, saving a backup first that you can restore.

## 1.0.6

- **PC checkup.** The Home screen checks the most common reasons a PC is slow (a hard drive that is mechanical or failing, low disk space, little memory, too many startup programs, days without a restart) and tells you what to do about each one.
- **New Fixes › Repair section.** The repairs Microsoft support recommends, one button each: Windows files (SFC), Windows image (DISM), check disk, reset network, unstick the printer and restart Explorer.
- **Fixes › Windows tweaks.** Gets rid of Windows 11 annoyances: classic context menu, visible file extensions, search highlights, account notifications in Start, “End task” on the taskbar, Fast Startup and more.
- **Safer cleanup.** Attachments opened from Outlook and folders pinned to Quick access are no longer touched, and the cleaner never follows links into other folders.
- **More reliable backups and undo.** The original value is saved before every change. If an undo partly fails, TURBiX tells you and lets you try again.
- **Clearer messages.** TURBiX tells you if the restore point couldn't be created, and if it was opened with an administrator account other than yours.
- **Keyboard use.** You can see where the focus is when moving with Tab, and the “?” help opens with Enter.

## 1.0.5

- **New Performance section.** Three tools to help the PC start faster, keep memory free and run games better.
- **Windows startup.** Lists the programs that open by themselves when the PC turns on and lets you turn them off with a switch, without uninstalling anything.
- **RAM.** Shows live usage and which programs use the most. One button frees memory right away, and automatic cleanup repeats it every 5, 10, 15, 30 or 60 minutes.
- **Games.** 13 tweaks that stop Windows from holding games back: background recording, power saving, mouse acceleration, background apps and more. You can undo all of them from “Restore”.

## 1.0.4

- **Full history.** “What's new” shows every version again, without skipping any.

## 1.0.3

- **Installed version in sight.** The top of “What's new” shows which version of TURBiX you're using.

## 1.0.2

- **Clearer status in the sidebar.** Where it used to say “offline”, it now says “Doesn't use the internet”, so it doesn't look like a problem with your connection. The card has its own “?” that explains each item.
- **Cleaner look.** The “?” next to the theme picker is gone, since it's self-explanatory.

## 1.0.1

- **Light, dark or system mode.** New “Appearance” picker in the sidebar. With “System”, TURBiX follows the Windows mode and switches by itself when you change it in Settings.
- **What's new section.** A new page, before “About TURBiX”, with the important changes in each version.

## 1.0.0

- **First version.** A single executable for Windows 10 (1607 or later) and Windows 11, with nothing to install and no dependencies.
- **Privacy and telemetry.** 23 tweaks in 5 groups, with “Recommended” and “Maximum” profiles and the current status of each one.
- **Cleanup.** 20 categories of temporary files and cache, with a read-only scan first, before anything is deleted.
- **Everything can be undone.** Automatic backup of every changed value, undo by session and an optional restore point.
- **Help on every option.** A “?” button briefly explains what each tweak does.
- **Activity log.** Everything TURBiX does is written down on screen and in C:\ProgramData\TURBiX\Logs.
