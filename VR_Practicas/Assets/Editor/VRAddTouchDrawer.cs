using Oculus.Interaction;
using Oculus.Interaction.Editor.QuickActions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Makes an object a touch-grabbable drawer that can only slide along the X axis of its parent.
// Same pieces as Interaction SDK > Add Touch Grab Interaction, plus a OneGrabTranslateTransformer
// that locks Y and Z and limits X to [MinX, MaxX] relative to the starting position.
public static class VRAddTouchDrawer
{
    // Opening range along the parent's X axis, relative to where the drawer starts (meters).
    // Flip the sign (e.g. -0.35 / 0) if the drawer opens the other way.
    const float MinX = 0f;
    const float MaxX = 0.35f;

    // Guid of Meta's TouchHandGrabInteractable template prefab (the one the quick action uses).
    const string TouchTemplateGuid = "aaae94d61b3f6bb42be68ba0a270e478";

    [MenuItem("VR/Add Touch Drawer (X axis only)/To Selected Objects")]
    static void ToSelected()
    {
        Apply(Selection.gameObjects);
    }

    [MenuItem("VR/Add Touch Drawer (X axis only)/To Selected Objects", true)]
    static bool ToSelectedValidate()
    {
        return Selection.gameObjects.Length > 0;
    }

    [MenuItem("VR/Add Touch Drawer (X axis only)/To Drawers Named 'C Drawer' In Scene")]
    static void ToCabinetDrawers()
    {
        var found = new System.Collections.Generic.List<GameObject>();
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (t.name.StartsWith("C Drawer"))
                found.Add(t.gameObject);
        Apply(found);
    }

    static void Apply(System.Collections.Generic.IEnumerable<GameObject> targets)
    {
        int done = 0, skipped = 0, failed = 0;
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        foreach (var go in targets)
        {
            if (go == null || !go.scene.IsValid())
                continue;

            if (go.GetComponentInChildren<TouchHandGrabInteractable>(true) != null)
            {
                skipped++;
                continue;
            }

            try
            {
                Setup(go);
                EditorSceneManager.MarkSceneDirty(go.scene);
                done++;
            }
            catch (System.Exception e)
            {
                failed++;
                Debug.LogWarning($"[VR] Could not set up '{go.name}' as touch drawer: {e.Message}", go);
            }
        }

        Undo.CollapseUndoOperations(group);
        Debug.Log($"[VR] Touch drawers: {done} set up, {skipped} already had a touch interactable, {failed} failed. Save the scene (Ctrl+S).");
    }

    static void Setup(GameObject go)
    {
        // Rigidbody: kinematic, the Grabbable moves the transform directly.
        var body = go.GetComponent<Rigidbody>() ?? Undo.AddComponent<Rigidbody>(go);
        body.useGravity = false;
        body.isKinematic = true;

        // Collider for touch detection (the cabinet drawers ship without one).
        Collider col = go.GetComponentInChildren<Collider>();
        if (col == null)
            col = AddFittedBoxCollider(go);

        // Slide only along X (parent space), relative to the starting position.
        var transformer = go.GetComponent<OneGrabTranslateTransformer>() ?? Undo.AddComponent<OneGrabTranslateTransformer>(go);
        var c = new OneGrabTranslateTransformer.OneGrabTranslateConstraints
        {
            ConstraintsAreRelative = true,
            MinX = new FloatConstraint { Constrain = true, Value = MinX },
            MaxX = new FloatConstraint { Constrain = true, Value = MaxX },
            MinY = new FloatConstraint { Constrain = true, Value = 0f },
            MaxY = new FloatConstraint { Constrain = true, Value = 0f },
            MinZ = new FloatConstraint { Constrain = true, Value = 0f },
            MaxZ = new FloatConstraint { Constrain = true, Value = 0f },
        };
        transformer.Constraints = c;
        EditorUtility.SetDirty(transformer);

        var grabbable = go.GetComponent<Grabbable>() ?? Undo.AddComponent<Grabbable>(go);
        grabbable.InjectOptionalTargetTransform(go.transform);
        grabbable.InjectOptionalRigidbody(body);
        grabbable.InjectOptionalOneGrabTransformer(transformer);
        grabbable.InjectOptionalThrowWhenUnselected(false);
        EditorUtility.SetDirty(grabbable);

        // Touch interactable child, from Meta's template prefab.
        string path = AssetDatabase.GUIDToAssetPath(TouchTemplateGuid);
        var template = AssetDatabase.LoadMainAssetAtPath(path) as GameObject;
        if (template == null)
            throw new System.InvalidOperationException("Touch template prefab not found at " + path);

        var obj = Object.Instantiate(template);
        obj.name = "TouchHandGrabInteractable";
        obj.transform.SetParent(go.transform, false);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;
        obj.transform.localScale = Vector3.one;
        Undo.RegisterCreatedObjectUndo(obj, "Add TouchHandGrabInteractable");

        var interactable = obj.GetComponent<TouchHandGrabInteractable>();
        interactable.InjectBoundsCollider(col);
        interactable.InjectColliders(new System.Collections.Generic.List<Collider> { col });
        interactable.InjectOptionalPointableElement(grabbable);
        EditorUtility.SetDirty(interactable);
    }

    static Collider AddFittedBoxCollider(GameObject go)
    {
        var box = Undo.AddComponent<BoxCollider>(go);
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return box;

        // Fit the box to the renderers, expressed in the object's local space.
        Bounds local = default;
        bool first = true;
        foreach (var r in renderers)
        {
            Bounds b = r.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = b.center + Vector3.Scale(b.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = go.transform.InverseTransformPoint(corner);
                if (first) { local = new Bounds(p, Vector3.zero); first = false; }
                else local.Encapsulate(p);
            }
        }
        box.center = local.center;
        box.size = local.size;
        return box;
    }
}
