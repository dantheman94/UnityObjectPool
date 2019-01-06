using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

//******************************
//
//  Created by: Daniel Marton
//
//  Last edited by: Daniel Marton
//  Last edited on: 6/1/2019
//
//******************************

/// <summary>
//  Resources used:
//  - https://docs.unity3d.com/Manual/RunningEditorCodeOnLaunch.html
//  - https://answers.unity.com/questions/447701/event-for-unity-editor-pause-and-playstop-events.html
/// </summary>

////////////////////////////////////////////////////////////////////////////////////////////////////////////////

[InitializeOnLoad]
public class LocalPoolEditor : EditorWindow {

    //**************************************************************************************************************
    //                                                                                                             *
    //      FUNCTIONS                                                                                              *
    //                                                                                                             *
    //**************************************************************************************************************

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    //  Adds a menu item to the Unity Editor top toolbar.
    //  Brings up the LocalPoolEditor window when clicked on from the toolbar.
    /// </summary>
    [MenuItem("Window/Localized Object Pool")]
    public static void ShowWindow() {

        GetWindow<LocalPoolEditor>("Object Pool");
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    //  Static constructor.
    /// </summary>
    static LocalPoolEditor() {
        
        Debug.Log("PRELOADING OBJECTS");
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    //  
    /// </summary>
    public void OnGUI() {

        EditorGUILayout.Space();

        // Header/title
        EditorGUILayout.BeginVertical();
        GUILayout.Label("LOCALIZED OBJECT POOL DEBUGGER", EditorStyles.boldLabel);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

}

////////////////////////////////////////////////////////////////////////////////////////////////////////////////
