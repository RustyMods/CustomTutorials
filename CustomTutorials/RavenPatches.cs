using HarmonyLib;
using UnityEngine;

namespace CustomTutorials;

public static class RavenPatches
{
    private static readonly int Teleporting = Animator.StringToHash("teleportin");
    private static readonly int Flying = Animator.StringToHash("flyin");

    [HarmonyPatch(typeof(Raven), nameof(Raven.Spawn))]
    private static class Raven_Spawn_Patch
    {
        private static void Postfix(Raven __instance, Raven.RavenText text, bool forceTeleport)
        {
            if (!__instance) return;
            if (Utils.GetMainCamera() == null) return;
            if (Raven.m_tutorialsEnabled) return;
            // if (CustomTutorialsPlugin._enabled.Value is CustomTutorialsPlugin.Toggle.Off) return;
            if (!TutorialManager.m_customTutorials.ContainsKey(text.m_key)) return;
            __instance.m_groundObject = text.m_guidePoint.gameObject;
            __instance.transform.position = text.m_guidePoint.transform.position;

            __instance.m_currentText = text;
            __instance.m_hasTalked = false;
            __instance.m_randomTextTimer = 99999f;

            if (__instance.m_currentText.m_key.Length > 0 &&
                Player.m_localPlayer.HaveSeenTutorial(__instance.m_currentText.m_key))
            {
                __instance.m_hasTalked = true;
            }

            Vector3 forward = (Player.m_localPlayer.transform.position - __instance.transform.position) with
            {
                y = 0.0f
            };
            forward.Normalize();
                
            __instance.transform.rotation = Quaternion.LookRotation(forward);

            if (forceTeleport)
            {
                __instance.m_animator.SetTrigger(Teleporting);
            }
            else if (text.m_static)
            {
                __instance.m_animator.SetTrigger(__instance.IsUnderRoof() ? Teleporting : Flying);
            }
            else
            {
                __instance.m_animator.SetTrigger(Flying);
            }
        }
    }
}