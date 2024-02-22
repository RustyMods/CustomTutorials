using System.Collections.Generic;
using System.Linq;
using BepInEx;
using CustomTutorials.Configurations;
using HarmonyLib;
using UnityEngine;

namespace CustomTutorials.Patches;

public static class ZNetScenePatches
{
    private static bool CustomTutorialsLoaded;

    [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
    private static class PlayerOnSpawnedPatch
    {
        private static void Postfix(Player __instance)
        {
            if (!__instance) return;
            if (CustomTutorialsLoaded) return;
            if (!ZNetScene.instance) return;
            AddCustomTutorials(ZNetScene.instance);
            CustomTutorialsLoaded = true;
        }
    }

    private static void AddCustomTutorials(ZNetScene __instance)
    {
        if (!__instance) return;
        GameObject? ravens = GetRavens();
        if (ravens == null) return;
        int count = 0;
        foreach (TutorialData tutorial in TutorialManager.customTutorials)
        {
            if (tutorial.m_prefab.IsNullOrWhiteSpace()) continue;
            GameObject prefab = __instance.GetPrefab(tutorial.m_prefab);
            if (!prefab)
            {
                CustomTutorialsPlugin.CustomTutorialsLogger.LogDebug("Failed to find prefab: " + tutorial.m_prefab);
                continue;
            }

            GuidePoint guide = prefab.GetComponentInChildren<GuidePoint>();
            if (guide)
            {
                CustomTutorialsPlugin.CustomTutorialsLogger.LogDebug("Prefab already has tutorial: " + tutorial.m_prefab);
                continue;
            }

            Renderer renderer = prefab.GetComponentInChildren<Renderer>();
            if (!renderer)
            {
                CustomTutorialsPlugin.CustomTutorialsLogger.LogDebug("Failed to find renderer: " + tutorial.m_prefab);
                continue;
            }

            GameObject guidePoint = new GameObject("guide");
            guidePoint.transform.SetParent(prefab.transform);
            guidePoint.transform.position = prefab.transform.position + new Vector3(0f, renderer.bounds.size.y, 0f);
            GuidePoint component = guidePoint.AddComponent<GuidePoint>();
            component.m_ravenPrefab = ravens;
            component.m_text = new Raven.RavenText()
            {
                m_alwaysSpawn = tutorial.m_alwaysSpawn,
                m_munin = tutorial.m_munin,
                m_priority = tutorial.m_priority,
                m_key = tutorial.m_key,
                m_topic = tutorial.m_topic,
                m_label = tutorial.m_label,
                m_text = tutorial.m_text,
            };

            ++count;
        }
        CustomTutorialsPlugin.CustomTutorialsLogger.LogDebug("Successfully added " + count + " custom tutorials");
    }
    
    private static GameObject? GetRavens()
    {
        List<GameObject> allObjects = Resources.FindObjectsOfTypeAll<GameObject>().ToList();
        GameObject Ravens =
            allObjects.Find(item => item.name == "Ravens" && item.transform.GetChild(0).name == "Hugin");
        if (!Ravens) return null;

        return Ravens;
    }
}