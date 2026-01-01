using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell.Settings;
using System;
using System.Collections.Generic;

namespace CodeMindMap
{
    internal class MindMapSettingsManager
    {
        private const string CollectionName = "SolutionMindMaps";
        private const string SettingsPropertyName = "MindMapsData";
        private readonly WritableSettingsStore _settingsStore;

        public MindMapSettingsManager(IServiceProvider serviceProvider)
        {
            var shellSettingsManager = new ShellSettingsManager(serviceProvider);
            _settingsStore = shellSettingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);

            // Ensure our collection exists
            if (!_settingsStore.CollectionExists(CollectionName))
            {
                _settingsStore.CreateCollection(CollectionName);
            }
        }

        public void SaveSolutionMindMapData(List<SolutionMindMapData> mindMaps)
        {
            // Convert list to XML for storage
            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(List<SolutionMindMapData>));
            using (var writer = new System.IO.StringWriter())
            {
                serializer.Serialize(writer, mindMaps);
                _settingsStore.SetString(CollectionName, SettingsPropertyName, writer.ToString());
            }
        }

        public List<SolutionMindMapData> LoadSolutionMindMapData()
        {
            if (!_settingsStore.PropertyExists(CollectionName, SettingsPropertyName))
            {
                return new List<SolutionMindMapData>();
            }

            try
            {
                string serializedData = _settingsStore.GetString(CollectionName, SettingsPropertyName);

                // Convert XML back to list
                var serializer = new System.Xml.Serialization.XmlSerializer(typeof(List<SolutionMindMapData>));
                using (var reader = new System.IO.StringReader(serializedData))
                {
                    try
                    {
                        return (List<SolutionMindMapData>)serializer.Deserialize(reader);
                    }
                    catch
                    {
                        // Return empty list if deserialization fails
                        return new List<SolutionMindMapData>();
                    }
                }
            }
            catch (Exception)
            {
                return new List<SolutionMindMapData>();
            }
        }
    }
}
