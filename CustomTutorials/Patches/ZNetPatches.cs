using CustomTutorials.Configurations;
using HarmonyLib;

namespace CustomTutorials.Patches;

public static class ZNetPatches
{
    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Awake))]
    private static class ZNetAwakePatch
    {
        private static void Postfix(ZNet __instance)
        {
            if (!__instance) return;
            TutorialManager.InitServerTutorials(__instance);
        }
    }
}