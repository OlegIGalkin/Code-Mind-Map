using System;
using System.IO;

namespace CodeMindMap
{
    internal static class RelativePathHelper
    {
        public static bool IsFileInSolution(string solutionPath, string filePath)
        {
            if (string.IsNullOrEmpty(solutionPath) || string.IsNullOrEmpty(filePath))
                return false;

            try
            {
                string solutionDirectory = Path.GetDirectoryName(solutionPath);
                string fullSolutionDir = Path.GetFullPath(solutionDirectory);
                string fullFilePath = Path.GetFullPath(filePath);

                return fullFilePath.StartsWith(fullSolutionDir, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static string GetRelativePathFromSolution(string solutionPath, string filePath)
        {
            if (!IsFileInSolution(solutionPath, filePath))
                return null;

            try
            {
                string solutionDirectory = Path.GetDirectoryName(solutionPath);
                string fullSolutionDir = Path.GetFullPath(solutionDirectory);
                string fullFilePath = Path.GetFullPath(filePath);

                // Ensure the solution directory path ends with a separator
                if (!fullSolutionDir.EndsWith(Path.DirectorySeparatorChar.ToString()))
                    fullSolutionDir += Path.DirectorySeparatorChar;

                Uri solutionUri = new Uri(fullSolutionDir);
                Uri fileUri = new Uri(fullFilePath);

                Uri relativeUri = solutionUri.MakeRelativeUri(fileUri);
                string relativePath = Uri.UnescapeDataString(relativeUri.ToString());

                return relativePath.Replace('/', Path.DirectorySeparatorChar);
            }
            catch
            {
                return null;
            }
        }

        public static string GetFullFilePath(string solutionPath, string filePath)
        {
            if (string.IsNullOrEmpty(solutionPath) || string.IsNullOrEmpty(filePath))
                return filePath;

            try
            {
                if (Path.IsPathRooted(filePath))
                {
                    return !File.Exists(filePath) && IsFileInSolution(solutionPath, filePath)
                        ? GetRelativePathFromSolution(solutionPath, filePath)
                        : Path.GetFullPath(filePath);
                }

                // Handle relative paths - combine with solution directory
                string solutionDirectory = Path.GetDirectoryName(solutionPath);
                string fullSolutionDir = Path.GetFullPath(solutionDirectory);

                // Combine the paths and get the full path
                string combinedPath = Path.Combine(fullSolutionDir, filePath);
                string fullPath = Path.GetFullPath(combinedPath);

                return fullPath;
            }
            catch (Exception)
            {
                return filePath;
            }
        }
    }
}
