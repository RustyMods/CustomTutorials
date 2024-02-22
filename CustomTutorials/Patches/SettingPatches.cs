using CustomTutorials.Configurations;
using HarmonyLib;

namespace CustomTutorials.Patches;

public static class SettingPatches
{
    [HarmonyPatch(typeof(Raven), nameof(Raven.Spawn))]
    private static class RavenSpawnPrefix
    {
        private static void Prefix(Raven __instance, Raven.RavenText text)
        {
            if (!__instance) return;
            if (CustomTutorialsPlugin._OverrideSettings.Value is CustomTutorialsPlugin.Toggle.Off) return;
            TutorialData? customTutorial = TutorialManager.customTutorials.Find(x => x.m_key == text.m_key);
            Raven.m_tutorialsEnabled = customTutorial != null;
        }
    }
    
    [HarmonyPatch(typeof(Raven), nameof(Raven.Spawn))]
    private static class RavenSpawnPostfix
    {
        private static void Postfix(Raven __instance, Raven.RavenText text)
        {
            if (!__instance) return;
            if (CustomTutorialsPlugin._OverrideSettings.Value is CustomTutorialsPlugin.Toggle.Off) return;
            Raven.m_tutorialsEnabled = false;
        }
    }
}