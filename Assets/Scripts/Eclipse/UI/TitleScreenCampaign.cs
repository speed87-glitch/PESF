using System;
using System.Collections.Generic;
using Eclipse.Saves;
using UnityEngine;
using UnityEngine.UI;

namespace Eclipse.UI
{
    public sealed partial class TitleScreen
    {
        private const int CampaignsPerPage = 4;
        private CampaignSaveStore campaignStore;
        private List<CampaignSaveInfo> campaigns = new List<CampaignSaveInfo>();
        private int campaignPage;
        private string campaignMessage;
        private InputField campaignNameField;
        private Action campaignNameSubmit;

        private void OpenCampaignSaves()
        {
            try
            {
                SF2Paths.Init();
                var store = new CampaignSaveStore(SF2Paths.GetLegacyUserDataDirectory());
                store.Initialize();
                campaignStore = store;
                campaignMessage = null;
                DrawCampaignSaves();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                campaignStore = null;
                Clear("Saves");
                Heading("Saves");
                var message = Label(page, "Could not open your saves.\n" + error.Message, 76, 220, 1120, 180, 23, Ink);
                message.supportRichText = false;
                Button(page, "Back", 76, 604, 320, 48, Home, UiSound.Back);
                Button(page, "Try again", 742, 604, 450, 48, OpenCampaignSaves);
                FocusFirst();
            }
        }

        private void DrawCampaignSaves(string focusId = null)
        {
            if (campaignStore == null) { OpenCampaignSaves(); return; }
            try { campaigns = campaignStore.List(); }
            catch (Exception error) { campaignMessage = error.Message; campaigns = new List<CampaignSaveInfo>(); }
            if (focusId != null)
            {
                int index = campaigns.FindIndex(save => save.Id == focusId);
                if (index >= 0) campaignPage = index / CampaignsPerPage;
            }
            int pages = Math.Max(1, (campaigns.Count + CampaignsPerPage - 1) / CampaignsPerPage);
            campaignPage = Math.Max(0, Math.Min(campaignPage, pages - 1));
            Clear("Saves");
            Heading("Saves");
            Label(page, campaigns.Count + (campaigns.Count == 1 ? " save" : " saves"), 990, 108, 200, 40, 20, Ink, TextAnchor.MiddleRight);
            Label(page, "Choose a save to continue, or begin a new one.", 76, 166, 1120, 36, 20, Ink);
            Button focus = null;
            for (int row = 0; row < CampaignsPerPage && campaignPage * CampaignsPerPage + row < campaigns.Count; row++)
            {
                CampaignSaveInfo save = campaigns[campaignPage * CampaignsPerPage + row];
                var play = SaveRow(save, 218 + row * 72);
                if (save.Id == focusId) focus = play;
            }
            if (campaigns.Count == 0)
            {
                Label(page, "Your journey begins here.", 76, 258, 1120, 68, 32, Ink, TextAnchor.MiddleCenter);
                Label(page, "Create a save to enter the world of Shadow Fight.", 76, 330, 1120, 60, 21, Ink, TextAnchor.MiddleCenter);
            }
            if (!string.IsNullOrEmpty(campaignMessage))
            {
                var status = Label(page, campaignMessage, 76, 510, 1120, 40, 17, Red);
                status.supportRichText = false;
            }
            if (pages > 1)
            {
                Button(page, "Previous", 76, 552, 240, 40, () => { campaignPage = (campaignPage + pages - 1) % pages; DrawCampaignSaves(); }, UiSound.Tab);
                Label(page, (campaignPage + 1) + " / " + pages, 332, 552, 150, 40, 20, Ink);
                Button(page, "Next", 500, 552, 210, 40, () => { campaignPage = (campaignPage + 1) % pages; DrawCampaignSaves(); }, UiSound.Tab);
            }
            Button(page, "Back", 76, 604, 320, 48, Home, UiSound.Back);
            var create = Button(page, "New save", 742, 604, 450, 48, () => CampaignNamePrompt(null), UiSound.Open, Look.Primary);
            FocusFirst();
            if (focus != null && focus.interactable) focus.Select();
            else if (campaigns.Count == 0) create.Select();
        }

        // One save across the card: a full-width brush bar with its name and progress, and when
        // it was last played on the right. On the focused row, Rename and Delete take that place.
        private Button SaveRow(CampaignSaveInfo save, float y)
        {
            var root = Rect(page, "Save " + save.Name, 76, y, 1120, 64);
            var play = Button(root, save.Name, 0, 0, 1120, 64, () => LoadCampaign(save), UiSound.Begin);
            var plate = play.transform.Find("Plate").GetComponent<InkStroke>();
            plate.Taper = .2f;
            var name = play.GetComponentInChildren<Text>();
            name.supportRichText = false; name.fontSize = 23;
            name.resizeTextForBestFit = true; name.resizeTextMinSize = 17; name.resizeTextMaxSize = 23;
            // Clear the stroke's pointed head and its ragged top edge.
            name.rectTransform.anchoredPosition = new Vector2(54, -8);
            name.rectTransform.sizeDelta = new Vector2(620, 30);
            play.GetComponent<EclipseUiButton>().Rehome();
            string summary = save.Error == null ? CampaignSummary(save) : "Save details could not be read";
            var detail = Label(play.transform, summary, 54, 37, 620, 20, 15, new Color(Paper.r, Paper.g, Paper.b, .85f));
            detail.supportRichText = false;
            play.interactable = save.Error == null;

            var info = Rect(play.transform, "Played", 700, 0, 384, 64);
            var infoGroup = info.gameObject.AddComponent<CanvasGroup>();
            infoGroup.blocksRaycasts = false;
            var played = Label(info, save.LastPlayedUtc != default ? "Played " + Ago(save.LastPlayedUtc) : "Not played yet",
                0, 9, 384, 26, 18, Paper, TextAnchor.MiddleRight);
            played.supportRichText = false;
            if (save.CreatedUtc != default)
                Label(info, "Begun " + save.CreatedUtc.ToLocalTime().ToString("d MMM yyyy"), 0, 34, 384, 22, 14,
                    new Color(Paper.r, Paper.g, Paper.b, .62f), TextAnchor.MiddleRight).supportRichText = false;

            var actions = Rect(root, "Actions", 764, 0, 356, 64);
            var actionsGroup = actions.gameObject.AddComponent<CanvasGroup>();
            var rename = Button(actions, "Rename", 26, 12, 156, 40, () => CampaignNamePrompt(save), UiSound.Open);
            rename.GetComponent<EclipseUiButton>().SetColors(PaperDim, Red, Ink, Paper);
            rename.interactable = save.Error == null;
            var delete = Button(actions, "Delete", 192, 12, 148, 40, () => DeleteCampaignPrompt(save), UiSound.Open);
            delete.GetComponent<EclipseUiButton>().SetColors(PaperDim, Red, Ink, Paper);
            FocusGroup.Attach(root.gameObject, actionsGroup, infoGroup, play, rename, delete);
            return play;
        }

