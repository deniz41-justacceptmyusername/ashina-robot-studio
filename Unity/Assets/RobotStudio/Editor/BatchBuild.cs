using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
namespace AshinaStudio.Editor {
 public static class BatchBuild {
  public static void Run(){try{var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);new GameObject("Robot Studio").AddComponent<StudioApp>();Directory.CreateDirectory("Assets/RobotStudio/Scenes");EditorSceneManager.SaveScene(scene,"Assets/RobotStudio/Scenes/Studio.unity");EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/RobotStudio/Scenes/Studio.unity",true)};
   PlayerSettings.companyName="Ashina Robotics";PlayerSettings.productName="Ashina Robot Studio";PlayerSettings.bundleVersion="0.4.6";PlayerSettings.defaultScreenWidth=1440;PlayerSettings.defaultScreenHeight=900;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.resizableWindow=true;PlayerSettings.runInBackground=true;PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);PlayerSettings.SetApiCompatibilityLevel(UnityEditor.Build.NamedBuildTarget.Standalone,ApiCompatibilityLevel.NET_Standard);PlayerSettings.colorSpace=ColorSpace.Gamma;PlayerSettings.usePlayerLog=true;PlayerSettings.stripEngineCode=false;
   var inputAsset=AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset").FirstOrDefault();if(inputAsset!=null){var input=new SerializedObject(inputAsset);var axes=input.FindProperty("m_Axes");string[] names={"Mouse X","Mouse Y","PadLeftX","PadLeftY","PadRightX"};axes.arraySize=names.Length;for(int i=0;i<names.Length;i++){var axis=axes.GetArrayElementAtIndex(i);axis.FindPropertyRelative("m_Name").stringValue=names[i];axis.FindPropertyRelative("descriptiveName").stringValue="";axis.FindPropertyRelative("negativeButton").stringValue="";axis.FindPropertyRelative("positiveButton").stringValue="";axis.FindPropertyRelative("altNegativeButton").stringValue="";axis.FindPropertyRelative("altPositiveButton").stringValue="";axis.FindPropertyRelative("gravity").floatValue=0;axis.FindPropertyRelative("dead").floatValue=i<2?0:.12f;axis.FindPropertyRelative("sensitivity").floatValue=i<2?.1f:1;axis.FindPropertyRelative("snap").boolValue=false;axis.FindPropertyRelative("invert").boolValue=false;axis.FindPropertyRelative("type").intValue=i<2?1:2;axis.FindPropertyRelative("axis").intValue=i==0||i==2?0:i==1||i==3?1:3;axis.FindPropertyRelative("joyNum").intValue=1;}input.ApplyModifiedProperties();}
   var graphics=AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset").FirstOrDefault();if(graphics!=null){var obj=new SerializedObject(graphics);var array=obj.FindProperty("m_AlwaysIncludedShaders");var shaders=new[]{"Standard","Unlit/Texture","Sprites/Default","glTF/PbrMetallicRoughness","glTF/PbrSpecularGlossiness","glTF/Unlit"}.Select(Shader.Find).Where(s=>s!=null).ToArray();var existing=Enumerable.Range(0,array.arraySize).Select(i=>array.GetArrayElementAtIndex(i).objectReferenceValue as Shader).Where(s=>s!=null);var unique=existing.Concat(shaders).Distinct().ToArray();array.arraySize=unique.Length;for(int i=0;i<unique.Length;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=unique[i];obj.ApplyModifiedProperties();}
   var icon=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RobotStudio/Resources/AshinaIcon.png");if(icon!=null){PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown,new[]{icon},IconKind.Any);PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Standalone,new Texture2D[0],IconKind.Any);}
   AssetDatabase.SaveAssets();string output=Environment.GetEnvironmentVariable("ASHINA_UNITY_OUTPUT")??Path.GetFullPath(Path.Combine(Application.dataPath,"../../Windows/RobotStudio.exe"));Directory.CreateDirectory(Path.GetDirectoryName(output));var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/RobotStudio/Scenes/Studio.unity"},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});File.WriteAllText(Path.GetFullPath("../unity-build-result.json"),JsonUtility.ToJson(new BuildSummary{result=report.summary.result.ToString(),errors=report.summary.totalErrors,warnings=report.summary.totalWarnings,bytes=report.summary.totalSize,path=output},true));EditorApplication.Exit(report.summary.result==BuildResult.Succeeded?0:1);
  }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
  [Serializable]class BuildSummary{public string result,path;public int errors,warnings;public ulong bytes;}
 }
}





