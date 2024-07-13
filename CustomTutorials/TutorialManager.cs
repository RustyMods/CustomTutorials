using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using ServerSync;
using UnityEngine;
using YamlDotNet.Serialization;

namespace CustomTutorials;

public static class TutorialManager
{
    private static readonly string FolderPath = Paths.ConfigPath + Path.DirectorySeparatorChar + "CustomTutorials";
    private static readonly string ExampleFilePath = FolderPath + Path.DirectorySeparatorChar + "Example.yml";
    private static readonly CustomSyncedValue<string> m_serverTutorials = new(CustomTutorialsPlugin.ConfigSync, "ServerTutorials", "");
    private static GameObject? m_ravens;
    private static bool m_tutorialsInitialized;
    private static bool m_onServerChangeInitialized;
    
    public static Dictionary<string, TutorialData> m_customTutorials = new();
    private static readonly Dictionary<string, Raven.RavenText> m_labeledTutorials = new();
    private static readonly HashSet<string> m_registeredTutorials = new();

    [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
    private static class RegisterTutorials
    {
        private static void Postfix(Player __instance)
        {
            if (!__instance) return;
            if (m_tutorialsInitialized) return;
            if (!ZNetScene.instance) return;
            InitializeCustomTutorials(true);
            m_tutorialsInitialized = true;
        }
    }
    
    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Awake))]
    private static class SendTutorialsToClients
    {
        private static void Postfix(ZNet __instance)
        {
            if (!__instance) return;
            UpdateServerData();
        }
    }

    private static bool UpdateServerData()
    {
        if (!IsServer()) return false;
        try
        {
            ISerializer serializer = new SerializerBuilder().Build();
            string data = serializer.Serialize(m_customTutorials);
            m_serverTutorials.Value = data;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void InitializeCustomTutorials(bool updateServer = false)
    {
        int count = m_customTutorials.Values.Count(AddTutorial);
        CustomTutorialsPlugin.CustomTutorialsLogger.LogDebug("Successfully added " + count + " custom tutorials");
        if (!updateServer) return;
        if (UpdateServerData())
        {
            CustomTutorialsPlugin.CustomTutorialsLogger.LogDebug("Sent tutorials to clients");
        }
    }

    private static bool AddTutorial(TutorialData tutorial)
    {
        if (!ZNetScene.instance) return false;
        if (tutorial.m_prefab.IsNullOrWhiteSpace()) return false;
        GameObject prefab = ZNetScene.instance.GetPrefab(tutorial.m_prefab);
        if (!prefab)
        {
            CustomTutorialsPlugin.CustomTutorialsLogger.LogDebug("Failed to find prefab: " + tutorial.m_prefab);
            return false;
        }
        
        Renderer renderer = prefab.GetComponentInChildren<Renderer>();
        if (!renderer)
        {
            CustomTutorialsPlugin.CustomTutorialsLogger.LogDebug("Failed to find renderer: " + tutorial.m_prefab);
            return false;
        }

        GuidePoint guide = prefab.GetComponentInChildren<GuidePoint>();

        if (!guide)
        {
            GameObject guidePoint = new GameObject("guide");
            guidePoint.transform.SetParent(prefab.transform);
            guidePoint.transform.position = prefab.transform.position + new Vector3(0f, renderer.bounds.size.y, 0f);
            guide = guidePoint.AddComponent<GuidePoint>();
            guide.m_ravenPrefab = GetRavens();;
        }
        
        guide.m_text = new Raven.RavenText()
        {
            m_alwaysSpawn = tutorial.m_alwaysSpawn,
            m_munin = tutorial.m_munin,
            m_priority = tutorial.m_priority,
            m_key = tutorial.m_key,
            m_topic = tutorial.m_topic,
            m_label = tutorial.m_label,
            m_text = tutorial.m_text,
        };

        m_labeledTutorials[guide.m_text.m_label] = guide.m_text;
        CustomTutorialsPlugin.CustomTutorialsLogger.LogDebug("Added tutorial for " + prefab.name);
        m_registeredTutorials.Add(prefab.name);
        return true;
    }

    private static GameObject? GetRavens()
    {
        if (m_ravens) return m_ravens;
        List<GameObject> allObjects = Resources.FindObjectsOfTypeAll<GameObject>().ToList();
        GameObject Ravens = allObjects.Find(item => item.name == "Ravens" && item.transform.GetChild(0).name == "Hugin");
        if (Ravens != null) m_ravens = Ravens;
        return !Ravens ? null : Ravens;
    }
    
    public static void InitServerTutorials()
    {
        if (m_onServerChangeInitialized) return;
        CustomTutorialsPlugin.CustomTutorialsLogger.LogDebug("Initializing Server Sync");
        m_serverTutorials.ValueChanged += OnServerDataChange;
        m_onServerChangeInitialized = true;
    }

    private static void OnServerDataChange()
    {
        try
        {
            if (IsServer()) return;
            if (m_serverTutorials.Value.IsNullOrWhiteSpace()) return;
            IDeserializer deserializer = new DeserializerBuilder().Build();
            Dictionary<string, TutorialData> serverTutorials = deserializer.Deserialize<Dictionary<string, TutorialData>>(m_serverTutorials.Value);
            CustomTutorialsPlugin.CustomTutorialsLogger.LogDebug("Client: Received " + serverTutorials.Count + " custom tutorials from server");
            m_customTutorials = serverTutorials;
            if (ClearAllTutorials()) InitializeCustomTutorials();
        }
        catch
        {
            CustomTutorialsPlugin.CustomTutorialsLogger.LogDebug("Failed to receive server tutorials");
        }
    }

    public static void ReadFiles()
    {
        m_customTutorials.Clear();
        if (!Directory.Exists(FolderPath)) Directory.CreateDirectory(FolderPath);
        if (!File.Exists(ExampleFilePath))
        {
            ISerializer serializer = new SerializerBuilder().Build();
            string exampleData = serializer.Serialize(new TutorialData()
            {
                m_prefab = "Dandelion",
                m_alwaysSpawn = false,
                m_munin = true,
                m_priority = 0,
                m_key = "DandelionKey",
                m_topic = "DandelionAreUseless",
                m_label = "DandelionInfo",
                m_text =
                    "Dandelions are found all around the meadows. They can aid in progressing through this world, but really are mostly useless."
            });
            File.WriteAllText(ExampleFilePath, exampleData);
        }
        string[] paths = Directory.GetFiles(FolderPath, "*.yml");
        foreach (string path in paths)
        {
            if (path.EndsWith("Example.yml")) continue;
            string data = File.ReadAllText(path);
            try
            {
                IDeserializer deserializer = new DeserializerBuilder().Build();
                TutorialData tutorialData = deserializer.Deserialize<TutorialData>(data);
                m_customTutorials[tutorialData.m_key] = tutorialData;
            }
            catch
            {
                CustomTutorialsPlugin.CustomTutorialsLogger.LogDebug("Failed to deserialize: " + path);
            }
        }
    }

    private static bool ClearAllTutorials()
    {
        if (!ZNetScene.instance) return false;
        int count = 0;
        foreach (string name in m_registeredTutorials)
        {
            GameObject prefab = ZNetScene.instance.GetPrefab(name);
            if (!prefab) continue;
            if (!RemoveTutorial(ref prefab)) continue;
            ++count;
        }
        m_registeredTutorials.Clear();
        CustomTutorialsPlugin.CustomTutorialsLogger.LogDebug($"Cleared {count} tutorials");
        return true;
    }

    private static bool RemoveTutorial(ref GameObject prefab)
    {
        GuidePoint guide = prefab.GetComponentInChildren<GuidePoint>();
        if (!guide) return false;
        Object.Destroy(guide.gameObject);
        return true;
    }

    public static void SetupFileWatcher()
    {
        FileSystemWatcher watcher = new FileSystemWatcher(FolderPath, "*.yml");
        watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
        watcher.EnableRaisingEvents = true;
        watcher.Changed += OnFileChange;
        watcher.Deleted += OnFileDeleted;
        watcher.Created += OnFileChange;
    }

    private static bool IsServer() => !ZNet.instance || ZNet.instance.IsServer();
    
    private static void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        CustomTutorialsPlugin.CustomTutorialsLogger.LogDebug($"{Path.GetFileName(e.FullPath)} deleted");
        if (!IsServer())
        {
            CustomTutorialsPlugin.CustomTutorialsLogger.LogDebug("Not server - ignoring");
            return;
        }
        if (ClearAllTutorials()) InitializeCustomTutorials(true);
    }

    private static void OnFileChange(object sender, FileSystemEventArgs e)
    {
        CustomTutorialsPlugin.CustomTutorialsLogger.LogDebug($"{Path.GetFileName(e.FullPath)} changed, updating tutorials");
        if (!IsServer())
        {
            CustomTutorialsPlugin.CustomTutorialsLogger.LogDebug("Not server - ignoring");
            return;
        }
        try
        {
            if (!ZNetScene.instance) return;
            string file = File.ReadAllText(e.FullPath);
            IDeserializer deserializer = new DeserializerBuilder().Build();
            TutorialData tutorial = deserializer.Deserialize<TutorialData>(file);

            GameObject prefab = ZNetScene.instance.GetPrefab(tutorial.m_prefab);
            GuidePoint guide = prefab.GetComponentInChildren<GuidePoint>();
            if (guide)
            {
                guide.m_text = new Raven.RavenText()
                {
                    m_alwaysSpawn = tutorial.m_alwaysSpawn,
                    m_munin = tutorial.m_munin,
                    m_priority = tutorial.m_priority,
                    m_key = tutorial.m_key,
                    m_topic = tutorial.m_topic,
                    m_label = tutorial.m_label,
                    m_text = tutorial.m_text,
                };
                m_customTutorials[tutorial.m_key] = tutorial;
            }
            else
            {
                AddTutorial(tutorial);
            }
            if (UpdateServerData())
            {
                CustomTutorialsPlugin.CustomTutorialsLogger.LogDebug("Sent tutorials to clients");
            }
        }
        catch
        {
            CustomTutorialsPlugin.CustomTutorialsLogger.LogDebug("Failed to update tutorial");
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.GetKnownTexts))]
    private static class Player_GetKnownTexts_Patch
    {
        private static void Prefix(Player __instance)
        {
            if (__instance.m_knownTexts.Count == 0) return;
            foreach (KeyValuePair<string, Raven.RavenText> kvp in m_labeledTutorials.Where(kvp => __instance.m_knownTexts.ContainsKey(kvp.Key)))
            {
                __instance.m_knownTexts[kvp.Key.Replace("\u0016", "")] = kvp.Value.m_text;
            }
        }
    }
}