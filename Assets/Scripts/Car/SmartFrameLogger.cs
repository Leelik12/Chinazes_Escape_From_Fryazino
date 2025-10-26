using UnityEngine;
using System.Text;

public class SmartFrameLogger : MonoBehaviour
{
    private float lastFixedTime;
    private float lastUpdateTime;
    private float lastLateTime;
    private float lastBeforeRenderTime;
    private float deltaFixed, deltaUpdate, deltaLate, deltaBeforeRender;
    private StringBuilder logBuilder = new StringBuilder();

    void FixedUpdate()
    {
        deltaFixed = Time.realtimeSinceStartup - lastFixedTime;
        lastFixedTime = Time.realtimeSinceStartup;
        LogPhase("FixedUpdate", deltaFixed);
    }

    void Update()
    {
        deltaUpdate = Time.realtimeSinceStartup - lastUpdateTime;
        lastUpdateTime = Time.realtimeSinceStartup;
        LogPhase("Update", deltaUpdate);
    }

    void LateUpdate()
    {
        deltaLate = Time.realtimeSinceStartup - lastLateTime;
        lastLateTime = Time.realtimeSinceStartup;
        LogPhase("LateUpdate", deltaLate);
    }

    void OnBeforeRender()
    {
        deltaBeforeRender = Time.realtimeSinceStartup - lastBeforeRenderTime;
        lastBeforeRenderTime = Time.realtimeSinceStartup;
        LogPhase("OnBeforeRender", deltaBeforeRender);
    }

    void OnAnimatorIK(int layerIndex)
    {
        LogPhase("OnAnimatorIK", 0);
    }

    void LogPhase(string phase, float delta)
    {
        logBuilder.AppendLine($"{phase,-15} delta{delta * 1000f,6:F2} ms | t={Time.realtimeSinceStartup:F3}");
    }

    void OnGUI()
    {
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(1.5f, 1.5f, 1));
        GUI.color = Color.green;
        GUI.Label(new Rect(10, 10, 600, 600), logBuilder.ToString());
        if (logBuilder.Length > 4000) logBuilder.Length = 0; // чистим каждые ~60 строк
    }
}
