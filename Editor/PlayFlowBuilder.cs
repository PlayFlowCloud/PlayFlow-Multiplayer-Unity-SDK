using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using ICSharpCode.SharpZipLib.Zip;
using ICSharpCode.SharpZipLib.Zip.Compression;
using UnityEngine;



public class PlayFlowBuilder
{

    public static string defaultPath = @"Builds" + Path.DirectorySeparatorChar + "Linux" + Path.DirectorySeparatorChar +
                                       "Server" + Path.DirectorySeparatorChar + "PlayFlowCloud" +
                                       Path.DirectorySeparatorChar + "PlayFlowCloudServerFiles" +
                                       Path.DirectorySeparatorChar + "Server.x86_64";
    public static bool BuildServer(bool devmode, List<string> sceneList)
    {
        bool success = false;
        try
        {
            EditorUtility.DisplayProgressBar("PlayFlowCloud", "Build Linux Server", 0.25f);
            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
            buildPlayerOptions.scenes = sceneList.ToArray();
            buildPlayerOptions.locationPathName = defaultPath;
            buildPlayerOptions.target = BuildTarget.StandaloneLinux64;

#if UNITY_2021_2_OR_NEWER
            if (Application.unityVersion.CompareTo(("2021.2")) >= 0)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone,
                    BuildTarget.StandaloneLinux64);
                EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Server;
                buildPlayerOptions.subtarget = (int) StandaloneBuildSubtarget.Server;
                if (devmode)
                {
                    buildPlayerOptions.options = BuildOptions.CompressWithLz4HC | BuildOptions.Development;
                }
                else
                {
                    buildPlayerOptions.options = BuildOptions.CompressWithLz4HC;
                }
            }
#else
        buildPlayerOptions.options = BuildOptions.CompressWithLz4HC | BuildOptions.EnableHeadlessMode;
        
        if (devmode)
        {
            buildPlayerOptions.options =
 BuildOptions.CompressWithLz4HC | BuildOptions.Development | BuildOptions.EnableHeadlessMode;
        }

#endif


            var report = BuildPipeline.BuildPlayer(buildPlayerOptions);

            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                success = true;
            }
            else
            {
                Debug.LogError("BuildServer failed: " + report.summary.result);
            }
        }

        catch (Exception e)
        {
            Debug.Log(e.StackTrace);
            success = false;
        }

        finally
        {
            EditorUtility.ClearProgressBar();
        }
        return success;
    }

    public static string ZipServerBuild()
    {
        //Display a progress bar in a try catch finally block
        try
        {
            // Display a progress bar
            EditorUtility.DisplayProgressBar("PlayFlowCloud", "Zipping Server Build", 0.5f);
            string directoryToZip = Path.GetDirectoryName(defaultPath);
            string zipFile = "";
            if (Directory.Exists(directoryToZip))
            {
                string targetfile = Path.Combine(directoryToZip, @".." + Path.DirectorySeparatorChar +  "Server.zip");
                zipFile = ZipPath(targetfile, directoryToZip, null, true, null);
            }

            return zipFile;
        }
        catch (Exception e)
        {
            Debug.Log(e.StackTrace);
            EditorUtility.ClearProgressBar();
            return null;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    public static string ZipPath(string zipFilePath, string sourceDir, string pattern, bool withSubdirs, string password)
    {
        FastZip fz = new FastZip();
        fz.CompressionLevel = Deflater.CompressionLevel.DEFAULT_COMPRESSION;
        fz.CreateZip(zipFilePath, sourceDir, withSubdirs, pattern);
        return zipFilePath;
    }

    public static void cleanUp(string zipFilePath)
    {
        string sourceDir = Path.GetDirectoryName(defaultPath);
        if (Directory.Exists(sourceDir))
        {
            Directory.Delete(sourceDir, true);
        }

        if (File.Exists(zipFilePath))
        {
            File.Delete(zipFilePath);
        }

    }
}