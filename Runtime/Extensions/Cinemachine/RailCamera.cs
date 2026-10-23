using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Splines;

namespace Kit
{
    /// <summary>Kamera: Schiene — fährt auf einer Schiene (Spline) mit, immer zur Stelle, die dem Ziel am nächsten ist, und schaut es an.</summary>
    [AddComponentMenu(KitBrand.ComponentMenu + "Kamera/Schiene")]
    public class RailCamera : CameraPreset
    {
        public override string Kind => "Schiene";
        public override void Configure()
        {
            var dolly = Ensure<CinemachineSplineDolly>();
            if (!dolly.Spline) dolly.Spline = CreateRail();
            dolly.AutomaticDolly.Enabled = true;
            dolly.AutomaticDolly.Method = new SplineAutoDolly.NearestPointToTarget();
            dolly.CameraRotation = CinemachineSplineDolly.RotationMode.Default;
            var r = Ensure<CinemachineRotationComposer>();
            r.Damping = new Vector2(0.5f, 0.5f);
        }

        /// <summary>The rail this camera rides on (created with the preset).</summary>
        public SplineContainer Rail => GetComponent<CinemachineSplineDolly>() ? GetComponent<CinemachineSplineDolly>().Spline : null;

        /// <summary>Puts the rail where the camera is now, across its view (after the camera was moved to the Scene view).</summary>
        public void PlaceRail()
        {
            var rail = Rail;
            if (!rail) return;
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(rail.transform, "Schiene ausrichten");
#endif
            rail.transform.SetPositionAndRotation(transform.position, Across(transform));
        }

        static Quaternion Across(Transform t) => Quaternion.LookRotation(Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized + Vector3.forward * 1e-4f);

        // Eine gerade Schiene, 12 m lang, quer zur Blickrichtung: verbiegen mit dem Spline-Werkzeug in der Scene-Ansicht.
        SplineContainer CreateRail()
        {
            var go = new GameObject("Schiene");
            go.transform.SetParent(transform.parent, false);
            go.transform.SetPositionAndRotation(transform.position, Across(transform));
#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Schiene anlegen");
#endif
            var c = go.AddComponent<SplineContainer>();
            var s = c.Spline;
            s.Clear();
            s.Add(new BezierKnot(new Unity.Mathematics.float3(-6f, 0f, 0f)), TangentMode.AutoSmooth);
            s.Add(new BezierKnot(new Unity.Mathematics.float3(0f, 0f, 1.5f)), TangentMode.AutoSmooth);
            s.Add(new BezierKnot(new Unity.Mathematics.float3(6f, 0f, 0f)), TangentMode.AutoSmooth);
            return c;
        }
    }
}
