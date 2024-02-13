using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEngine;

/// Binds style and click events to the Spacetime Publisher Window
public class PublisherWindow : EditorWindow
{
    private const string PUBLISHER_DIR_PATH = "Assets/Editor/SpacetimePublisher";
    private static string PathToUxml => $"{PUBLISHER_DIR_PATH}/Publisher.uxml";

    private PublisherData publisherData;
    private Label publishStatusLabel;
    private Button publishBtn;
    
    
    #region Init
    /// Show the publisher window via top Menu item
    [MenuItem("Window/Spacetime/Publisher #&p")] // (SHIFT+ALT+P)
    public static void ShowPublisherWindow()
    {
        PublisherWindow window = GetWindow<PublisherWindow>();
        window.titleContent = new GUIContent("Publisher");
    }
    
    /// Add style to the UI window
    public void CreateGUI()
    {
        // Clone UXML to root visual element of the Editor window, creating UI
        VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PathToUxml);
        visualTree.CloneTree(rootVisualElement);
        
        // Apply style via USS
        string pathToUss = $"{PUBLISHER_DIR_PATH}/Publisher.uss";
        StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(pathToUss);
        rootVisualElement.styleSheets.Add(styleSheet);

        // Set UI elements for the session + subscribe to actions
        setUiElements();
        sanityCheckUiElements();
        setOnActionEvents();
    }

    private void setUiElements()
    {
        // Set
        publishStatusLabel = rootVisualElement.Q<Label>("PublishStatusLabel");
        publishBtn = rootVisualElement.Q<Button>("PublishBtn");
    }
    
    /// Changing implicit names can easily cause unexpected nulls
    private void sanityCheckUiElements()
    {
        // Sanity check - 
        Assert.IsNotNull(publishStatusLabel, $"Expected `{nameof(publishStatusLabel)}`");
        Assert.IsNotNull(publishBtn, $"Expected `{nameof(publishBtn)}`");
    }
    
    private void setOnActionEvents()
    {
        publishBtn.clicked += OnPublishBtnClick;
    }
    #endregion // Init

    
    #region User Input Interactions
    private void OnPublishBtnClick()
    {
        throw new NotImplementedException("TODO: OnPublishBtnClick");
    }
    #endregion // User Input Interactions
}
