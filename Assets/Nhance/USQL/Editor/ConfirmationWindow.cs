using UnityEditor;
using UnityEngine;
using System;

public class ConfirmationWindow : EditorWindow
{
    private static string windowTitle;
    private static string confirmationMessage;
    private static Action onConfirm; // Callback for deletion

    public static void ShowWindow(string title, string message, Action confirmAction)
    {
        windowTitle = title;
        confirmationMessage = message;
        onConfirm = confirmAction;

        ConfirmationWindow window = GetWindow<ConfirmationWindow>(title);
        window.position = new Rect(Screen.width / 2, Screen.height / 2, 350, 150);
        window.ShowModal();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(confirmationMessage, EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Yes", GUILayout.Height(25)))
        {
            onConfirm?.Invoke();
            Close();
        }

        if (GUILayout.Button("No", GUILayout.Height(25)))
        {
            Close();
        }

        EditorGUILayout.EndHorizontal();
    }
}