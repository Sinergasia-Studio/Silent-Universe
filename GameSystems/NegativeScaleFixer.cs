// Taruh file ini di folder Assets/Editor
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// NegativeScaleFixer — Editor tool untuk memperbaiki negative scale pada GameObject.
/// 
/// Cara pakai:
///   Tools > Negative Scale Fixer > ...
///   Atau klik kanan GameObject di Hierarchy > Fix Negative Scale
///
/// Yang dilakukan:
///   - Ubah scale negatif menjadi positif
///   - Flip rotation 180 derajat pada axis yang scale-nya negatif
///     agar visual tetap sama
///   - BoxCollider size dan center juga dikoreksi
/// </summary>
public class NegativeScaleFixer : EditorWindow
{
    private bool _includeChildren = true;
    private bool _fixBoxColliders = true;
    private bool _previewOnly     = false;
    private Vector2 _scroll;
    private List<string> _report = new List<string>();

    [MenuItem("Tools/Negative Scale Fixer")]
    public static void OpenWindow()
    {
        var w = GetWindow<NegativeScaleFixer>("Negative Scale Fixer");
        w.minSize = new Vector2(420, 300);
    }

    // Klik kanan di Hierarchy
    [MenuItem("GameObject/Fix Negative Scale", false, 0)]
    public static void FixSelected()
    {
        if (Selection.gameObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("Negative Scale Fixer", "Pilih GameObject dulu di Hierarchy.", "OK");
            return;
        }
        int fixed_count = 0;
        foreach (var go in Selection.gameObjects)
            fixed_count += FixGameObject(go, true, true);

        EditorUtility.DisplayDialog("Negative Scale Fixer",
            fixed_count > 0
                ? fixed_count + " GameObject berhasil diperbaiki."
                : "Tidak ada negative scale yang ditemukan.",
            "OK");
    }

    // Auto-fix dari console warning — parse path dari pesan error
    [MenuItem("Tools/Negative Scale Fixer/Auto Fix From Warning")]
    public static void AutoFixFromWarning()
    {
        // Cari semua BoxCollider di scene yang punya negative scale
        var allColliders = FindObjectsByType<BoxCollider>(FindObjectsSortMode.None);
        int count = 0;
        foreach (var col in allColliders)
        {
            var ls = col.transform.lossyScale;
            if (ls.x < 0 || ls.y < 0 || ls.z < 0)
            {
                FixGameObject(col.gameObject, true, true);
                count++;
            }
        }
        Debug.Log("[NegativeScaleFixer] Auto-fix selesai: " + count + " BoxCollider diperbaiki.");
    }

    private void OnGUI()
    {
        GUILayout.Label("Negative Scale Fixer", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        _includeChildren = EditorGUILayout.Toggle("Include Children", _includeChildren);
        _fixBoxColliders = EditorGUILayout.Toggle("Fix BoxCollider Size", _fixBoxColliders);
        _previewOnly     = EditorGUILayout.Toggle("Preview Only (tidak apply)", _previewOnly);

        EditorGUILayout.Space();

        GUI.enabled = Selection.gameObjects.Length > 0;
        if (GUILayout.Button("Fix Selected GameObject(s)", GUILayout.Height(32)))
            RunFix();
        GUI.enabled = true;

        if (GUILayout.Button("Auto Fix Semua BoxCollider di Scene", GUILayout.Height(28)))
        {
            _report.Clear();
            var allColliders = FindObjectsByType<BoxCollider>(FindObjectsSortMode.None);
            int count = 0;
            foreach (var col in allColliders)
            {
                var ls = col.transform.lossyScale;
                if (ls.x < 0 || ls.y < 0 || ls.z < 0)
                {
                    if (!_previewOnly)
                        FixGameObject(col.gameObject, _includeChildren, _fixBoxColliders);
                    _report.Add(GetPath(col.transform) + " [FIXED]");
                    count++;
                }
            }
            if (count == 0)
                _report.Add("Tidak ada negative scale yang ditemukan.");
            Repaint();
        }

        EditorGUILayout.Space();
        GUILayout.Label("Report:", EditorStyles.boldLabel);
        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
        foreach (var line in _report)
            EditorGUILayout.LabelField(line, EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndScrollView();
    }

    private void RunFix()
    {
        _report.Clear();
        int total = 0;

        foreach (var go in Selection.gameObjects)
        {
            var targets = new List<Transform> { go.transform };
            if (_includeChildren)
                targets.AddRange(go.GetComponentsInChildren<Transform>());

            foreach (var t in targets)
            {
                var ls = t.localScale;
                bool hasNeg = ls.x < 0 || ls.y < 0 || ls.z < 0;
                if (!hasNeg) continue;

                _report.Add(GetPath(t) + "  scale: " + ls + " -> " + Abs(ls));

                if (!_previewOnly)
                {
                    FixTransform(t, _fixBoxColliders);
                    total++;
                }
            }
        }

        if (!_previewOnly && total > 0)
            _report.Add("--- " + total + " GameObject diperbaiki ---");
        else if (_previewOnly)
            _report.Add("--- Preview only, tidak ada perubahan ---");
        else
            _report.Add("Tidak ada negative scale.");

        Repaint();
    }

    // ── Core Fix ──

    public static int FixGameObject(GameObject go, bool includeChildren, bool fixColliders)
    {
        int count = 0;
        var targets = new List<Transform> { go.transform };
        if (includeChildren)
            targets.AddRange(go.GetComponentsInChildren<Transform>());

        foreach (var t in targets)
        {
            var ls = t.localScale;
            if (ls.x < 0 || ls.y < 0 || ls.z < 0)
            {
                FixTransform(t, fixColliders);
                count++;
            }
        }
        return count;
    }

    private static void FixTransform(Transform t, bool fixColliders)
    {
        Undo.RecordObject(t, "Fix Negative Scale");

        var ls  = t.localScale;
        var rot = t.localEulerAngles;

        // Flip rotation 180° pada axis yang negatif agar visual tidak berubah
        if (ls.x < 0) rot.x += 180f;
        if (ls.y < 0) rot.y += 180f;
        if (ls.z < 0) rot.z += 180f;

        t.localScale       = Abs(ls);
        t.localEulerAngles = rot;

        // Fix BoxCollider
        if (fixColliders)
        {
            var col = t.GetComponent<BoxCollider>();
            if (col != null)
            {
                Undo.RecordObject(col, "Fix BoxCollider");
                col.size   = Abs(col.size);
                col.center = new Vector3(
                    ls.x < 0 ? -col.center.x : col.center.x,
                    ls.y < 0 ? -col.center.y : col.center.y,
                    ls.z < 0 ? -col.center.z : col.center.z
                );
            }
        }

        EditorUtility.SetDirty(t.gameObject);
        Debug.Log("[NegativeScaleFixer] Fixed: " + GetPath(t));
    }

    private static Vector3 Abs(Vector3 v) =>
        new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    private static string GetPath(Transform t)
    {
        string path = t.name;
        var parent  = t.parent;
        while (parent != null) { path = parent.name + "/" + path; parent = parent.parent; }
        return path;
    }
}