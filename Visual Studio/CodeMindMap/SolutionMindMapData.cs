using System;
using System.Diagnostics;
using System.IO;

namespace CodeMindMap
{
    public class SolutionMindMapData
    {
        public SolutionMindMapData()
        {

        }

        public SolutionMindMapData(string id, string solutionFilePath, string appDataFolderPath)
        {
            Id = id;
            SolutionFilePath = solutionFilePath;

            var dirName = Path.GetRandomFileName();
            var dirPath = Path.Combine(appDataFolderPath, dirName);


            try
            {
                if (!Directory.Exists(dirPath))
                {
                    Directory.CreateDirectory(dirPath);
                }

                MindMapDataFilePath = Path.Combine(dirPath, DefaultDataFileName);
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Error default mind map data dir: {exception.Message}");
            }
        }

        public const string DefaultDataFileName = "CodeMindMap.txt";

        public string Id { get; set; }
        public string SolutionFilePath { get; set; }
        public string MindMapDataFilePath { get; set; }

        public bool IsEmpty => string.IsNullOrEmpty(Id);
    }
}
