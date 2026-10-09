using System;
using System.Collections.Generic;
using System.IO;
using Eclipse.Modding;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Eclipse.UI
{
    public sealed partial class TitleScreen
    {
        private ModSelection modSelection;
        private ModDiscoveryResult modDiscovery;
        private string modSelectionPath;
        private string modMessage;
        // The scrolling list keeps its place, its focus and the seals' states across redraws.
        private float modScroll;
        private string modFocusId;
        private Dictionary<string, bool> modStatesBefore;
        private RectTransform modListContent, modDetails;
        private readonly Dictionary<GameObject, ModDescriptor> modRows = new Dictionary<GameObject, ModDescriptor>();
        private ModDescriptor modDetailed;
        private List<ModDiagnostic> modIssues = new List<ModDiagnostic>();
        private string pendingModZip;
        private bool pendingModZipIsTemporary;
        private ModZipPreview pendingModPreview;
        private const float ModRowHeight = 54f, ModRowStep = 60f;

        private void OpenMods()
        {
            modScroll = 0f;
            modFocusId = null;
            modMessage = null;
            try
            {
                string root = ModHost.GetDefaultModsRoot();
                modDiscovery = ModDiscovery.DiscoverLoose(root);
                modSelectionPath = ModHost.GetSelectionPath(root);
                try { modSelection = ModSelection.Load(modSelectionPath); }
                catch (Exception settingsError)
                {
                    modSelection = new ModSelection();
                    modMessage = "Saved selection could not be read. Review the defaults below, then Apply to replace it.";
                    Debug.LogWarning("[Mods] " + settingsError.Message);
                }
                DrawMods();
            }
            catch (Exception error)
            {
                Clear("Mods");
                Heading("Mods");
                Label(page, "Unable to read mod settings", 76, 180, 1100, 48, 30, Ink);
                Label(page, error.Message, 76, 190, 1100, 300, 22, Ink);
                Button(page, "Back", 76, 604, 240, 48, Home, UiSound.Back);
                FocusFirst();
            }
        }

        private DependencyResolutionResult ResolveModSelection() => DependencyResolver.Resolve(
            modSelection.Filter(modDiscovery.Mods), ModPlatformVersions.Core);

        private void DrawMods(int focus = 0)
        {
            Clear("Mods");
            modRows.Clear();
            modDetailed = null;
            Heading("Mods");
            if (CommunityModsSetting.Enabled)
            {
                Label(page, "Core " + ModPlatformVersions.Core + "  ·  Always enabled", 420, 108, 516, 40, 20, Ink, TextAnchor.MiddleRight);
                Button(page, "Community mods", 952, 100, 240, 48, OpenCommunity, UiSound.Open);
            }
            else Label(page, "Core " + ModPlatformVersions.Core + "  ·  Always enabled", 730, 108, 465, 40, 20, Ink, TextAnchor.MiddleRight);
            Label(page, "Enable or disable mods, then apply to restart. Dependencies are toggled together.", 76, 166, 1120, 40, 19, Ink);
            var mods = modDiscovery.Mods;
            var resolution = ResolveModSelection();
            modIssues = new List<ModDiagnostic>(modDiscovery.Diagnostics);
            modIssues.AddRange(resolution.Diagnostics);

            // Left: every mod in one scrolling list. Right: details for the focused mod.
            var list = Rect(page, "Mod list", 64, 212, 624, 336);
            var scroll = list.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            var view = Rect(list, "Viewport", 0, 0, 624, 336);
            view.gameObject.AddComponent<RectMask2D>();
            view.gameObject.AddComponent<Image>().color = Color.clear;
            modListContent = Rect(view, "Content", 0, 0, 624, Mathf.Max(336f, mods.Count * ModRowStep));
            scroll.viewport = view;
            scroll.content = modListContent;
            list.gameObject.AddComponent<Eclipse.Multiplayer.ScrollToSelection>().Scroll = scroll;
            Button focusRow = null;
            for (int i = 0; i < mods.Count; i++)
            {
                var mod = mods[i];
                var row = ModRow(mod, i * ModRowStep + 3f, mods);
                if (mod.Id.Value == modFocusId) focusRow = row;
            }
            if (mods.Count == 0) Label(page, "No mods found in the Mods folder.", 76, 240, 600, 70, 26, Ink);
            modDetails = Rect(page, "Mod details", 716, 212, 480, 336);
            var rule = Stroke(page, "Details rule", 702, 222, 316, 5, "details rule");
            rule.rectTransform.pivot = new Vector2(0f, .5f);
            rule.rectTransform.localRotation = Quaternion.Euler(0, 0, -90);
            rule.color = new Color(Ink.r, Ink.g, Ink.b, .3f);

            bool errors = resolution.HasErrors;
            var status = Label(page, modMessage ?? (errors ? "Some enabled mods have unmet requirements. Review details or disable them." : "Selections apply after a restart. Your saved mod progress is kept."),
                76, 552, modIssues.Count > 0 ? 880 : 1120, 40, 17, errors ? Red : new Color(Ink.r, Ink.g, Ink.b, .85f));
            status.supportRichText = false;
            if (modIssues.Count > 0)
            {
                var issues = modIssues;
                Button(page, "Details (" + issues.Count + ")", 972, 552, 220, 40, () => DrawModIssues(issues, 0), UiSound.Open, Look.Field);
            }
            // One row of actions; Apply & Restart is the page's single primary action.
            Button(page, "Back", 76, 604, 160, 48, Home, UiSound.Back);
            Button(page, "Install ZIP", 250, 604, 190, 48, PickModZip);
            Button(page, "Characters", 454, 604, 190, 48, OpenCharacters, UiSound.Open);
            // Opens the multiplayer menus straight on the Moveset Lab.
            Button(page, "Moveset Lab", 658, 604, 200, 48, () =>
            {
                Eclipse.Multiplayer.LocalVersusMenu.OpenMovesetLabOnEntry();
                Eclipse.Multiplayer.LocalVersusSession.RequestEntry();
                BeginCampaign();
            }, UiSound.Begin);
            var apply = Button(page, "Apply & Restart", 896, 604, 300, 48, ApplyMods, UiSound.Confirm, Look.Primary);
            apply.interactable = !errors;
            FocusFirst();
            // Keep the list where it was; the focused row scrolls itself into view if needed.
            modListContent.anchoredPosition = new Vector2(0f, modScroll);
            if (focusRow != null) focusRow.Select();
            else if (focus > 0 && focus < controls.Count) controls[focus].Select();
            ShowModDetails(focusRow != null ? modRows[focusRow.gameObject] : mods.Count > 0 ? mods[0] : null);
            modStatesBefore = null;
        }

        // One mod: its seal (stamped red while enabled), name and version. Choosing the row
        // toggles it; the seals of mods toggled with it as dependencies stamp too.
        private Button ModRow(ModDescriptor mod, float y, IReadOnlyList<ModDescriptor> mods)
        {
            bool enabled = modSelection.IsEnabled(mod.Id);
            var wash = Stroke(modListContent, mod.Id.Value + " focus", 0, y - 2, 620, ModRowHeight + 4, mod.Id.Value);
            wash.color = new Color(Red.r, Red.g, Red.b, .13f); wash.Taper = .35f; wash.Fill = 0f;
            var row = Button(modListContent, mod.Manifest.Name, 6, y, 610, ModRowHeight, () =>
            {
                modStatesBefore = new Dictionary<string, bool>();
                foreach (var other in mods) modStatesBefore[other.Id.Value] = modSelection.IsEnabled(other.Id);
                modSelection.SetEnabled(mod.Id, !modSelection.IsEnabled(mod.Id), mods);
                modMessage = "Changes pending. Apply & Restart to use this selection.";
                modFocusId = mod.Id.Value;
                modScroll = modListContent != null ? modListContent.anchoredPosition.y : 0f;
                DrawMods();
            }, UiSound.Toggle, Look.Field);
            var name = row.GetComponentInChildren<Text>();
            name.supportRichText = false;
            name.rectTransform.anchoredPosition = new Vector2(62f, 0f);
            name.rectTransform.sizeDelta = new Vector2(410f, ModRowHeight - 6f);
            name.resizeTextForBestFit = true; name.resizeTextMinSize = 16; name.resizeTextMaxSize = 24;
            var fx = row.GetComponent<EclipseUiButton>();
            fx.Rehome();
            var version = Label(row.transform, mod.Version.ToString(), 476, 0, 120, ModRowHeight - 6f, 16, new Color(Ink.r, Ink.g, Ink.b, .6f), TextAnchor.MiddleRight);
            version.supportRichText = false;
            var seal = InkSeal.Create(row.transform, "Seal", 34f, enabled);
            seal.rectTransform.anchorMin = seal.rectTransform.anchorMax = new Vector2(0f, 1f);
            seal.rectTransform.anchoredPosition = new Vector2(32f, -(ModRowHeight - 6f) * .5f);
            bool before;
            if (modStatesBefore != null && modStatesBefore.TryGetValue(mod.Id.Value, out before) && before != enabled)
            {
                seal.Set(before, false);
                seal.Set(enabled, true);
            }
            fx.AddAccent(wash);
            modRows[row.gameObject] = mod;
            return row;
        }

        // The details pane follows focus through the list and keeps the last mod shown while
        // focus is on the buttons below.
        private void UpdateModDetails()
        {
            if (currentPage != "Mods" || modDetails == null || EventSystem.current == null) return;
            var focused = EventSystem.current.currentSelectedGameObject;
            ModDescriptor mod;
            if (focused != null && modRows.TryGetValue(focused, out mod) && mod != modDetailed) ShowModDetails(mod);
        }

        private void ShowModDetails(ModDescriptor mod)
        {
            if (modDetails == null) return;
            modDetailed = mod;
            for (int i = modDetails.childCount - 1; i >= 0; i--) Destroy(modDetails.GetChild(i).gameObject);
            if (mod == null) return;
            var manifest = mod.Manifest;
            bool enabled = modSelection.IsEnabled(mod.Id);
            var name = Label(modDetails, manifest.Name, 0, 0, 480, 44, 30, Ink);
            name.supportRichText = false;
            name.resizeTextForBestFit = true; name.resizeTextMinSize = 18; name.resizeTextMaxSize = 30;
            string version = "Version " + manifest.Version;
            if (!SameName(manifest.Name, manifest.Id.Value)) version += "   ·   " + manifest.Id.Value;
            var versionLabel = Label(modDetails, version, 0, 44, 480, 24, 16, new Color(Ink.r, Ink.g, Ink.b, .65f));
            versionLabel.supportRichText = false;
            var seal = InkSeal.Create(modDetails, "State seal", 26f, enabled);
            seal.rectTransform.anchorMin = seal.rectTransform.anchorMax = new Vector2(0f, 1f);
            seal.rectTransform.anchoredPosition = new Vector2(13f, -91f);
            Label(modDetails, enabled ? "Enabled" : "Disabled", 36, 78, 440, 26, 19, Ink);
            var facts = new System.Text.StringBuilder();
            if (manifest.Authors.Count > 0) facts.Append("By ").Append(string.Join(", ", manifest.Authors)).Append('\n');
            facts.Append(manifest.HasEntrypoint ? "Scripted (Lua)" : "Content only (no scripts)");
            if (manifest.Capabilities.Count > 0) facts.Append("   ·   uses ").Append(string.Join(", ", manifest.Capabilities));
            facts.Append('\n');
            if (manifest.Dependencies.Count > 0)
            {
                facts.Append("Requires ");
                for (int i = 0; i < manifest.Dependencies.Count; i++)
                {
                    if (i > 0) facts.Append(", ");
                    facts.Append(ModLabel(manifest.Dependencies[i].Id));
                }
                facts.Append('\n');
            }
            int problems = 0;
            ModDiagnostic first = null;
            foreach (var issue in modIssues)
                if (Concerns(issue, mod)) { problems++; if (first == null) first = issue; }
            var factsLabel = Label(modDetails, facts.ToString().TrimEnd(), 0, 116, 480, 150, 16, new Color(Ink.r, Ink.g, Ink.b, .85f), TextAnchor.UpperLeft);
            factsLabel.supportRichText = false;
            factsLabel.lineSpacing = 1.15f;
            if (first != null)
            {
                var problem = Label(modDetails, (problems == 1 ? "1 issue: " : problems + " issues. First: ") + first.Message,
                    0, 266, 480, 66, 15, Red, TextAnchor.UpperLeft);
                problem.supportRichText = false;
            }
            if (!InkTheme.ReducedMotion) UiReveal.Play(modDetails, 0f, .18f, new Vector2(10f, 0f));
        }

        private string ModLabel(ModId id)
        {
            foreach (var mod in modDiscovery.Mods)
                if (mod.Id == id) return mod.Manifest.Name;
            return id.Value;
        }

        private static bool Concerns(ModDiagnostic issue, ModDescriptor mod)
        {
            if (string.IsNullOrEmpty(issue.Source)) return false;
            return issue.Source.IndexOf(mod.Id.Value, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   issue.Source.IndexOf(mod.RootPath, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // True when an id only restates the display name ("chiaroscuro" for "Chiaroscuro").
        private static bool SameName(string name, string id)
        {
            return Normalize(name) == Normalize(id);
        }

        private static string Normalize(string text)
        {
            var result = new System.Text.StringBuilder();
            foreach (char c in text ?? string.Empty)
                if (char.IsLetterOrDigit(c)) result.Append(char.ToLowerInvariant(c));
            return result.ToString();
        }

        private void DrawModIssues(List<ModDiagnostic> issues, int index)
        {
            Clear("Mod details");
            Heading("Mod diagnostics");
            var message = Label(page, issues[index].ToString(), 76, 190, 1120, 310, 23, Ink);
            message.supportRichText = false;
            Label(page, (index + 1) + " / " + issues.Count, 76, 552, 200, 40, 20, Ink);
            Button(page, "Back to Mods", 76, 604, 350, 48, () => DrawMods(), UiSound.Back);
            if (issues.Count > 1) Button(page, "Next issue", 842, 604, 350, 48, () => DrawModIssues(issues, (index + 1) % issues.Count));
            FocusFirst();
        }

        private void ApplyMods()
        {
            if (ResolveModSelection().HasErrors) { DrawMods(); return; }
            // The restart saves the loaded profile; the title's preview must not be it.
            DiscardGameDataPreview();
            if (!GameSessionRestart.TryRestart(() => modSelection.Save(modSelectionPath), out var error))
            {
                modMessage = error ?? "Restart already in progress.";
                DrawMods();
                return;
            }
            Clear("Restarting");
            Label(page, "Restarting with your mod selection...", 76, 270, 1120, 120, 36, Ink, TextAnchor.MiddleCenter);
        }

        private void PickModZip()
        {
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                modMessage = "Choose a mod ZIP in Android's file picker.";
                DrawMods();
                ModZipPicker.PickAndroid(gameObject.name);
#else
                string path = ModZipPicker.PickDesktop();
                if (!string.IsNullOrEmpty(path)) PreviewModZip(path, false);
#endif
            }
            catch (Exception error)
            {
                modMessage = "Could not open file picker: " + error.Message;
                DrawMods();
            }
        }

        // Called by the Android document picker after it copies the selected URI to app cache.
        public void OnModZipPicked(string path)
        {
            if (!string.IsNullOrEmpty(path)) PreviewModZip(path, true);
            else { modMessage = "ZIP selection canceled."; DrawMods(); }
        }

        public void OnModZipPickerError(string error)
        {
            modMessage = "Could not read ZIP: " + error;
            DrawMods();
        }

        private void PreviewModZip(string path, bool temporary)
        {
            ClearPendingModZip();
            pendingModZip = path;
            pendingModZipIsTemporary = temporary;
            try
            {
                if (!string.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Choose a .zip archive.");
                pendingModPreview = ModZipInstaller.Inspect(path, ModHost.GetDefaultModsRoot());
                DrawModZipPreview();
            }
            catch (Exception error)
            {
                ClearPendingModZip();
                modMessage = "ZIP cannot be installed: " + error.Message;
                DrawMods();
            }
        }

        private void DrawModZipPreview(string error = null)
        {
            Clear("Mod ZIP");
            Heading(pendingModPreview.IsUpdate ? "Update mod" : "Install mod");
            var name = Label(page, pendingModPreview.Manifest.Name + "  " + pendingModPreview.Manifest.Version,
                76, 205, 1100, 52, 30, Ink);
            name.supportRichText = false;
            var id = Label(page, "ID: " + pendingModPreview.Manifest.Id, 76, 262, 1100, 40, 22, Ink);
            id.supportRichText = false;
            Label(page, pendingModPreview.IsUpdate
                ? "The installed mod folder will be replaced. Your saved mod progress is kept."
                : "The mod will be added to this installation's Mods directory.",
                76, 335, 1100, 85, 21, Ink);
            var status = Label(page, error ?? "Review the mod, then install it. Apply & Restart on the Mods screen to load it.",
                76, 480, 1100, 90, 20, error == null ? Ink : Red);
            status.supportRichText = false;
            Button(page, "Back / Cancel", 76, 604, 320, 48, CancelModZip, UiSound.Back);
            Button(page, pendingModPreview.IsUpdate ? "Replace mod" : "Install mod", 742, 604, 450, 48, InstallModZip, UiSound.Confirm, Look.Primary);
            FocusFirst();
        }

        private void InstallModZip()
        {
            try
            {
                ModZipPreview result = ModZipInstaller.Install(pendingModZip, ModHost.GetDefaultModsRoot(),
                    pendingModPreview.IsUpdate, pendingModPreview.Manifest.Id);
                ClearPendingModZip();
                modDiscovery = ModDiscovery.DiscoverLoose(ModHost.GetDefaultModsRoot());
                modMessage = (result.IsUpdate ? "Updated " : "Installed ") + result.Manifest.Name +
                    ". Review its toggle, then Apply & Restart.";
                DrawMods();
            }
            catch (Exception error)
            {
                DrawModZipPreview("Install failed: " + error.Message);
            }
        }

        private void CancelModZip()
        {
            ClearPendingModZip();
            modMessage = "ZIP install canceled.";
            DrawMods();
        }

        private void ClearPendingModZip()
        {
            if (pendingModZipIsTemporary && !string.IsNullOrEmpty(pendingModZip))
            {
                try { File.Delete(pendingModZip); }
                catch (Exception error) { Debug.LogWarning("[Mods] Could not remove temporary ZIP: " + error.Message); }
            }
            pendingModZip = null;
            pendingModZipIsTemporary = false;
            pendingModPreview = null;
        }
    }
}