        private static readonly Color PaperDim = new Color32(196, 178, 146, 255);

        // "just now", "3 hours ago", "yesterday", "5 days ago", then the date.
        private static string Ago(DateTime utc)
        {
            var span = DateTime.UtcNow - utc;
            if (span.TotalMinutes < 2) return "just now";
            if (span.TotalHours < 1) return (int)span.TotalMinutes + " minutes ago";
            if (span.TotalHours < 24) return (int)span.TotalHours == 1 ? "an hour ago" : (int)span.TotalHours + " hours ago";
            if (span.TotalDays < 2) return "yesterday";
            if (span.TotalDays < 14) return (int)span.TotalDays + " days ago";
            return "on " + utc.ToLocalTime().ToString("d MMM yyyy");
        }

        private string CampaignSummary(CampaignSaveInfo save)
        {
            try { return campaignStore.Progress(save.Id); }
            catch (Exception error) { Debug.LogWarning("[Campaigns] " + error.Message); return "Progress unavailable"; }
        }

        private void LoadCampaign(CampaignSaveInfo save)
        {
            if (leaving) return;
            try
            {
                CampaignSaveSession.Select(campaignStore, save.Id);
                BeginCampaign();
            }
            catch (Exception error)
            {
                CampaignSaveSession.Clear();
                campaignMessage = "Could not open save: " + error.Message;
                DrawCampaignSaves(save.Id);
            }
        }

        private void CampaignNamePrompt(CampaignSaveInfo save)
        {
            bool creating = save == null;
            Clear(creating ? "New save" : "Rename save");
            Heading(creating ? "A new journey" : "Rename save");
            Label(page, "Give your save a name.", 76, 212, 1120, 44, 24, Ink);
            var fieldRoot = Rect(page, "Save name", 76, 282, 1120, 64);
            var input = InkField.Build(fieldRoot, font, 28, false, "Save name");
            input.characterLimit = CampaignSaveStore.MaximumNameLength;
            input.text = creating ? "Save " + (campaigns.Count + 1) : save.Name;
            campaignNameField = input;
            controls.Add(input);
            Label(page, "Up to 48 characters. Each save keeps its own progress.", 76, 376, 1120, 44, 20, Ink);
            var errorText = Label(page, "", 76, 466, 1120, 90, 19, Red); errorText.supportRichText = false;
            Action submit = () =>
            {
                if (leaving) return;
                try
                {
                    if (creating)
                    {
                        CampaignSaveInfo created = campaignStore.Create(input.text);
                        campaignMessage = null;
                        LoadCampaign(created);
                    }
                    else
                    {
                        campaignStore.Rename(save.Id, input.text);
                        campaignMessage = null;
                        DrawCampaignSaves(save.Id);
                    }
                }
                catch (Exception error) { errorText.text = error.Message; }
            };
            campaignNameSubmit = submit;
            // Confirm follows the field in keyboard/controller navigation.
            Button(page, creating ? "Create & play" : "Save name", 742, 604, 450, 48, submit, creating ? UiSound.Begin : UiSound.Confirm, Look.Primary);
            Button(page, "Cancel", 76, 604, 320, 48, () => DrawCampaignSaves(save?.Id), UiSound.Back);
            FocusFirst();
        }

        private void DeleteCampaignPrompt(CampaignSaveInfo save)
        {
            Clear("Delete save");
            Heading("End this journey?");
            var name = Label(page, save.Name, 76, 240, 1120, 72, 32, Ink, TextAnchor.MiddleCenter);
            name.supportRichText = false;
            Label(page, "This deletes the save file and its saved progress.\nThis cannot be undone.",
                76, 330, 1120, 100, 23, Ink, TextAnchor.MiddleCenter);
            Button(page, "Keep save", 76, 604, 450, 48, () => DrawCampaignSaves(save.Id), UiSound.Back);
            Button(page, "Delete save", 742, 604, 450, 48, () =>
            {
                try { campaignStore.Delete(save.Id); campaignMessage = "Save file deleted."; }
                catch (Exception error) { campaignMessage = "Could not delete save: " + error.Message; }
                DrawCampaignSaves();
            });
            FocusFirst(); // Keep is the safe default for keyboard/controller confirmation.
        }
    }
}
