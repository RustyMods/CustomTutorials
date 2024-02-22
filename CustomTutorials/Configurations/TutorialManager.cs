using System.Collections.Generic;
using System.IO;
using BepInEx;
using ServerSync;
using YamlDotNet.Serialization;

namespace CustomTutorials.Configurations;

public static class TutorialManager
{
    private static readonly string FolderPath = Paths.ConfigPath + Path.DirectorySeparatorChar + "CustomTutorials";
    private static readonly string ExampleFilePath = FolderPath + Path.DirectorySeparatorChar + "Example.yml";

    private static readonly CustomSyncedValue<string> ServerTutorials = new(CustomTutorialsPlugin.ConfigSync, "ServerTutorials", "");

    public static List<TutorialData> customTutorials = new();

    public static void InitServerTutorials(ZNet instance)
    {
        CustomTutorialsPlugin.CustomTutorialsLogger.LogDebug("Initializing Server Custom Tutorials");
        if (instance.IsServer())
        {
            ISerializer serializer = new SerializerBuilder().Build();
            string data = serializer.Serialize(customTutorials);
            ServerTutorials.Value = data;
        }
        else
        {
            ServerTutorials.ValueChanged += OnServerTutorialChange;
        }
    }

    private static void OnServerTutorialChange()
    {
        IDeserializer deserializer = new DeserializerBuilder().Build();
        List<TutorialData> serverTutorials = deserializer.Deserialize<List<TutorialData>>(ServerTutorials.Value);
        CustomTutorialsPlugin.CustomTutorialsLogger.LogDebug("Client: Received " + serverTutorials.Count + " custom tutorials from server");
        customTutorials = serverTutorials;
    }

    public static void InitCustomTutorials()
    {
        customTutorials.Clear();
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
                customTutorials.Add(tutorialData);
            }
            catch
            {
                CustomTutorialsPlugin.CustomTutorialsLogger.LogDebug("Failed to deserialize: " + path);
            }
        }
    }
}