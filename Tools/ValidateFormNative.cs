using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Eclipse.Modding;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class ValidateFormNative
{
    const string Active = "Eclipse.FormNative.Active";
    static double started, lastReport;
    static bool campaign, entered;
    static Model original;
    static float originalRatio;
    static int switchedAt = -1;
    static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    static ValidateFormNative()
    {
        if (SessionState.GetBool(Active, false))
        {
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Update;
        }
    }

    public static void RunEditor()
    {
        string root = Directory.GetParent(Application.dataPath).FullName;
        if (!File.Exists(Path.Combine(root, "form-native-fixture.marker")))
            throw new InvalidOperationException("Native form validation requires an isolated fixture.");
        Environment.SetEnvironmentVariable("ECLIPSE_MODS_ROOT", Path.Combine(root, "Mods"));
        PlayerSettings.companyName = "EclipseAcceptance";
        PlayerSettings.productName = Path.GetFileName(root);
        SessionState.SetBool(Active, true);
        EditorSceneManager.OpenScene("Assets/src/GUI/Scenes/GameLoaderScene/GameLoader.unity");
        EditorApplication.EnterPlaymode();
    }

    static void Update()
    {
        if (!EditorApplication.isPlaying) return;
        try
        {
            if (EditorApplication.timeSinceStartup - started > 240)
                throw new Exception("Timed out during " + (entered ? "native fight" : "game boot") + ".");
            if (EditorApplication.timeSinceStartup - lastReport > 15)
            {
                lastReport = EditorApplication.timeSinceStartup;
                Debug.Log("[FormNative] Waiting: campaign=" + campaign + " entered=" + entered + " scripts=" + (ModRuntime.Scripts != null));
            }
            if (!campaign && Eclipse.UI.TitleScreen.IsOpen)
            {
                var title = UnityEngine.Object.FindObjectOfType<Eclipse.UI.TitleScreen>();
                if (title != null)
                {
                    typeof(Eclipse.UI.TitleScreen).GetMethod("BeginCampaign", Hidden).Invoke(title, null);
                    campaign = true;
                }
                return;
            }
            if (!entered)
            {
                var scripts = ModRuntime.Scripts;
                if (scripts == null || ListSF.CCDKHLAMKKO() == null || Module.ELEBLBJKDBI() == null) return;
                var screen = Module.ELEBLBJKDBI().NMCNDOPKFJD();
                if (screen != ScreenType.ModuleDojo && screen != ScreenType.ModuleMap) return;
                var definition = scripts.Content.Fights.FirstOrDefault(f => f.Id.ToString() == "example.shifting-guardian:fights/guardian");
                if (definition == null) throw new Exception("Shifting Guardian did not register.");
                var encounter = ListSF.CHMCKGCDGCM(new FightIDS(scripts.Content.RuntimeFightId(definition.Id)));
                if (encounter == null) throw new Exception("Native encounter projection is missing.");
                entered = GameUtils.StartFight(encounter, false, null, true, false);
                Debug.Log("[FormNative] StartFight=" + entered);
                return;
            }
            var fight = Fight.OHNKFOHIAKG();
            if (fight == null) return;
            var enemy = (Model)typeof(Fight).GetField("CKNCPOABFBO", Hidden).GetValue(fight);
            var player = (Model)typeof(Fight).GetField("_playerModel", Hidden).GetValue(fight);
            if (enemy == null || player == null || fight.get_FightTimeInFrames() < 1) return;
            player.KMMJCHDKBDO.set_IsImmortalityEnabled(true);
            if (original == null)
            {
                if (enemy.KMMJCHDKBDO.EclipseCharacterId != "example.shifting-guardian:warriors/staff")
                    throw new Exception("Wrong initial native character: " + enemy.KMMJCHDKBDO.EclipseCharacterId);
                original = enemy;
                enemy.GFNCMLFKBGP(enemy.KMMJCHDKBDO.CIDCNCDFONA * .75f);
                originalRatio = enemy.KKMCHCNOHMB() / enemy.KMMJCHDKBDO.CIDCNCDFONA;
                Debug.Log("[FormNative] Initial body ready; health ratio=" + originalRatio);
            }
            if (enemy != original && switchedAt < 0)
            {
                if (enemy.KMMJCHDKBDO.EclipseCharacterId != "example.shifting-guardian:warriors/baton")
                    throw new Exception("Unexpected replacement character.");
                float ratio = enemy.KKMCHCNOHMB() / enemy.KMMJCHDKBDO.CIDCNCDFONA;
                if (Math.Abs(ratio - originalRatio) > .001f) throw new Exception("Health ratio changed across form swap: " + ratio);
                switchedAt = fight.get_FightTimeInFrames();
                Debug.Log("[FormNative] Native body replaced at frame " + switchedAt);
            }
            if (switchedAt >= 0 && fight.get_FightTimeInFrames() >= switchedAt + 120)
            {
                if (enemy.CLDMEJKGLBA() == null || enemy.FHBLLPCEAHG() == null || !enemy.MJNPBMOAFML().activeSelf)
                    throw new Exception("Replacement did not remain visible and animated.");
                Debug.Log("[FormNative] PASS: real game boot, registered encounter, Lua-requested native body replacement, health continuity and 120 subsequent combat frames.");
                Finish(0);
            }
            else if (switchedAt < 0 && fight.get_FightTimeInFrames() > 420)
            {
                throw new Exception("Form did not apply: " + ReadFormFeedback());
            }
        }
        catch (Exception error)
        {
            Debug.LogError("[FormNative] FAIL: " + error);
            Finish(1);
        }
    }

    // Read the Lua-owned state as well as rendered text: an inactive/missing HUD
    // must not hide the original request failure from native acceptance logs.
    static string ReadFormFeedback()
    {
        var contexts = (System.Collections.IEnumerable)typeof(ModScriptSession)
            .GetField("_contexts", Hidden).GetValue(ModRuntime.Scripts);
        foreach (var context in contexts)
        {
            var property = context.GetType().GetProperty("UiScope");
            var scope = property?.GetValue(context) as ModUiScope;
            if (scope == null || scope.Owner.Value != "example.shifting-guardian") continue;
            var surfaces = (System.Collections.IDictionary)typeof(ModUiScope)
                .GetField("surfaces", Hidden).GetValue(scope);
            foreach (ModUiSurface surface in surfaces.Values)
                if (surface.Id == "shift")
                    return surface.Read("phase").Text + " | " + surface.Read("result").Text;
        }
        return "Shifting Guardian HUD state is absent.";
    }

    static void Finish(int code)
    {
        SessionState.SetBool(Active, false);
        EditorApplication.update -= Update;
        EditorApplication.Exit(code);
    }
}
