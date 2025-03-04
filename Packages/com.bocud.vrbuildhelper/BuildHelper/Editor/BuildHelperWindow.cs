﻿/* MIT License
 Copyright (c) 2021 BocuD (github.com/BocuD)

 Permission is hereby granted, free of charge, to any person obtaining a copy
 of this software and associated documentation files (the "Software"), to deal
 in the Software without restriction, including without limitation the rights
 to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 copies of the Software, and to permit persons to whom the Software is
 furnished to do so, subject to the following conditions:

 The above copyright notice and this permission notice shall be included in all
 copies or substantial portions of the Software.

 THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BocuD.VRChatApiTools;
using BuildHelper.Editor;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.Core;
using VRC.SDK3.Components;
using VRC.SDKBase.Editor;
using VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using static BocuD.VRChatApiTools.VRChatApiTools;

namespace BocuD.BuildHelper.Editor
{
    public class BuildHelperWindow : EditorWindow
    {
        private GUIStyle styleHelpBox;
        private GUIStyle styleBox;
        private GUIStyle modifiedImageBackground;

        private GUIContent activeWindowsTarget;
        private GUIContent activeAndroidTarget;
        private GUIContent switchToWindowsTarget;
        private GUIContent switchToAndroidTarget;

        private GUIContent openWebsite;

        private GUIContent makePrivateButton;
        private GUIContent makePublicButton;

        private GUIContent addTagButton;

        private GUIContent localTestingHeader;
        private GUIContent buildOnlyHeader;
        private GUIContent publishHeader;

        private GUIContent refreshButton;

        private GUIContent lastBuildButton;
        private GUIContent newBuildButton;

        private GUIContent windowsTargetButton;
        private GUIContent androidTargetButton;

        private GUIContent deleteWorldButton;
        private GUIContent editButton;
        private GUIContent saveButton;
        private GUIContent cancelButton;
        private GUIContent replaceImageButton;
        private GUIContent setImageButton;
        private GUIContent cameraButton;
        private GUIContent revertImageButton;

        private GUIContent currentPlatformPublish;
        private GUIContent crossPlatformPublish;
        
        private GUIContent buildFolder;
        private GUIContent exportBuild;

        private Texture2D _iconGitHub;
        private Texture2D _iconVRChat;
        private Texture2D _iconCloud;
        private Texture2D _iconBuild;
        private Texture2D _iconSettings;

        private Dictionary<string, Texture2D> modifiedWorldImages = new Dictionary<string, Texture2D>();

        public const string version = "v1.0.0";

        private Vector2 scrollPosition;
        private bool settings;

        private PipelineManager pipelineManager;

        [MenuItem("Window/VR Build Helper")]
        public static void ShowWindow()
        {
            BuildHelperWindow window = GetWindow<BuildHelperWindow>();
            window.titleContent = new GUIContent("VR Build Helper");
            window.minSize = new Vector2(550, 650);
            window.Show();
        }

        private bool init = false;

        private void OnEnable()
        {
            buildHelperData = BuildHelperData.GetDataBehaviour();
            pipelineManager = FindObjectOfType<PipelineManager>();
            BuildHelperData.RunLastBuildChecks();

            if (buildHelperData)
            {
                branchStorageObject = buildHelperData.dataObject;

                InitBranchList();
            }

            init = true;
            editMode = false;
        }

        private void OnGUI()
        {
            if (BuildPipeline.isBuildingPlayer) return;

            if (init)
            {
                GetUIAssets();
                InitializeStyles();
                InitGameObjectContainerLists();
                init = false;
            }

            DrawBanner();

            if (DrawSettings()) return;

            if (buildHelperData == null)
            {
                OnEnable();

                if (buildHelperData == null)
                {
                    EditorGUILayout.HelpBox("Build Helper has not been set up in this scene.", MessageType.Info);

                    if (GUILayout.Button("Set up Build Helper in this scene"))
                    {
                        if (FindObjectOfType<VRCSceneDescriptor>())
                            ResetData();
                        else
                        {
                            if (EditorUtility.DisplayDialog("Build Helper",
                                    "The scene currently does not contain a scene descriptor. For VR Build Helper to work, a scene descriptor needs to be present. Should VR Build Helper create one automatically?",
                                    "Yes", "No"))
                            {
                                CreateSceneDescriptor();
                                ResetData();
                            }
                            else
                            {
                                ResetData();
                            }
                        }
                    }
                    else return;
                }
            }

            if (branchStorageObject == null)
            {
                OnEnable();
            }

            DrawUpgradeUI();

            buildHelperDataSO.Update();
            branchList.DoLayoutList();
            buildHelperDataSO.ApplyModifiedProperties();

            if (branchStorageObject.branches.Length == 0)
            {
                GUIStyle welcomeLabel = new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold, fontSize = 20 };
                GUIStyle textArea = new GUIStyle(EditorStyles.label) { wordWrap = true, richText = true };

                EditorGUILayout.BeginVertical("Helpbox");
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Welcome to VR Build Helper", welcomeLabel, GUILayout.Height(23));
                EditorGUILayout.LabelField(
                    "VR Build Helper is an integrated editor toolset that adds a number of quality of life features to assist in managing your project. In practice it will function as a replacement for the VRChat SDK Control panel for 99% of tasks.",
                    textArea);
                GUILayout.Space(5);
                EditorGUILayout.LabelField(
                    "To get started, create a new branch. For documentation, please visit the wiki on GitHub.",
                    textArea);
                EditorGUILayout.Space(2);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Create new branch"))
                {
                    Undo.RecordObject(buildHelperData, "Create new branch");

                    PipelineManager manager = FindPipelineManager();

                    string blueprintID = "";
                    if (manager != null && Regex.IsMatch(manager.blueprintId, world_regex))
                    {
                        if (EditorUtility.DisplayDialog("Build Helper",
                                "An existing scene descriptor was found in the scene. Do you want to use its blueprint ID for your initial branch?",
                                "Yes (recommended)", "No"))
                        {
                            blueprintID = manager.blueprintId;
                        }
                    }

                    Branch newBranch = new Branch
                    {
                        name = "main", buildData = new BuildData(), branchID = BuildHelperData.GetUniqueID(),
                        blueprintID = blueprintID
                    };
                    ArrayUtility.Add(ref branchStorageObject.branches, newBranch);

                    branchList.index = Array.IndexOf(branchStorageObject.branches, newBranch);
                    TrySave();
                }

                if (GUILayout.Button("Open Wiki"))
                {
                    Application.OpenURL("https://github.com/BocuD/VRBuildHelper/wiki/Getting-Started");
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
            }

            if (branchStorageObject.currentBranch >= branchStorageObject.branches.Length)
                branchStorageObject.currentBranch = 0;

            DrawSwitchBranchButton();

            DrawBranchUpgradeUI();

            PipelineChecks();

            if (SceneChecks()) return;

            if (branchList.index != -1 && branchStorageObject.branches.Length > 0)
            {
                scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, false, GUIStyle.none,
                    GUI.skin.verticalScrollbar);

                DrawBranchEditor();

                GUILayout.EndScrollView();

                DisplayBuildButtons();
            }
        }

        private bool DrawSettings()
        {
            if (!settings) return false;

            GUIStyle labelStyle = new GUIStyle(GUI.skin.label) { richText = true };
            EditorGUILayout.LabelField("<b>VR Build Helper Options</b>", labelStyle);

            if (buildHelperData != null)
            {
                EditorGUILayout.BeginVertical("Helpbox");
                EditorGUILayout.LabelField("<b>Scene Options</b>", labelStyle);

                GUIContent[] mainPlatform =
                {
                    windowsTargetButton,
                    androidTargetButton
                };

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(
                    new GUIContent("Primary Platform",
                        "The primary platform you are developing for. For proper version management, when building for the secondary platform you should mark "),
                    GUILayout.Width(150));
                GUILayout.FlexibleSpace();
                buildHelperData.targetPlatform =
                    (Platform)GUILayout.Toolbar((int)buildHelperData.targetPlatform, mainPlatform,
                        GUILayout.Width(250));
                EditorGUILayout.EndHorizontal();

                if (buildHelperData.gameObject.hideFlags == HideFlags.None)
                {
                    EditorGUILayout.HelpBox("The VRBuildHelper Data object is currently not hidden.",
                        MessageType.Warning);
                    if (GUILayout.Button("Hide VRBuildHelper Data object"))
                    {
                        buildHelperData.gameObject.hideFlags = HideFlags.HideInHierarchy;
                        EditorApplication.RepaintHierarchyWindow();
                    }
                }
                else
                {
                    if (GUILayout.Button("Show VRBuildHelper Data object (Not recommended)"))
                    {
                        buildHelperData.gameObject.hideFlags = HideFlags.None;
                        EditorApplication.RepaintHierarchyWindow();
                    }
                }

                if (GUILayout.Button("Remove VRBuildHelper from this scene"))
                {
                    bool confirm = EditorUtility.DisplayDialog("Build Helper",
                        "Are you sure you want to remove Build Helper from this scene? All stored information will be lost permanently.",
                        "Yes",
                        "Cancel");

                    if (confirm)
                    {
                        buildHelperData = BuildHelperData.GetDataBehaviour();

                        if (buildHelperData != null)
                        {
                            buildHelperData.DeleteJSON();
                            DestroyImmediate(buildHelperData.gameObject);
                        }
                    }
                }

                EditorGUILayout.EndVertical();
                GUILayout.Space(10);
            }

            EditorGUILayout.BeginVertical("Helpbox");
            EditorGUILayout.LabelField("<b>Global VR Build Helper Options</b>", labelStyle);

            EditorGUILayout.BeginHorizontal();
            bool asyncPublishTemp = BuildHelperEditorPrefs.UseAsyncPublish;
            BuildHelperEditorPrefs.UseAsyncPublish =
                EditorGUILayout.Toggle(BuildHelperEditorPrefs.UseAsyncPublish, GUILayout.Width(15));
            if (asyncPublishTemp != BuildHelperEditorPrefs.UseAsyncPublish && BuildHelperEditorPrefs.UseAsyncPublish)
            {
                BuildHelperEditorPrefs.UseAsyncPublish = EditorUtility.DisplayDialog("Build Helper",
                    "Async publishing is a new feature of VRChat Api Tools that lets you build and publish your world without entering playmode. This may speed up your workflow significantly depending on how large your project is. It should already fully work as expected, but has not undergone as much testing as the rest of VR Build Helper. Do you want to use Async Publishing?",
                    "Enable", "Keep disabled");
            }

            EditorGUILayout.LabelField("Use Async Publisher (beta)");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            BuildHelperEditorPrefs.ShowBuildOnly =
                EditorGUILayout.Toggle(BuildHelperEditorPrefs.ShowBuildOnly, GUILayout.Width(15));
            EditorGUILayout.LabelField(new GUIContent("Always show build only options",
                "This will always show build only options instead of just when the target platform is Android"));
            EditorGUILayout.EndHorizontal();

            GUIContent[] buildNumberModes =
            {
                new GUIContent("On build", "The build number will be incremented on every new build"),
                new GUIContent("On upload", "The build number will only be incremented after every upload")
            };

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Increment build number", GUILayout.Width(150));
            GUILayout.FlexibleSpace();
            BuildHelperEditorPrefs.BuildNumberMode = GUILayout.Toolbar(BuildHelperEditorPrefs.BuildNumberMode,
                buildNumberModes, GUILayout.Width(250));
            EditorGUILayout.EndHorizontal();

            GUIContent[] platformSwitchModes =
            {
                new GUIContent("For every build",
                    "Build Helper will always ask you if it should match build numbers between the PC and Android versions."),
                new GUIContent("Only after switching",
                    "Build Helper will only ask to increment the build number right after switching to Android, and then increment automatically.")
            };
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("<i>When building for secondary platform</i>", labelStyle);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Ask to match build number", GUILayout.Width(250));
            GUILayout.FlexibleSpace();
            BuildHelperEditorPrefs.PlatformSwitchMode =
                GUILayout.Toolbar(BuildHelperEditorPrefs.PlatformSwitchMode, platformSwitchModes);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            GUILayout.Space(10);

            if (GUILayout.Button("Close"))
            {
                settings = false;
            }

            EditorGUILayout.LabelField($"VR Build Helper {version}");
            EditorGUILayout.LabelField($"<i>Made with ♡ by BocuD</i>",
                new GUIStyle(EditorStyles.label) { richText = true });
            return true;
        }

        private void DrawSwitchBranchButton()
        {
            if (pipelineManager == null) return;
            if (branchStorageObject.branches.Length <= 0 || branchList.index == -1) return;

            Rect buttonRectBase = GUILayoutUtility.GetLastRect();

            Rect buttonRect = new Rect(5, buttonRectBase.y, 250, EditorGUIUtility.singleLineHeight);

            bool buttonDisabled = true;
            if (branchStorageObject.currentBranch != branchList.index)
            {
                buttonDisabled = false;
            }
            else if (branchStorageObject.CurrentBranch.blueprintID != pipelineManager.blueprintId)
            {
                buttonDisabled = false;
            }

            EditorGUI.BeginDisabledGroup(buttonDisabled);

            if (GUI.Button(buttonRect, $"Switch to {branchStorageObject.branches[branchList.index].name}"))
            {
                SwitchBranch(buildHelperData, branchList.index);
            }

            EditorGUI.EndDisabledGroup();
        }

        private void DrawUpgradeUI()
        {
            if (buildHelperData.sceneID != "")
            {
                if (buildHelperData.overrideContainers != null &&
                    buildHelperData.overrideContainers.Length > 0)
                {
                    EditorGUILayout.HelpBox(
                        "This scene still uses the previous GameObject override save system. You will have to upgrade your old data in order to use it.",
                        MessageType.Error);
                    if (GUILayout.Button("Upgrade data"))
                    {
                        try
                        {
                            if (branchStorageObject.branches != null)
                            {
                                for (int i = 0;
                                     i < buildHelperData.overrideContainers.Length &&
                                     i < branchStorageObject.branches.Length;
                                     i++)
                                {
                                    branchStorageObject.branches[i].overrideContainer =
                                        buildHelperData.overrideContainers[i];

                                    OverrideContainer targetContainer = branchStorageObject.branches[i].overrideContainer;
                                    if (targetContainer.ExcludedGameObjects.Length > 0 ||
                                        targetContainer.ExclusiveGameObjects.Length > 0)
                                    {
                                        targetContainer.hasOverrides = true;
                                    }
                                }
                            }

                            buildHelperData.overrideContainers = null;

                            //reset override container serializedproperties and lists
                            InitGameObjectContainerLists();
                        }
                        catch (Exception e)
                        {
                            Logger.LogError($"Error occured while trying to convert data: {e.Message}");
                        }
                    }
                }

                return;
            }

            EditorGUILayout.HelpBox(
                "This scene still uses the non GUID based identifier for scene identification. You should consider upgrading using the button below.",
                MessageType.Warning);
            if (GUILayout.Button("Upgrade to GUID system"))
            {
                try
                {
                    buildHelperData.DeleteJSON();
                    buildHelperData.sceneID = BuildHelperData.GetUniqueID();

                    EditorSceneManager.SaveScene(buildHelperData.gameObject.scene);
                    Logger.Log(
                        $"Succesfully converted Build Helper data for scene {SceneManager.GetActiveScene().name} to GUID {buildHelperData.sceneID}");
                }
                catch (Exception e)
                {
                    Logger.LogError($"Error occured while trying to convert data to GUID system: {e.Message}");
                }
            }
        }

        private void DrawBranchUpgradeUI()
        {
            if (branchStorageObject.CurrentBranch == null || branchStorageObject.CurrentBranch.branchID != "") return;

            Branch currentBranch = branchStorageObject.CurrentBranch;

            EditorGUILayout.HelpBox(
                "This branch still uses the non GUID based identifier for branches and deployment data identification. You should consider upgrading using the button below. Please keep in mind that this process may not transfer over all deployment data (if any is present) perfectly.",
                MessageType.Warning);
            if (GUILayout.Button("Upgrade to GUID system"))
            {
                try
                {
                    DeploymentManager.RefreshDeploymentDataLegacy(currentBranch);

                    currentBranch.branchID = BuildHelperData.GetUniqueID();

                    if (string.IsNullOrEmpty(currentBranch.deploymentData.initialBranchName)) return;

                    foreach (DeploymentUnit unit in currentBranch.deploymentData.units)
                    {
                        //lol idk how this is part of udongraphextensions but suuure i'll use it here :P
                        string newFilePath = unit.filePath.ReplaceLast(
                            currentBranch.deploymentData.initialBranchName,
                            currentBranch.branchID);
                        Debug.Log($"Renaming {unit.filePath} to {newFilePath}");

                        File.Move(unit.filePath, newFilePath);
                    }

                    currentBranch.deploymentData.initialBranchName = "unused";
                    TrySave();
                    Logger.Log($"Succesfully converted branch '{currentBranch}' to GUID system");
                }
                catch (Exception e)
                {
                    Logger.LogError($"Error occured while trying to convert deployment data to GUID system: {e}");
                }
            }
        }

        private void PipelineChecks()
        {
            pipelineManager = FindObjectOfType<PipelineManager>();

            if (branchStorageObject.CurrentBranch == null) return;

            if (pipelineManager != null)
            {
                //dumb check to prevent buildhelper from throwing an error when it doesn't need to
                if (branchStorageObject.CurrentBranch.blueprintID.Length > 1)
                {
                    if (pipelineManager.blueprintId != branchStorageObject.CurrentBranch.blueprintID)
                    {
                        EditorGUILayout.HelpBox(
                            "The scene descriptor blueprint ID currently doesn't match the branch blueprint ID. VR Build Helper will not function properly.",
                            MessageType.Error);
                        if (GUILayout.Button("Auto fix"))
                        {
                            ApplyPipelineID(branchStorageObject.CurrentBranch.blueprintID);
                        }
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "To use VR Build Helper you need a Scene Decriptor in the scene. Please add a VRC Scene Descriptor.",
                    MessageType.Error);

                GUIContent autoFix = new GUIContent("Auto fix",
                    "Create a new GameObject containing Scene Descriptor and Pipeline Manager components");

                if (GUILayout.Button(autoFix))
                {
                    CreateSceneDescriptor();
                    ApplyPipelineID(branchStorageObject.CurrentBranch.blueprintID);
                }
            }
        }

        private void CreateSceneDescriptor()
        {
            GameObject sceneDescriptorObject = new GameObject("Scene Descriptor");
            sceneDescriptorObject.AddComponent<VRCSceneDescriptor>();
            sceneDescriptorObject.AddComponent<PipelineManager>();
            pipelineManager = sceneDescriptorObject.GetComponent<PipelineManager>();
        }

        private bool SceneChecks()
        {
            bool sceneIssues = !UpdateLayers.AreLayersSetup() || !UpdateLayers.IsCollisionLayerMatrixSetup();

            if (sceneIssues)
            {
                EditorGUILayout.HelpBox(
                    "The current project either has Layer or Collision Matrix issues. You should open the VRChat SDK Control Panel to fix these issues, or have Build Helper fix them automatically.",
                    MessageType.Warning);
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Open VRChat Control Panel"))
                {
                    VRCSettings.ActiveWindowPanel = 1;
                    GetWindow<VRCSdkControlPanel>();
                }

                if (!UpdateLayers.AreLayersSetup())
                {
                    if (GUILayout.Button("Setup Layers for VRChat", GUILayout.Width(172)))
                    {
                        UpdateLayers.SetupEditorLayers();
                    }
                }

                using (new EditorGUI.DisabledScope(!UpdateLayers.AreLayersSetup()))
                {
                    if (!UpdateLayers.IsCollisionLayerMatrixSetup())
                    {
                        if (GUILayout.Button("Set Collision Matrix", GUILayout.Width(172)))
                        {
                            UpdateLayers.SetupCollisionLayerMatrix();
                        }
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            return sceneIssues;
        }

        public static void SwitchBranch(BuildHelperData data, int targetBranch)
        {
            BranchStorageObject storageObject = data.dataObject;

            //prevent indexoutofrangeexception
            if (storageObject.currentBranch < storageObject.branches.Length && storageObject.currentBranch > -1)
            {
                OverrideContainer overrideContainer = storageObject.CurrentBranch.overrideContainer;

                //reverse override container state
                if (overrideContainer.hasOverrides)
                    overrideContainer.ResetStateChanges();
            }

            storageObject.currentBranch = targetBranch;
            data.PrepareExcludedGameObjects();

            if (storageObject.branches.Length > targetBranch)
            {
                OverrideContainer overrideContainer = storageObject.CurrentBranch.overrideContainer;

                if (overrideContainer.hasOverrides)
                    overrideContainer.ApplyStateChanges();

                ApplyPipelineID(storageObject.CurrentBranch.blueprintID);
            }
            else if (storageObject.branches.Length == 0)
            {
                ApplyPipelineID("");
            }
        }

        public static void ApplyPipelineID(string blueprintID)
        {
            if (FindObjectOfType<VRCSceneDescriptor>())
            {
                VRCSceneDescriptor sceneDescriptor = FindObjectOfType<VRCSceneDescriptor>();
                PipelineManager pipelineManager = sceneDescriptor.GetComponent<PipelineManager>();

                pipelineManager.blueprintId = "";
                pipelineManager.completedSDKPipeline = false;

                EditorUtility.SetDirty(pipelineManager);
                EditorSceneManager.MarkSceneDirty(pipelineManager.gameObject.scene);
                EditorSceneManager.SaveScene(pipelineManager.gameObject.scene);

                sceneDescriptor.apiWorld = null;

                pipelineManager.blueprintId = blueprintID;
                pipelineManager.completedSDKPipeline = true;

                EditorUtility.SetDirty(pipelineManager);
                EditorSceneManager.MarkSceneDirty(pipelineManager.gameObject.scene);
                EditorSceneManager.SaveScene(pipelineManager.gameObject.scene);

                if (pipelineManager.blueprintId == "") return;

                ApiWorld world = API.FromCacheOrNew<ApiWorld>(pipelineManager.blueprintId);
                world.Fetch(null,
                    c => sceneDescriptor.apiWorld = c.Model as ApiWorld,
                    c =>
                    {
                        if (c.Code == 404)
                        {
                            Logger.LogError($"The selected blueprint id ({pipelineManager.blueprintId}) does not exist");
                            ApiCache.Invalidate(pipelineManager.blueprintId);
                        }
                        else
                            Logger.LogError($"Could not load world {pipelineManager.blueprintId} because {c.Error}");
                    });
                sceneDescriptor.apiWorld = world;
            }
        }

        private bool deploymentEditor, gameObjectOverrides;

        private void DrawBranchEditor()
        {
            Branch selectedBranch = branchStorageObject.CurrentBranch;

            GUIStyle styleRichTextLabel = new GUIStyle(GUI.skin.label) { richText = true };
            GUILayout.Label("<b>Branch Editor</b>", styleRichTextLabel);

            EditorGUI.BeginChangeCheck();

            selectedBranch.name = EditorGUILayout.TextField("Branch name:", selectedBranch.name);

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(true);
            selectedBranch.blueprintID = EditorGUILayout.TextField("Blueprint ID:", selectedBranch.blueprintID);
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("Change", GUILayout.Width(55)))
            {
                ChangeBranchBlueprintID(selectedBranch);
            }

            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck()) TrySave();

            DrawVRCWorldEditor(selectedBranch);

            DrawGameObjectEditor(selectedBranch);
            DrawDeploymentEditorPreview(selectedBranch);
            DrawUdonLinkEditor(selectedBranch);
            DrawDiscordLinkEditor(selectedBranch);

            GUILayout.FlexibleSpace();

            DisplayBuildInformation(selectedBranch);

            EditorGUILayout.Space();
        }

        private void ChangeBranchBlueprintID(Branch targetBranch)
        {
            //capture current blueprint id since the Blueprint Picker might not finish immediately
            string branchID = targetBranch.branchID;

            //spawn editor window
            BlueprintPicker.BlueprintSelector<ApiWorld>(world =>
            {
                Branch branch = branchStorageObject.GetBranchByID(branchID);

                if (branch == null)
                {
                    EditorUtility.DisplayDialog("Couldn't update blueprint ID",
                        "The target branch is invalid or was deleted.", "Close");
                    return;
                }

                branch.blueprintID = world == null ? "" : world.id;

                branch.cachedName = "Unpublished VRChat world";
                branch.cachedDescription = "";
                branch.cachedCap = 16;
                branch.cachedRelease = "private";
                branch.cachedTags = new List<string>();

                if (world == null)
                {
                    branch.editedName = "New VRChat World";
                    branch.editedDescription = "Fancy description for your world";
                }

                branch.nameChanged = false;
                branch.descriptionChanged = false;
                branch.capacityChanged = false;
                branch.tagsChanged = false;

                SwitchBranch(buildHelperData, Array.IndexOf(branchStorageObject.branches, branch));
            }, true, branchStorageObject.CurrentBranch.blueprintID);
        }

        private void DrawGameObjectEditor(Branch selectedBranch)
        {
            EditorGUI.BeginChangeCheck();
            GUILayout.BeginVertical("Helpbox");

            OverrideContainer container = selectedBranch.overrideContainer;
            container.hasOverrides = EditorGUILayout.Toggle("GameObject Overrides", container.hasOverrides);
            if (container.hasOverrides) gameObjectOverrides = EditorGUILayout.Foldout(gameObjectOverrides, "");
            if (EditorGUI.EndChangeCheck())
            {
                TrySave();
            }

            if (gameObjectOverrides && container.hasOverrides)
            {
                EditorGUILayout.HelpBox(
                    "GameObject overrides are rules that can be set up for a branch to exclude GameObjects from builds for that or other branches. Exclusive GameObjects are only included on branches which have them added to the exclusive list. Excluded GameObjects are excluded for branches that have them added.",
                    MessageType.Info);

                _overrideContainer = container;

                if (currentGameObjectContainerIndex != branchStorageObject.currentBranch) InitGameObjectContainerLists();
                if (exclusiveGameObjectsList == null) InitGameObjectContainerLists();
                if (excludedGameObjectsList == null) InitGameObjectContainerLists();

                buildHelperDataSO.Update();

                exclusiveGameObjectsList.DoLayoutList();
                excludedGameObjectsList.DoLayoutList();

                buildHelperDataSO.ApplyModifiedProperties();
            }

            GUILayout.EndVertical();
        }


        private Vector2 deploymentScrollArea;
        private bool doneScan = false;

        private void DrawDeploymentEditorPreview(Branch selectedBranch)
        {
            GUILayout.BeginVertical("Helpbox");

            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginChangeCheck();
            selectedBranch.hasDeploymentData =
                EditorGUILayout.Toggle("Deployment Manager", selectedBranch.hasDeploymentData);
            if (EditorGUI.EndChangeCheck())
            {
                TrySave();
            }

            EditorGUI.BeginDisabledGroup(!selectedBranch.hasDeploymentData);
            if (GUILayout.Button("Open Deployment Manager", GUILayout.Width(200)))
            {
                DeploymentManagerEditor.OpenDeploymentManager(buildHelperData, branchStorageObject.currentBranch);
            }

            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            if (selectedBranch.hasDeploymentData)
            {
                EditorGUILayout.BeginHorizontal();
                deploymentEditor = EditorGUILayout.Foldout(deploymentEditor, "");
                if (deploymentEditor)
                {
                    if (GUILayout.Button(refreshButton, GUILayout.Width(100)))
                    {
                        DeploymentManager.RefreshDeploymentData(selectedBranch);
                    }
                }

                EditorGUILayout.EndHorizontal();

                if (deploymentEditor)
                {
                    bool deleted =
                        !Directory.Exists(Application.dataPath + selectedBranch.deploymentData.deploymentPath);
                    if (selectedBranch.deploymentData.deploymentPath == "" || deleted)
                    {
                        EditorGUILayout.HelpBox("The previous deployment save location was deleted.",
                            MessageType.Error);

                        EditorGUILayout.HelpBox(
                            "The Deployment Manager automatically saves uploaded builds so you can revisit or reupload them later.\nTo start using the Deployment Manager, please set a location to store uploaded builds.",
                            MessageType.Info);
                        if (GUILayout.Button("Set deployment path..."))
                        {
                            string selectedFolder = EditorUtility.OpenFolderPanel("Set deployment folder location...",
                                Application.dataPath, "Deployments");
                            if (!string.IsNullOrEmpty(selectedFolder))
                            {
                                if (selectedFolder.StartsWith(Application.dataPath))
                                {
                                    selectedBranch.deploymentData.deploymentPath =
                                        selectedFolder.Substring(Application.dataPath.Length);
                                }
                                else
                                {
                                    Logger.LogError("Please choose a location within the Assets folder");
                                }
                            }
                        }

                        GUILayout.EndVertical();

                        return;
                    }

                    if (!doneScan)
                    {
                        DeploymentManager.RefreshDeploymentData(selectedBranch);
                        doneScan = true;
                    }

                    deploymentScrollArea = EditorGUILayout.BeginScrollView(deploymentScrollArea);

                    if (selectedBranch.deploymentData.units.Length < 1)
                    {
                        EditorGUILayout.HelpBox(
                            "No builds have been saved yet. To save a build for this branch, upload your world.",
                            MessageType.Info);
                    }

                    bool pcUploadKnown = false, androidUploadKnown = false;

                    foreach (DeploymentUnit deploymentUnit in selectedBranch.deploymentData.units)
                    {
                        Color backgroundColor = GUI.backgroundColor;

                        bool isLive = false;

                        if (deploymentUnit.platform == Platform.Android)
                        {
                            if (selectedBranch.buildData.androidData.uploadVersion != -1)
                            {
                                DateTime androidUploadTime = selectedBranch.buildData.androidData.UploadTime;
                                if (Mathf.Abs((float)(androidUploadTime - deploymentUnit.buildDate).TotalSeconds) <
                                    300 &&
                                    !androidUploadKnown)
                                {
                                    androidUploadKnown = true;
                                    isLive = true;
                                }
                            }
                        }
                        else
                        {
                            if (selectedBranch.buildData.pcData.uploadVersion != -1)
                            {
                                DateTime pcUploadTime = selectedBranch.buildData.pcData.UploadTime;
                                if (Mathf.Abs((float)(pcUploadTime - deploymentUnit.buildDate).TotalSeconds) < 300 &&
                                    !pcUploadKnown)
                                {
                                    pcUploadKnown = true;
                                    isLive = true;
                                }
                            }
                        }

                        if (isLive) GUI.backgroundColor = new Color(0.2f, 0.92f, 0.2f);

                        GUILayout.BeginVertical("GroupBox");

                        GUI.backgroundColor = backgroundColor;

                        EditorGUILayout.BeginHorizontal();
                        GUIContent icon = EditorGUIUtility.IconContent(deploymentUnit.platform == Platform.Windows
                            ? "BuildSettings.Metro On"
                            : "BuildSettings.Android On");
                        EditorGUILayout.LabelField(icon, GUILayout.Width(20));
                        GUILayout.Label("Build " + deploymentUnit.buildNumber, GUILayout.Width(60));

                        EditorGUI.BeginDisabledGroup(true);
                        Rect fieldRect = EditorGUILayout.GetControlRect();
                        GUI.TextField(fieldRect, deploymentUnit.fileName);
                        EditorGUI.EndDisabledGroup();

                        GUIStyle selectButtonStyle = new GUIStyle(GUI.skin.button) { fixedWidth = 60 };
                        if (GUILayout.Button("Select", selectButtonStyle))
                        {
                            Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(
                                $"Assets/{selectedBranch.deploymentData.deploymentPath}/" + deploymentUnit.fileName);
                        }

                        EditorGUILayout.EndHorizontal();
                        GUILayout.EndVertical();
                    }

                    EditorGUILayout.EndScrollView();
                }
            }

            GUILayout.EndVertical();
        }

        private void DrawUdonLinkEditor(Branch selectedBranch)
        {
            EditorGUI.BeginChangeCheck();
            GUILayout.BeginVertical("Helpbox");

            EditorGUILayout.BeginHorizontal();
            selectedBranch.hasUdonLink = EditorGUILayout.Toggle("Udon Link", selectedBranch.hasUdonLink);
            EditorGUI.BeginDisabledGroup(!selectedBranch.hasUdonLink || buildHelperData.linkedBehaviour == null);
            if (GUILayout.Button("Open inspector", GUILayout.Width(200)))
            {
                EditorApplication.ExecuteMenuItem("Window/General/Inspector");
                Selection.objects = new Object[] { buildHelperData.linkedBehaviourGameObject };
            }

            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            if (selectedBranch.hasUdonLink)
            {
                if (buildHelperData.linkedBehaviourGameObject != null)
                {
                    buildHelperData.linkedBehaviour = buildHelperData.linkedBehaviourGameObject
                        .GetComponent<BuildHelperUdon>();
                }

                EditorGUILayout.BeginHorizontal();

                EditorGUI.BeginChangeCheck();
                buildHelperDataSO.Update();
                EditorGUILayout.PropertyField(buildHelperDataSO.FindProperty("linkedBehaviour"));
                buildHelperDataSO.ApplyModifiedProperties();

                if (EditorGUI.EndChangeCheck())
                {
                    if (buildHelperData.linkedBehaviour == null)
                    {
                        buildHelperData.linkedBehaviourGameObject = null;
                    }
                    else
                        buildHelperData.linkedBehaviourGameObject =
                            buildHelperData.linkedBehaviour.gameObject;
                }

                if (buildHelperData.linkedBehaviourGameObject == null)
                {
                    if (GUILayout.Button("Create new", GUILayout.Width(100)))
                    {
                        GameObject buildHelperUdonGameObject = new GameObject("BuildHelperUdon");
                        buildHelperUdonGameObject.AddUdonSharpComponent<BuildHelperUdon>();
                        buildHelperData.linkedBehaviourGameObject = buildHelperUdonGameObject;
                    }

                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.HelpBox(
                        "There is no BuildHelperUdon behaviour selected for this scene right now.\nSelect an existing behaviour or create a new one.",
                        MessageType.Info);
                }
                else EditorGUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
            if (EditorGUI.EndChangeCheck())
            {
                if (selectedBranch.hasUdonLink)
                {

                }

                TrySave();
            }
        }

        private void DrawDiscordLinkEditor(Branch selectedBranch)
        {
            EditorGUI.BeginChangeCheck();
            GUILayout.BeginVertical("Helpbox");

            EditorGUI.BeginChangeCheck();
            
            EditorGUILayout.BeginHorizontal();
            selectedBranch.hasDiscordWebhook = EditorGUILayout.Toggle("Discord Webhooks", selectedBranch.hasDiscordWebhook);
            
            if (GUILayout.Button("Open editor", GUILayout.Width(200)))
            {
                DiscordWebhookEditor.OpenEditor(buildHelperData, selectedBranch);
            }
            
            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                TrySave();
            }

            GUILayout.EndVertical();
        }

        private bool editMode;
        private bool editModeChanges;
        private string tempName;
        private string tempDesc;
        private int tempCap;
        private bool applying;
        private Branch applyBranch;
        private List<string> tempTags;

        private void DrawVRCWorldEditor(Branch branch)
        {
            ApiWorld apiWorld = branch.FetchWorldData();

            //set up button style
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                stretchWidth = false, fixedHeight = EditorGUIUtility.singleLineHeight, richText = true
            };
            
            GUILayout.BeginVertical("Helpbox");

            GUILayout.BeginHorizontal();
            GUILayout.Label("VRChat World Editor");
            EditorGUI.BeginDisabledGroup(Application.isPlaying);
            if (GUILayout.Button(refreshButton, GUILayout.Width(100)))
            {
                VRChatApiToolsEditor.RefreshData();
            }
            
            if (!branch.isNewWorld && branch.apiWorldLoaded && branch.HasVRCDataChanges())
            {
                bool authorMismatch = apiWorld.authorId != APIUser.CurrentUser.id;

                using (new EditorGUI.DisabledScope(applying || authorMismatch))
                {
                    GUIContent applyChangesButton = new GUIContent(
                        applying ? "Applying changes..." : "<color=yellow>Apply Changes to World</color>",
                        applying ? null : EditorGUIUtility.IconContent("Warning").image,
                        applying
                            ? ""
                            : (authorMismatch
                                ? "Can't apply changes to this blueprint ID because the logged in user doesn't own it"
                                : "Apply changes to this blueprint ID"));

                    if (GUILayout.Button(applyChangesButton, buttonStyle))
                    {
                        if (EditorUtility.DisplayDialog("Applying Changes to VRChat World",
                                "Applying changes will immediately apply any changes you made here without reding the world. Are you sure you want to continue?",
                                "Yes", "No"))
                        {
                            ApplyBranchChanges(branch, apiWorld);
                        }
                    }
                }
            }
            EditorGUI.EndDisabledGroup();
            GUILayout.EndHorizontal();
            
            if (branch.loadError)
            {
                EditorGUILayout.HelpBox(
                    "Couldn't load world information. This can happen if the blueprint ID is invalid, or if the world was deleted.",
                    MessageType.Error);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Change blueprint ID"))
                {
                    ChangeBranchBlueprintID(branch);
                }

                if (GUILayout.Button("Remove blueprint ID"))
                {
                    branch.blueprintID = "";
                    SwitchBranch(buildHelperData, Array.IndexOf(branchStorageObject.branches, branch));
                }

                EditorGUILayout.EndHorizontal();
            }
            
            if (!branch.loadError && !branch.isNewWorld && branch.apiWorldLoaded == false && !Application.isPlaying)
            {
                EditorGUILayout.LabelField("Loading world information...");
            }

            GUIStyle styleRichTextLabelBig = new GUIStyle(GUI.skin.label)
                { richText = true, fontSize = 20, wordWrap = true };

            if (branch.loadError)
            {
                GUILayout.Label("Error loading world information", styleRichTextLabelBig);
            }
            else if (branch.isNewWorld)
            {
                GUILayout.Label("Unpublished VRChat World", styleRichTextLabelBig);
            }
            else
            {
                string headerText = branch.cachedName;

                if (branch.apiWorldLoaded)
                {
                    CacheWorldInfo(branch, apiWorld);
                    headerText = $"<b>{apiWorld.name}</b> by {apiWorld.authorName}";
                }

                GUILayout.Label(headerText, styleRichTextLabelBig);
            }

            float imgWidth = 170;
            float width = position.width - imgWidth - 20;
            GUIStyle worldInfoStyle = new GUIStyle(GUI.skin.label)
                { wordWrap = true, fixedWidth = width, richText = true };

            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(width));

            string displayName = "",
                displayDesc = "",
                displayCap = "",
                displayTags = "",
                displayRelease = "",
                displayPlatforms = "";

            //draw world editor
            if (!editMode)
            {
                if (branch.isNewWorld)
                {
                    displayName = $"<color=gray>{branch.editedName}</color>";
                    displayDesc = $"<color=gray>{branch.editedDescription}</color>";
                    displayCap = $"<color=gray>{branch.editedCap}</color>";
                    displayTags = $"<color=gray>{DisplayTags(branch.editedTags)}</color>";
                    displayRelease = "<b>New world</b>";
                    displayPlatforms = CurrentPlatform().ToString();
                }
                else if (apiWorld != null)
                {
                    displayName = branch.nameChanged
                        ? $"<color=yellow>{branch.editedName}</color>"
                        : apiWorld.name;
                    displayDesc = branch.descriptionChanged
                        ? $"<color=yellow>{branch.editedDescription}</color>"
                        : apiWorld.description;
                    displayCap = branch.capacityChanged
                        ? $"<color=yellow>{branch.editedCap}</color>"
                        : apiWorld.capacity.ToString();
                    displayTags = branch.tagsChanged
                        ? $"<color=yellow>{DisplayTags(branch.editedTags)}</color>"
                        : DisplayTags(apiWorld.publicTags);
                    displayRelease = apiWorld.releaseStatus +
                                     (apiWorld.IsCommunityLabsWorld ? " (Community labs)" : "");
                    displayPlatforms = apiWorld.supportedPlatforms.ToPrettyString();
                }

                EditorGUILayout.LabelField("Name: " + displayName, worldInfoStyle);
                EditorGUILayout.LabelField("Description: " + displayDesc, worldInfoStyle);
                EditorGUILayout.LabelField("Capacity: " + displayCap, worldInfoStyle);
                EditorGUILayout.LabelField("Tags: " + displayTags, worldInfoStyle);
                EditorGUILayout.LabelField("Release: " + displayRelease, worldInfoStyle);
                EditorGUILayout.LabelField("Supported platforms: " + displayPlatforms, worldInfoStyle);
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                tempName = EditorGUILayout.TextField("Name:", tempName);
                EditorGUILayout.LabelField("Description:");
                tempDesc = EditorGUILayout.TextArea(tempDesc,
                    new GUIStyle(EditorStyles.textArea) { wordWrap = true });
                tempCap = EditorGUILayout.IntField("Capacity:", tempCap);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Tags:");
                GUILayout.FlexibleSpace();
                EditorGUI.BeginDisabledGroup(tempTags.Count >= 5);
                if (GUILayout.Button(addTagButton, GUILayout.Width(80)))
                {
                    tempTags.Add("author_tag_new tag");
                }

                EditorGUI.EndDisabledGroup();

                EditorGUILayout.EndHorizontal();

                for (int i = 0; i < tempTags.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    //don't expose the user to author_tag_
                    tempTags[i] = "author_tag_" + EditorGUILayout.TextField(tempTags[i].Substring(11));

                    if (GUILayout.Button("Delete", GUILayout.Width(70)))
                    {
                        tempTags.RemoveAt(i);
                    }

                    EditorGUILayout.EndHorizontal();
                }

                if (EditorGUI.EndChangeCheck()) editModeChanges = true;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(branch.apiWorldLoaded
                    ? "Release: " + apiWorld.releaseStatus + (apiWorld.IsCommunityLabsWorld ? " (Community labs)" : "")
                    : "Release: " + branch.cachedRelease, GUILayout.Width(100));

                GUILayout.FlexibleSpace();

                if (branch.apiWorldLoaded)
                {
                    if (apiWorld.releaseStatus == "private")
                    {
                        if (GUILayout.Button(makePublicButton, GUILayout.Width(100)))
                        {
                            CommunityLabsPublisher.PublishWorld(apiWorld);
                        }
                    }
                    else if (apiWorld.releaseStatus == "public")
                    {
                        if (GUILayout.Button(makePrivateButton, GUILayout.Width(100)))
                        {
                            CommunityLabsPublisher.UnpublishWorld(apiWorld);
                        }
                    }

                    Color temp = GUI.backgroundColor;
                    GUI.backgroundColor = Color.red;
                    if (GUILayout.Button(deleteWorldButton, GUILayout.Width(100)))
                    {
                        if (EditorUtility.DisplayDialog("Confirm deletion",
                                $"Are you sure you want to delete the world '{apiWorld.name}'? This will remove the world from VRChat permanently, and this is not reversible.",
                                "Delete",
                                "Cancel"))
                        {
                            branch.blueprintID = "";

                            branch.cachedName = "Unpublished VRChat world";
                            branch.cachedDescription = "";
                            branch.cachedCap = 16;
                            branch.cachedRelease = "private";
                            branch.cachedTags = new List<string>();

                            branch.nameChanged = false;
                            branch.descriptionChanged = false;
                            branch.capacityChanged = false;
                            branch.tagsChanged = false;

                            SwitchBranch(buildHelperData, Array.IndexOf(branchStorageObject.branches, branch));

                            API.Delete<ApiWorld>(apiWorld.id);

                            VRChatApiTools.VRChatApiTools.ClearCaches();
                        }

                        editMode = false;
                        TrySave();
                    }

                    GUI.backgroundColor = temp;
                }

                EditorGUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();

            //draw image editor
            GUILayout.BeginVertical();

            if (branch.vrcImageHasChanges)
            {
                if (!modifiedWorldImages.ContainsKey(branch.branchID))
                {
                    modifiedWorldImages.Add(branch.branchID,
                        AssetDatabase.LoadAssetAtPath<Texture2D>(
                            ImageTools.GetImageAssetPath(buildHelperData.sceneID, branch.branchID)));
                }

                if (modifiedWorldImages.ContainsKey(branch.branchID))
                {
                    GUILayout.Box(modifiedWorldImages[branch.branchID], modifiedImageBackground, GUILayout.Width(imgWidth), GUILayout.Height(imgWidth / 4 * 3));
                }
            }
            else
            {
                if (branch.apiWorldLoaded && !branch.loadError)
                {
                    if (VRChatApiTools.VRChatApiTools.ImageCache.ContainsKey(apiWorld.id))
                    {
                        GUILayout.Box(ImageCache[apiWorld.id], GUILayout.Width(imgWidth),
                            GUILayout.Height(imgWidth / 4 * 3));
                    }
                }
                else
                {
                    GUILayout.Box(branch.apiWorldLoaded ? "Couldn't load image" : "No image set",
                        GUILayout.Width(imgWidth),
                        GUILayout.Height(imgWidth / 4 * 3));
                }
            }

            GUILayout.Space(8);
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            GUIStyle infoStyle = new GUIStyle(EditorStyles.helpBox) { richText = true };

            if (branch.vrcImageHasChanges && !string.IsNullOrWhiteSpace(branch.vrcImageWarning))
            {
                EditorGUILayout.LabelField(branch.vrcImageWarning, infoStyle);
            }

            if (branch.nameChanged || branch.descriptionChanged || branch.capacityChanged || branch.tagsChanged ||
                branch.vrcImageHasChanges)
            {
                string changesWarning =
                    "<color=yellow>Your changes will be applied automatically with the next upload. You can also apply changes directly by clicking [Apply Changes to World].</color>";
                EditorGUILayout.LabelField(changesWarning, infoStyle);
            }
            
            //draw button row
            EditorGUILayout.BeginHorizontal();

            if (editMode)
            {
                if (GUILayout.Button(editModeChanges ? saveButton : cancelButton, buttonStyle))
                {
                    editMode = false;

                    if (editModeChanges)
                    {
                        branch.editedName = tempName;
                        branch.editedDescription = tempDesc;
                        branch.editedCap = tempCap;
                        branch.editedTags = tempTags.ToList();

                        if (branch.isNewWorld)
                        {
                            branch.nameChanged = branch.editedName != "New VRChat World";
                            branch.descriptionChanged = branch.editedDescription != "Fancy description for your world";
                            branch.capacityChanged = branch.editedCap != 16;
                            branch.tagsChanged = !branch.editedTags.SequenceEqual(new List<string>());
                        }
                        else
                        {
                            branch.nameChanged = branch.editedName != apiWorld.name;
                            branch.descriptionChanged = branch.editedDescription != apiWorld.description;
                            branch.capacityChanged = branch.editedCap != apiWorld.capacity;
                            branch.tagsChanged = !branch.editedTags.SequenceEqual(apiWorld.publicTags);
                        }

                        TrySave();
                    }
                }

                using (new EditorGUI.DisabledScope(!editModeChanges && branch.isNewWorld))
                {
                    if ((editModeChanges || branch.HasVRCDataChanges()) && GUILayout.Button(editModeChanges //if have been changes in edit mode, give an option to revert only those changes
                                ? new GUIContent("Revert", "Will undo any changes made since you started editing")
                                : new GUIContent("Revert All", EditorGUIUtility.IconContent("Warning").image,
                                    branch.isNewWorld ? "There is nothing to revert to on new worlds." : "Revert all world metadata back to what is currently stored on VRChat."), buttonStyle))
                    {
                        if (!editModeChanges)
                        {
                            if (EditorUtility.DisplayDialog("Reverting all changes",
                                    "You don't seem to have made any text changes while in edit mode. Clicking revert will reset all previously edited text to what is currently stored on VRChat's servers. Do you want to proceed?",
                                    "Proceed", "Cancel"))
                            {
                                branch.editedName = branch.cachedName;
                                branch.editedDescription = branch.cachedDescription;
                                branch.editedCap = branch.cachedCap;
                                branch.editedTags = branch.cachedTags.ToList();

                                branch.nameChanged = false;
                                branch.descriptionChanged = false;
                                branch.capacityChanged = false;
                                branch.tagsChanged = false;

                                editMode = false;

                                TrySave();
                            }
                        }
                        else
                        {
                            editMode = false;

                            tempName = branch.editedName;
                            tempDesc = branch.editedDescription;
                            tempCap = branch.editedCap;
                            tempTags = branch.editedTags.ToList();

                            TrySave();
                        }
                    }
                }

                //draw image buttons
                if (GUILayout.Button(branch.apiWorldLoaded ? replaceImageButton : setImageButton, buttonStyle))
                {
                    imageBranch = branch;
                    OnImageSelected(EditorUtility.OpenFilePanel("Select Image", "", "png"));
                }

                if (GUILayout.Button(cameraButton, buttonStyle))
                {
                    imageBranch = branch;
                    EditorCameraGUIHelper.SetupCapture(UpdateBranchImage);
                }

                if (branch.vrcImageHasChanges)
                {
                    if (GUILayout.Button(revertImageButton, buttonStyle))
                    {
                        modifiedWorldImages.Remove(branch.branchID);

                        branch.RevertImageModifications();
                    }
                }
            }
            else
            {
                if (GUILayout.Button(editButton, buttonStyle))
                {
                    editMode = true;
                    editModeChanges = false;

                    if (!branch.isNewWorld)
                    {
                        tempName = branch.nameChanged ? branch.editedName : apiWorld.name;
                        tempDesc = branch.descriptionChanged ? branch.editedDescription : apiWorld.description;
                        tempCap = branch.capacityChanged ? branch.editedCap : apiWorld.capacity;
                        tempTags = branch.tagsChanged ? branch.editedTags.ToList() : apiWorld.publicTags.ToList();
                    }
                    else
                    {
                        tempName = branch.editedName;
                        tempDesc = branch.editedDescription;
                        tempCap = branch.editedCap;
                        tempTags = branch.editedTags.ToList();
                    }
                }

                buttonStyle.fixedWidth = 170;

                if (GUILayout.Button(openWebsite, buttonStyle))
                {
                    Application.OpenURL($"https://vrchat.com/home/world/{branch.blueprintID}");
                }
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private async void ApplyBranchChanges(Branch branch, ApiWorld apiWorld)
        {
            applyBranch = branch;
            applying = true;

            apiWorld.name = branch.editedName;
            apiWorld.description = branch.editedDescription;
            apiWorld.capacity = branch.editedCap;
            apiWorld.tags = branch.editedTags.ToList();

            if (branch.vrcImageHasChanges)
            {
                VRChatApiUploaderAsync uploader = new VRChatApiUploaderAsync();
                uploader.UseStatusWindow();

                apiWorld.imageUrl = await uploader.UploadImage(apiWorld, branch.overrideImagePath);
                branch.vrcImageHasChanges = false;

                uploader.OnUploadState(VRChatApiToolsUploadStatus.UploadState.finished);
            }

            apiWorld.Save(c =>
            {
                applyBranch.nameChanged = false;
                applyBranch.descriptionChanged = false;
                applyBranch.capacityChanged = false;
                applyBranch.tagsChanged = false;

                VRChatApiToolsEditor.RefreshData();
                applying = false;
                Logger.Log($"Succesfully applied changed to {branch.name}");
            }, c =>
            {
                EditorUtility.DisplayDialog("Build Helper",
                    $"Couldn't apply changes to target branch: {c.Error}", "Ok");

                VRChatApiToolsEditor.RefreshData();
                applying = false;
            });

            await Task.Delay(3000);

            ClearCaches();

            FetchApiWorld(branch.blueprintID);

            await Task.Delay(200);

            Repaint();
        }

        private void CacheWorldInfo(Branch branch, ApiWorld apiWorld)
        {
            bool localDataOutdated = false;

            if (branch.cachedName != apiWorld.name)
            {
                branch.cachedName = apiWorld.name;
                localDataOutdated = true;
            }

            if (branch.cachedDescription != apiWorld.description)
            {
                branch.cachedDescription = apiWorld.description;
                localDataOutdated = true;
            }

            if (branch.cachedCap != apiWorld.capacity)
            {
                branch.cachedCap = apiWorld.capacity;
                localDataOutdated = true;
            }

            if (!branch.cachedTags.SequenceEqual(apiWorld.publicTags))
            {
                branch.cachedTags = apiWorld.publicTags.ToList();
                localDataOutdated = true;
            }

            if (branch.cachedRelease != apiWorld.releaseStatus)
            {
                branch.cachedRelease = apiWorld.releaseStatus;
                localDataOutdated = true;
            }

            if (branch.cachedPlatforms != apiWorld.supportedPlatforms)
            {
                branch.cachedPlatforms = apiWorld.supportedPlatforms;
                localDataOutdated = true;
            }

            if (localDataOutdated)
            {
                if (branch.editedName == "notInitialised") branch.editedName = branch.cachedName;
                if (branch.editedDescription == "notInitialised") branch.editedDescription = branch.cachedDescription;
                if (branch.editedCap == -1) branch.editedCap = branch.cachedCap;
                if (branch.editedTags.Count == 0) branch.editedTags = branch.cachedTags.ToList();
            }

            if (localDataOutdated) TrySave();
        }

        private Branch imageBranch;

        private void OnImageSelected(string filePath)
        {
            if (File.Exists(filePath))
            {
                if (imageBranch != null)
                {
                    byte[] fileData = File.ReadAllBytes(filePath);
                    Texture2D overrideImage = new Texture2D(2, 2);

                    overrideImage.LoadImage(fileData); //..this will auto-resize the texture dimensions.
                    overrideImage.Apply();

                    //check aspect ratio and resolution
                    imageBranch.vrcImageWarning =
                        ImageTools.GenerateImageFeedback(overrideImage.width, overrideImage.height);

                    //resize image if needed (for some reason i can just upload 8k images to vrc's servers and it works fine..)
                    if (overrideImage.width != 1200 || overrideImage.height != 900)
                    {
                        overrideImage = ImageTools.Resize(overrideImage, 1200, 900);
                    }

                    UpdateBranchImage(overrideImage);
                }
                else
                {
                    Logger.LogError("Target branch for image processor doesn't exist anymore, was it deleted?");
                }
            }
            else
            {
                Logger.LogError("Null filepath was passed to image processor, skipping process steps");
            }
        }

        private void UpdateBranchImage(Texture2D newImage)
        {
            //encode image as PNG
            byte[] worldImagePNG = newImage.EncodeToPNG();

            string dirPath = Application.dataPath + "/Resources/BuildHelper/";
            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            //write image
            File.WriteAllBytes(ImageTools.GetImagePath(buildHelperData.sceneID, imageBranch.branchID),
                worldImagePNG);

            string savePath = ImageTools.GetImageAssetPath(buildHelperData.sceneID, imageBranch.branchID);

            AssetDatabase.WriteImportSettingsIfDirty(savePath);
            AssetDatabase.ImportAsset(savePath);

            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(savePath);
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            EditorUtility.SetDirty(importer);
            AssetDatabase.WriteImportSettingsIfDirty(savePath);

            AssetDatabase.ImportAsset(savePath);

            imageBranch.vrcImageHasChanges = true;
            imageBranch.overrideImagePath = savePath;

            editModeChanges = true;

            TrySave();
        }

        private void DisplayBuildInformation(Branch branch)
        {
            BuildData buildData = branch.buildData;
            GUIStyle styleRichTextLabel = new GUIStyle(GUI.skin.label) { richText = true };

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("<b>Build Information</b>", styleRichTextLabel);
            if (GUILayout.Button(buildFolder, GUILayout.Width(150), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
            {
                string path = branch.buildData.GetLatestBuild().buildPath;
                if (path != null)
                {
                    if (File.Exists(path))
                    {
                        path = Path.GetDirectoryName(path);
                    }

                    if (Directory.Exists(path))
                    {
                        Process.Start("explorer.exe", "/select," + path);
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            
            PlatformBuildInfo pcBuild = buildData.pcData;
            PlatformBuildInfo androidBuild = buildData.androidData;

            GUIContent build = new GUIContent(_iconBuild);
            GUIContent cloud = new GUIContent(_iconCloud);

            buildStatusStyle = new GUIStyle(GUI.skin.label)
            {
                wordWrap = false,
                fixedWidth = 400,
                contentOffset = new Vector2(-12, 5),
                richText = true
            };

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(build, GUILayout.Width(48), GUILayout.Height(48));
            EditorGUILayout.BeginVertical();
            DrawBuildInfoLine(Platform.Windows, pcBuild, false);
            DrawBuildInfoLine(Platform.Android, androidBuild, false);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(cloud, GUILayout.Width(48), GUILayout.Height(48));
            EditorGUILayout.BeginVertical();
            DrawBuildInfoLine(Platform.Windows, pcBuild, true,
                pcBuild.uploadVersion < buildData.GetLatestBuild().uploadVersion);
            DrawBuildInfoLine(Platform.Android, androidBuild, true,
                androidBuild.uploadVersion < buildData.GetLatestBuild().uploadVersion);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        private GUIStyle buildStatusStyle;

        private void DrawBuildInfoLine(Platform platform, PlatformBuildInfo info, bool isUpload,
            bool isOutdated = false)
        {
            int ver = isUpload ? info.uploadVersion : info.buildVersion;
            bool hasTime = ver != -1;
            string time = isUpload
                ? hasTime ? info.UploadTime.ToString() : "Unknown"
                : hasTime
                    ? info.BuildTime.ToString()
                    : "Unknown";

            string tooltip = hasTime
                ? $"{platform} Build {ver}\n" +
                  (isUpload
                      ? $"Uploaded at {time}" + (isOutdated
                          ? "\nThis build doesn't match the newest uploaded build, you should consider reuploading for this platform."
                          : "")
                      : $"Built at {time}\nBuild path: {info.buildPath}\nBuild hash: {info.buildHash}\nBlueprint ID: {info.blueprintID}\nBuild size: {info.buildSize.ToReadableBytes()}\n{(info.buildValid ? "Verified build" : $"Couldn't verify build : {info.buildInvalidReason}")}")
                : $"Couldn't find a last {(isUpload ? "upload" : "build")} for this platform";
            
            GUIContent content = new GUIContent(
                (isOutdated ? "<color=yellow>" : "") +
                $"Last {platform} {(isUpload ? "upload" : "build")}: {(hasTime ? $"build {ver} ({time})" : "Unknown")}" +
                (isOutdated ? "</color>" : ""),
                tooltip);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(content, buildStatusStyle);
            
            if(!isUpload)
            {
                EditorGUI.BeginDisabledGroup(!info.buildValid);
                if (GUILayout.Button(exportBuild, GUILayout.ExpandWidth(false)))
                {
                    //save dialog
                    string path = EditorUtility.SaveFilePanel("Export build", "", $"{platform} build {ver}", "vrcw");
                    if (!string.IsNullOrEmpty(path))
                    {
                        //copy build to path
                        File.Copy(info.buildPath, path, true);
                    }
                }
                EditorGUI.EndDisabledGroup();
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private void DisplayBuildButtons()
        {
            GUIStyle styleRichTextLabel = new GUIStyle(GUI.skin.label) { richText = true };
            GUILayout.Label("<b>Build Options</b>", styleRichTextLabel);

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Build options are unavailable in play mode.", MessageType.Error);
                return;
            }

            if (!VRChatApiToolsGUI.HandleLogin(this, false)) return;

            if (editMode)
            {
                EditorGUILayout.HelpBox("Please save all changes in the blueprint editor before building.",
                    MessageType.Info);
                return;
            }

            DrawBuildTargetSwitcher();

            if (CurrentPlatform() == Platform.Android || BuildHelperEditorPrefs.ShowBuildOnly)
                DrawBuildOnlyOptions();

            if (CurrentPlatform() == Platform.Windows)
                DrawLocalTestingOptions();
            
            DrawPublishingOptions();
        }

        private void DrawBuildTargetSwitcher()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Active build target: "));
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(CurrentPlatform() == Platform.Windows ? activeWindowsTarget : activeAndroidTarget, GUILayout.Width(80));

            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows ||
                EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows64 &&
                GUILayout.Button(switchToAndroidTarget, GUILayout.Width(150)))
            {
                if (EditorUtility.DisplayDialog("Build Target Switcher",
                        "Are you sure you want to switch your build target to Android? This could take a while.",
                        "Confirm",
                        "Cancel"))
                {
                    EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Android;
                    EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Android, BuildTarget.Android);
                }
            }

            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android &&
                GUILayout.Button(switchToWindowsTarget, GUILayout.Width(150)))
            {
                if (EditorUtility.DisplayDialog("Build Target Switcher",
                        "Are you sure you want to switch your build target to Windows? This could take a while.",
                        "Confirm",
                        "Cancel"))
                {
                    EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Standalone;
                    EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Standalone,
                        BuildTarget.StandaloneWindows64);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawBuildOnlyOptions()
        {
            GUIStyle styleRichTextLabel = new GUIStyle(GUI.skin.label) { richText = true };
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
                { fixedWidth = 140, fixedHeight = EditorGUIUtility.singleLineHeight };

            EditorGUILayout.BeginVertical("Helpbox");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(buildOnlyHeader, styleRichTextLabel);

            if (CurrentPlatform() == Platform.Android)
                EditorGUILayout.LabelField("Local testing is not supported for Android");

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Build assetbundle");

            newBuildButton.tooltip = "You can use this option to do a clean build, either for analysis or to upload later.";
            if (GUILayout.Button(newBuildButton, buttonStyle))
            {
                string targetPath = EditorUtility.SaveFilePanel("Export AssetBundle as .vrcw", Application.dataPath, branchStorageObject.CurrentBranch.cachedName, "vrcw");
                if (!string.IsNullOrEmpty(targetPath))
                {
                    BuildHelperBuilder.ExportAssetBundle(targetPath);
                    return;
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawLocalTestingOptions()
        {
            GUIStyle styleRichTextLabel = new GUIStyle(GUI.skin.label) { richText = true };
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
                { fixedWidth = 140, fixedHeight = EditorGUIUtility.singleLineHeight };

            EditorGUILayout.BeginVertical("Helpbox");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(localTestingHeader, styleRichTextLabel);
            EditorGUILayout.LabelField("Number of Clients", GUILayout.Width(140));
            VRCSettings.NumClients = EditorGUILayout.IntField(VRCSettings.NumClients, GUILayout.Width(140));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("");
            EditorGUILayout.LabelField("Force no VR", GUILayout.Width(140));
            VRCSettings.ForceNoVR = EditorGUILayout.Toggle(VRCSettings.ForceNoVR, GUILayout.Width(140));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("");
            EditorGUILayout.LabelField(new GUIContent("Watch for changes",
                    "When enabled, launched VRChat clients will watch for new builds and reload the world when a new build is detected."),
                GUILayout.Width(140));
            VRCSettings.WatchWorlds = EditorGUILayout.Toggle(VRCSettings.WatchWorlds, GUILayout.Width(140));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Launch new test clients");

            //Prevent local testing on Android
            bool lastBuildBlocked = !CheckLastBuiltBranch();
            string lastBuildBlockedTooltip =
                $"Your last build for the current platform couldn't be found, its hash doesn't match the last {CurrentPlatform()} build for this branch, or was built for a different blueprint ID.";


            lastBuildButton.tooltip = lastBuildBlocked
                ? lastBuildBlockedTooltip
                : "Equivalent to the (Last build) Build & Test option in the VRChat SDK";

            using (new EditorGUI.DisabledScope(lastBuildBlocked))
            {
                if (GUILayout.Button(lastBuildButton, buttonStyle))
                {
                    BuildHelperData.RunLastBuildChecks();
                    
                    if (CheckLastBuiltBranch())
                    {
                        BuildHelperBuilder.TestExistingBuild(branchStorageObject.CurrentBranch.buildData
                            .CurrentPlatformBuildData().buildPath);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("BuildHelper", "Couldn't find the last build for this branch. Try building again.", "OK");
                    }
                }
            }

            newBuildButton.tooltip = "Equivalent to the Build & Test option in the VRChat SDK";

            if (GUILayout.Button(newBuildButton, buttonStyle))
            {
                BuildHelperBuilder.TestNewBuild();
                return;
            }

            EditorGUILayout.EndHorizontal();

            //Hide reload if watch changes is disabled
            if (VRCSettings.WatchWorlds)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Reload existing test clients");

                lastBuildButton.tooltip = lastBuildBlocked
                    ? lastBuildBlockedTooltip
                    : "Equivalent to using the (Last build) Enable World Reload option in the VRChat SDK with number of clients set to 0";

                using (new EditorGUI.DisabledScope(lastBuildBlocked))
                {
                    if (GUILayout.Button(lastBuildButton, buttonStyle))
                    {
                        BuildHelperData.RunLastBuildChecks();

                        if (CheckLastBuiltBranch())
                        {
                            BuildHelperBuilder.ReloadExistingBuild(branchStorageObject.CurrentBranch.buildData
                                .CurrentPlatformBuildData().buildPath);
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("BuildHelper", "Couldn't find the last build for this branch. Try building again.", "OK");
                        }
                    }
                }

                newBuildButton.tooltip =
                    "Equivalent to using the Enable World Reload option in the VRChat SDK with number of clients set to 0";

                if (GUILayout.Button(newBuildButton, buttonStyle))
                {
                    BuildHelperBuilder.ReloadNewBuild();
                    return;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPublishingOptions()
        {
            GUIStyle styleRichTextLabel = new GUIStyle(GUI.skin.label) { richText = true };
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
                { fixedWidth = 140, fixedHeight = EditorGUIUtility.singleLineHeight };

            EditorGUILayout.BeginVertical("Helpbox");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(publishHeader, styleRichTextLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(
                $"<i>{(APIUser.IsLoggedIn ? "Currently logged in as " + APIUser.CurrentUser.displayName : "")}</i>",
                styleRichTextLabel, GUILayout.ExpandWidth(false));

            bool publishBlocked = !branchStorageObject.CurrentBranch.isNewWorld && (branchStorageObject.CurrentBranch.apiWorldLoaded) && !CheckAccount(branchStorageObject.CurrentBranch);

            if (!publishBlocked)
            {
                if (GUILayout.Button(" Switch ", GUILayout.ExpandWidth(false)))
                {
                    VRCSettings.ActiveWindowPanel = 0;
                    GetWindow<VRCSdkControlPanel>();
                }
            }

            EditorGUILayout.EndHorizontal();

            if (publishBlocked)
            {
                EditorGUILayout.HelpBox("The currently logged in user doesn't have permission to publish to this blueprint ID.", MessageType.Error);

                if (GUILayout.Button("Open VRChat SDK Control Panel to switch accounts"))
                {
                    VRCSettings.ActiveWindowPanel = 0;
                    GetWindow<VRCSdkControlPanel>();
                }
            }

            using (new EditorGUI.DisabledScope(publishBlocked || !branchStorageObject.CurrentBranch.isNewWorld && !branchStorageObject.CurrentBranch.apiWorldLoaded))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Publish to VRChat");

                bool lastBuildBlocked = !CheckLastBuiltBranch();
                string lastBuildBlockedTooltip =
                    $"Your last build for the current platform couldn't be found, its hash doesn't match the last {CurrentPlatform()} build for this branch, or was built for a different blueprint ID.";

                using (new EditorGUI.DisabledScope(lastBuildBlocked))
                {
                    lastBuildButton.tooltip = lastBuildBlocked
                        ? lastBuildBlockedTooltip
                        : "Equivalent to (Last build) Build & Publish in the VRChat SDK";

                    if (GUILayout.Button(lastBuildButton, buttonStyle))
                    {
                        BuildHelperData.RunLastBuildChecks();

                        if (CheckLastBuiltBranch())
                        {
                            Branch targetBranch = branchStorageObject.CurrentBranch;

                            bool canPublish = true;
                            if (!targetBranch.remoteExists && !targetBranch.HasVRCDataChanges() &&
                                BuildHelperEditorPrefs.UseAsyncPublish)
                            {
                                canPublish = EditorUtility.DisplayDialog("Build Helper",
                                    $"You are about to publish a new world, but you haven't edited any world details. The async publisher doesn't enter playmode to let you edit world details, so your world will be uploaded as '{targetBranch.editedName}'. Do you want to continue?",
                                    "Continue", "Cancel");
                            }

                            if (canPublish)
                            {
                                if (BuildHelperEditorPrefs.UseAsyncPublish)
                                {
                                    Branch b = branchStorageObject.CurrentBranch;
                                    BuildHelperBuilder.PublishWorldAsync(
                                        b.buildData.CurrentPlatformBuildData().buildPath,
                                        "", b.ToWorldInfo(), info =>
                                        {
                                            Task verify =
                                                buildHelperData.OnSuccesfulPublish(branchStorageObject.CurrentBranch,
                                                    info.blueprintID,
                                                    DateTime.Now);
                                        });
                                }
                                else BuildHelperBuilder.PublishLastBuild();
                            }
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("BuildHelper",
                                "Couldn't find the last build for this branch. Try building again.", "OK");
                        }
                    }
                }

                newBuildButton.tooltip = "Equivalent to Build & Publish in the VRChat SDK";

                if (GUILayout.Button(newBuildButton, buttonStyle))
                {
                    Branch targetBranch = branchStorageObject.CurrentBranch;

                    bool canPublish = true;
                    if (!targetBranch.remoteExists && !targetBranch.HasVRCDataChanges() &&
                        BuildHelperEditorPrefs.UseAsyncPublish)
                    {
                        canPublish = EditorUtility.DisplayDialog("Build Helper",
                            $"You are about to publish a new world, but you haven't edited any world details. The async publisher doesn't enter playmode to let you edit world details, so your world will be uploaded as '{targetBranch.editedName}'. Do you want to continue?",
                            "Continue", "Cancel");
                    }

                    if (canPublish)
                    {
                        if (BuildHelperEditorPrefs.UseAsyncPublish)
                        {
                            BuildHelperBuilder.PublishNewBuildAsync(branchStorageObject.CurrentBranch.ToWorldInfo(), info =>
                            {
                                Task verify = buildHelperData.OnSuccesfulPublish(branchStorageObject.CurrentBranch,
                                    info.blueprintID, DateTime.Now);
                            });
                        }
                        else BuildHelperBuilder.PublishNewBuild();
                    }

                    return;
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Autonomous Builder");

                using (new EditorGUI.DisabledScope(!BuildHelperEditorPrefs.UseAsyncPublish))
                {
                    GUIContent autonomousBuilderButtonSingle = new GUIContent("Current platform",
                        BuildHelperEditorPrefs.UseAsyncPublish
                            ? "Publish your world autonomously"
                            : "To use the autonomous builder, please enable Async Publishing in settings");

                    if (GUILayout.Button(currentPlatformPublish, buttonStyle))
                    {
                        Branch targetBranch = branchStorageObject.CurrentBranch;

                        bool canPublish = true;
                        if (!targetBranch.remoteExists && !targetBranch.HasVRCDataChanges())
                        {
                            canPublish = EditorUtility.DisplayDialog("Build Helper",
                                $"You are about to publish a new world using the autonomous builder, but you haven't edited any world details. The autonomous builder doesn't enter playmode to let you edit world details, so your world will be uploaded as '{targetBranch.editedName}'. Do you want to continue?",
                                "Continue", "Cancel");
                        }

                        if (canPublish)
                        {
                            InitAutonomousBuild(true);
                        }
                    }

                    GUIContent autonomousBuilderButton = new GUIContent("All platforms",
                        BuildHelperEditorPrefs.UseAsyncPublish
                            ? "Publish your world for both platforms simultaneously"
                            : "To use the autonomous builder, please enable Async Publishing in settings");

                    if (GUILayout.Button(crossPlatformPublish, buttonStyle))
                    {
                        Branch targetBranch = branchStorageObject.CurrentBranch;

                        bool canPublish = true;
                        if (!targetBranch.remoteExists && !targetBranch.HasVRCDataChanges())
                        {
                            canPublish = EditorUtility.DisplayDialog("Build Helper",
                                $"You are about to publish a new world using the autonomous builder, but you haven't edited any world details. The autonomous builder doesn't enter playmode to let you edit world details, so your world will be uploaded as '{targetBranch.editedName}'. Do you want to continue?",
                                "Continue", "Cancel");
                        }

                        if (canPublish)
                        {
                            InitAutonomousBuild();
                        }
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private bool CheckLastBuiltBranch()
        {
            Branch currentBranch = branchStorageObject.CurrentBranch;
            return currentBranch.buildData.CurrentPlatformBuildData().buildValid && currentBranch.blueprintID ==
                currentBranch.buildData.CurrentPlatformBuildData().blueprintID;
        }

        private static bool CheckAccount(Branch target)
        {
            if (target.blueprintID == "")
            {
                return true;
            }

            if (blueprintCache.TryGetValue(target.blueprintID, out ApiModel model))
            {
                if (APIUser.CurrentUser.id != ((ApiWorld)model).authorId)
                {
                    return false;
                }

                return true;
            }
            
            return false;
        }

        private void InitAutonomousBuild(bool singlePlatform = false)
        {
            if (branchStorageObject.CurrentBranch.blueprintID == "")
            {
                if (EditorUtility.DisplayDialog("Build Helper",
                        "You are trying to use the autonomous builder with an unpublished build. Are you sure you want to continue?",
                        "Yes", "No"))
                {

                }
                else return;
            }
            else
            {
                if (!EditorUtility.DisplayDialog("Build Helper",
                        singlePlatform
                            ? $"Build Helper will initiate a build and publish cycle for {CurrentPlatform()}"
                            : "Build Helper will initiate a build and publish cycle for both PC and mobile in succesion.",
                        "Proceed", "Cancel"))
                {
                    return;
                }
            }

            AutonomousBuilder.AutonomousBuildData buildInfo = new AutonomousBuilder.AutonomousBuildData
            {
                initialTarget = CurrentPlatform(),
                secondaryTarget = CurrentPlatform() == Platform.Windows ? Platform.Android : Platform.Windows,
                progress = AutonomousBuilder.AutonomousBuildData.Progress.PreInitialBuild,
                worldInfo = branchStorageObject.CurrentBranch.ToWorldInfo(),
                singleTarget = singlePlatform
            };

            AutonomousBuilder.StartAutonomousPublish(buildInfo);
        }

        private void ResetData()
        {
            BuildHelperData data = BuildHelperData.GetDataBehaviour();
            if (data != null)
            {
                DestroyImmediate(data.gameObject);
            }

            GameObject dataObj = new GameObject("BuildHelperData");

            buildHelperData = dataObj.AddComponent<BuildHelperData>();

            branchStorageObject = buildHelperData.dataObject;
            branchStorageObject.branches = new Branch[0];

            dataObj.AddComponent<BuildHelperRuntime>();
            dataObj.hideFlags = HideFlags.HideInHierarchy;
            dataObj.tag = "EditorOnly";

            Save();

            EditorSceneManager.SaveScene(buildHelperData.gameObject.scene);
            OnEnable();
        }

        private void OnDestroy()
        {
            Save();
        }

        private void TrySave()
        {
            Save();
        }

        private void Save()
        {
            EditorUtility.SetDirty(buildHelperData);
        }

        #region Reorderable list initialisation

        private BuildHelperData buildHelperData;
        private BranchStorageObject branchStorageObject;

        private SerializedObject buildHelperDataSO;
        private ReorderableList branchList;

        private void InitBranchList()
        {
            buildHelperDataSO = new SerializedObject(buildHelperData);

            branchList = new ReorderableList(buildHelperDataSO,
                buildHelperDataSO.FindProperty("dataObject").FindPropertyRelative("branches"), true,
                true, true, true)
            {
                drawHeaderCallback = rect => { EditorGUI.LabelField(rect, "World branches"); },

                drawElementCallback = (rect, index, isActive, isFocused) =>
                {
                    SerializedProperty property = branchList.serializedProperty.GetArrayElementAtIndex(index);

                    SerializedProperty branchName = property.FindPropertyRelative("name");
                    SerializedProperty worldID = property.FindPropertyRelative("blueprintID");

                    Rect nameRect = new Rect(rect)
                        { y = rect.y + 1.5f, width = 135, height = EditorGUIUtility.singleLineHeight };
                    Rect blueprintIDRect = new Rect(rect)
                    {
                        x = 165, y = rect.y + 1.5f, width = EditorGUIUtility.currentViewWidth - 115,
                        height = EditorGUIUtility.singleLineHeight
                    };
                    Rect selectedRect = new Rect(rect)
                    {
                        x = EditorGUIUtility.currentViewWidth - 95, y = rect.y + 1.5f, width = 90,
                        height = EditorGUIUtility.singleLineHeight
                    };

                    EditorGUI.LabelField(nameRect, branchName.stringValue);
                    EditorGUI.LabelField(blueprintIDRect, worldID.stringValue);

                    if (branchStorageObject.currentBranch == index)
                        EditorGUI.LabelField(selectedRect, "current branch");
                },
                onAddCallback = list =>
                {
                    Undo.RecordObject(buildHelperData, "Create new branch");

                    Branch newBranch = new Branch
                        { name = "new branch", buildData = new BuildData(), branchID = BuildHelperData.GetUniqueID() };
                    ArrayUtility.Add(ref branchStorageObject.branches, newBranch);

                    list.index = Array.IndexOf(branchStorageObject.branches, newBranch);
                    TrySave();
                },
                onRemoveCallback = list =>
                {
                    string branchName = branchStorageObject.branches[list.index].name;
                    if (EditorUtility.DisplayDialog("Build Helper",
                            $"Are you sure you want to delete the branch '{branchName}'? This can not be undone.",
                            "Yes",
                            "No"))
                    {
                        ArrayUtility.RemoveAt(ref branchStorageObject.branches, list.index);
                    }

                    SwitchBranch(buildHelperData, 0);
                    list.index = 0;
                    TrySave();
                },
                onReorderCallback = list =>
                {
                    branchStorageObject.currentBranch = list.index;
                },

                index = branchStorageObject.currentBranch
            };
        }

        private OverrideContainer _overrideContainer;
        private int currentGameObjectContainerIndex;
        private ReorderableList excludedGameObjectsList;
        private ReorderableList exclusiveGameObjectsList;

        private void InitGameObjectContainerLists()
        {
            if (!buildHelperData) return;
            if (branchStorageObject.branches == null || branchStorageObject.branches.Length == 0) return;

            //setup exclusive list
            exclusiveGameObjectsList = new ReorderableList(buildHelperDataSO,
                buildHelperDataSO.FindProperty("dataObject")
                    .FindPropertyRelative("branches")
                    .GetArrayElementAtIndex(branchStorageObject.currentBranch)
                    .FindPropertyRelative("overrideContainer")
                    .FindPropertyRelative("ExclusiveGameObjects"), true,
                true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Exclusive GameObjects"),

                drawElementCallback = (rect, index, isActive, isFocused) =>
                {
                    SerializedProperty property =
                        exclusiveGameObjectsList.serializedProperty.GetArrayElementAtIndex(index);
                    //EditorGUI.BeginChangeCheck();
                    EditorGUI.PropertyField(rect, property);
                    // if (EditorGUI.EndChangeCheck())
                    // {
                    //     Undo.RecordObject(buildHelperBehaviour, "Modify GameObject list");
                    //     TrySave();
                    // }
                },
                onAddCallback = list =>
                {
                    Undo.RecordObject(buildHelperData, "Add GameObject to list");
                    ArrayUtility.Add(ref _overrideContainer.ExclusiveGameObjects, null);
                    TrySave();
                },
                onRemoveCallback = list =>
                {
                    Undo.RecordObject(buildHelperData, "Remove GameObject from list");

                    GameObject toRemove = _overrideContainer.ExclusiveGameObjects[exclusiveGameObjectsList.index];

                    bool existsInOtherList = false;

                    foreach (OverrideContainer container in branchStorageObject.branches.Select(b => b.overrideContainer))
                    {
                        if (container == _overrideContainer) continue;
                        if (container.ExclusiveGameObjects.Contains(toRemove)) existsInOtherList = true;
                    }

                    if (!existsInOtherList) OverrideContainer.EnableGameObject(toRemove);

                    ArrayUtility.RemoveAt(ref _overrideContainer.ExclusiveGameObjects,
                        exclusiveGameObjectsList.index);
                    TrySave();
                }
            };

            //setup exclude list
            excludedGameObjectsList = new ReorderableList(buildHelperDataSO,
                buildHelperDataSO.FindProperty("dataObject")
                    .FindPropertyRelative("branches")
                    .GetArrayElementAtIndex(branchStorageObject.currentBranch)
                    .FindPropertyRelative("overrideContainer")
                    .FindPropertyRelative("ExcludedGameObjects"), true,
                true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Excluded GameObjects"),

                drawElementCallback = (rect, index, isActive, isFocused) =>
                {
                    SerializedProperty property =
                        excludedGameObjectsList.serializedProperty.GetArrayElementAtIndex(index);
                    //EditorGUI.BeginChangeCheck();
                    EditorGUI.PropertyField(rect, property);
                    // if (EditorGUI.EndChangeCheck())
                    // {
                    //     Undo.RecordObject(buildHelperBehaviour, "Modify GameObject list");
                    //     TrySave();
                    // }
                },
                onAddCallback = list =>
                {
                    Undo.RecordObject(buildHelperData, "Add GameObject to list");
                    ArrayUtility.Add(ref _overrideContainer.ExcludedGameObjects, null);
                    TrySave();
                },
                onRemoveCallback = list =>
                {
                    GameObject toRemove = _overrideContainer.ExcludedGameObjects[excludedGameObjectsList.index];

                    Undo.RecordObject(buildHelperData, "Remove GameObject from list");

                    OverrideContainer.EnableGameObject(toRemove);

                    ArrayUtility.RemoveAt(ref _overrideContainer.ExcludedGameObjects, excludedGameObjectsList.index);
                    TrySave();
                }
            };

            currentGameObjectContainerIndex = branchStorageObject.currentBranch;
        }

        #endregion

        #region Editor GUI Helper Functions

        private void InitializeStyles()
        {
            // EditorGUI
            styleHelpBox = new GUIStyle(EditorStyles.helpBox);
            styleHelpBox.padding = new RectOffset(0, 0, styleHelpBox.padding.top, styleHelpBox.padding.bottom + 3);
            
            modifiedImageBackground = new GUIStyle(GUI.skin.box)
            {
                normal =
                {
                    background = ImageTools.BackgroundTexture(32, 32, new Color(1f, 1f, 0, 0.75f))
                }
            };

            // GUI
            styleBox = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(GUI.skin.box.padding.left * 2, GUI.skin.box.padding.right * 2,
                    GUI.skin.box.padding.top * 2, GUI.skin.box.padding.bottom * 2),
                margin = new RectOffset(0, 0, 4, 4)
            };

            activeWindowsTarget = new GUIContent
            {
                text = "Windows",
                image = EditorGUIUtility.IconContent("d_BuildSettings.Metro.Small").image
            };

            activeAndroidTarget = new GUIContent
            {
                text = "Android",
                image = EditorGUIUtility.IconContent("d_BuildSettings.Android.Small").image
            };

            switchToWindowsTarget = new GUIContent
            {
                text = "  Switch to Windows",
                image = EditorGUIUtility.IconContent("d_RotateTool On").image
            };

            switchToAndroidTarget = new GUIContent
            {
                text = "  Switch to Android",
                image = EditorGUIUtility.IconContent("d_RotateTool On").image
            };

            openWebsite = new GUIContent
            {
                text = " View on VRChat website",
                image = EditorGUIUtility.IconContent("d_BuildSettings.Web.Small").image
            };

            makePrivateButton = new GUIContent
            {
                text = " Make private",
                image = EditorGUIUtility.IconContent("d_animationvisibilitytoggleoff").image
            };

            makePublicButton = new GUIContent
            {
                text = " Make public",
                image = EditorGUIUtility.IconContent("d_animationvisibilitytoggleon").image
            };

            addTagButton = new GUIContent
            {
                text = " Add tag",
                image = EditorGUIUtility.IconContent("d_FilterByLabel").image
            };

            localTestingHeader = new GUIContent
            {
                text = " Local Testing",
                image = EditorGUIUtility.IconContent("d_BuildSettings.Standalone.Small").image
            };

            buildOnlyHeader = new GUIContent
            {
                text = " Build Only",
                image = EditorGUIUtility.IconContent("d_ModelImporter Icon").image
            };

            publishHeader = new GUIContent
            {
                text = " Publishing Options",
                image = EditorGUIUtility.IconContent("d_CloudConnect").image
            };

            refreshButton = new GUIContent
            {
                text = " Refresh",
                image = EditorGUIUtility.IconContent("d_Refresh").image
            };

            lastBuildButton = new GUIContent
            {
                text = " Last Build",
                image = EditorGUIUtility.IconContent("d_Refresh").image
            };

            newBuildButton = new GUIContent
            {
                text = " New Build",
                image = EditorGUIUtility.IconContent("d_ModelImporter Icon").image
            };

            windowsTargetButton = new GUIContent
            {
                text = " Windows",
                image = EditorGUIUtility.IconContent("d_BuildSettings.Metro.Small").image
            };

            androidTargetButton = new GUIContent
            {
                text = " Android",
                image = EditorGUIUtility.IconContent("d_BuildSettings.Android.Small").image
            };

            currentPlatformPublish = new GUIContent
            {
                text = "Current Platform",
                image = EditorGUIUtility.IconContent("d_winbtn_win_max_h@2x").image,
                tooltip = $"Autonomously build and publish a new build for {CurrentPlatform()}. Only available if Async Publisher is enabled."
                //image = CurrentPlatform() == Platform.Windows ? 
                // EditorGUIUtility.IconContent("d_BuildSettings.Metro.Small").image : 
                // EditorGUIUtility.IconContent("d_BuildSettings.Android.Small").image
            };

            crossPlatformPublish = new GUIContent
            {
                text = "All Platforms",
                image = EditorGUIUtility.IconContent("d_winbtn_win_restore_h@2x").image,
                tooltip = $"Autonomously build and publish a new build for both platforms. Only available if Async Publisher is enabled."
                //image = CurrentPlatform() == Platform.Windows ? 
                // EditorGUIUtility.IconContent("d_BuildSettings.Metro.Small").image : 
                // EditorGUIUtility.IconContent("d_BuildSettings.Android.Small").image
            };

            deleteWorldButton = new GUIContent
            {
                text = " Delete World",
                image = EditorGUIUtility.IconContent("d_TreeEditor.Trash").image,
            }; 
            
            editButton = new GUIContent
            {
                text = " Edit",
                image = EditorGUIUtility.IconContent("d_editicon.sml").image,
            }; 
            
            saveButton = new GUIContent
            {
                text = " Save",
                image = EditorGUIUtility.IconContent("d_SaveAs").image,
            };
            
            cancelButton = new GUIContent
            {
                text = " Cancel "
            };

            replaceImageButton = new GUIContent
            {
                text = " Replace Image",
                image = EditorGUIUtility.IconContent("d_RawImage Icon").image,
                tooltip = "Replace the current thumbnail image"
            };

            setImageButton = new GUIContent
            {
                text = " Add Image",
                image = EditorGUIUtility.IconContent("d_RawImage Icon").image,
                tooltip = "Add a thumbnail image to the current branch"
            };
            
            cameraButton = new GUIContent
            {
                text = " Take Picture",
                image = EditorGUIUtility.IconContent("d_SceneViewCamera").image,
                tooltip = "Add a thumbnail image by taking a picture"
            };
            
            revertImageButton = new GUIContent
            {
                text = " Revert Image",
                image = EditorGUIUtility.IconContent("d_RotateTool").image,
                tooltip = "Restore the image to the last uploaded version"
            };
            
            buildFolder = new GUIContent
            {
                text = " Open build folder",
                image = EditorGUIUtility.IconContent("d_FolderOpened Icon").image
            };
            
            exportBuild = new GUIContent
            {
                text = " Export build",
                image = EditorGUIUtility.IconContent("d_SaveAs").image
            };
        }

        private void GetUIAssets()
        {
            _iconVRChat = Resources.Load<Texture2D>("Icons/VRChat-Emblem-32px");
            _iconGitHub = Resources.Load<Texture2D>("Icons/GitHub-Mark-32px");
            _iconCloud = Resources.Load<Texture2D>("Icons/Cloud-32px");
            _iconBuild = Resources.Load<Texture2D>("Icons/Build-32px");
            _iconSettings = Resources.Load<Texture2D>("Icons/Settings-32px");
        }

        private void DrawBanner()
        {
            GUILayout.BeginHorizontal(EditorStyles.helpBox);

            GUIStyle styleRichTextLabel = new GUIStyle(GUI.skin.label) { richText = true };
            GUILayout.Label("<b>VR Build Helper</b>", styleRichTextLabel);

            GUILayout.FlexibleSpace();

            float iconSize = EditorGUIUtility.singleLineHeight;

            GUIContent buttonVRChat = new GUIContent("", "VRChat");
            GUIStyle styleVRChat = new GUIStyle(GUI.skin.box);
            if (_iconVRChat != null)
            {
                buttonVRChat = new GUIContent(_iconVRChat, "VRChat");
                styleVRChat = GUIStyle.none;
            }

            if (GUILayout.Button(buttonVRChat, styleVRChat, GUILayout.Width(iconSize), GUILayout.Height(iconSize)))
            {
                Application.OpenURL("https://vrchat.com/home/user/usr_3a5bf7e4-e569-41d5-b70a-31304fd8e0e8");
            }

            GUILayout.Space(iconSize / 4);

            GUIContent buttonGitHub = new GUIContent("", "Github");
            GUIStyle styleGitHub = new GUIStyle(GUI.skin.box);
            if (_iconGitHub != null)
            {
                buttonGitHub = new GUIContent(_iconGitHub, "Github");
                styleGitHub = GUIStyle.none;
            }

            if (GUILayout.Button(buttonGitHub, styleGitHub, GUILayout.Width(iconSize), GUILayout.Height(iconSize)))
            {
                Application.OpenURL("https://github.com/BocuD/VRBuildHelper");
            }

            GUILayout.Space(iconSize / 4);

            GUIContent buttonSettings = new GUIContent("", "Settings");
            GUIStyle styleSettings = new GUIStyle(GUI.skin.box);
            if (_iconSettings != null)
            {
                buttonSettings = new GUIContent(_iconSettings, "Settings");
                styleSettings = GUIStyle.none;
            }

            if (GUILayout.Button(buttonSettings, styleSettings, GUILayout.Width(iconSize), GUILayout.Height(iconSize)))
            {
                settings = true;
            }

            GUILayout.EndHorizontal();
        }

        #endregion
    }
}