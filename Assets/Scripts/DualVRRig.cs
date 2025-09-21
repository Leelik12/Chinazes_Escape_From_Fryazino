using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

public class DualVRRig : MonoBehaviour
{
    public Transform driverHead;     // Голова водителя
    public Transform gunnerHead;     // Голова стрелка

    public XRNode driverNode = XRNode.LeftEye;   // Шлем водителя
    public XRNode gunnerNode = XRNode.RightEye;  // Шлем стрелка

    void Update()
    {
        // Обновляем позиции и повороты для каждого шлема
        UpdateHeadPosition(driverNode, driverHead);
        UpdateHeadPosition(gunnerNode, gunnerHead);
    }

    void UpdateHeadPosition(XRNode node, Transform headTransform)
    {
        Vector3 position;
        Quaternion rotation;

        if (TryGetXRNodeState(node, out position, out rotation))
        {
            headTransform.localPosition = position;
            headTransform.localRotation = rotation;
        }
    }

    bool TryGetXRNodeState(XRNode node, out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        List<XRNodeState> nodeStates = new List<XRNodeState>();
        InputTracking.GetNodeStates(nodeStates);

        foreach (XRNodeState nodeState in nodeStates)
        {
            if (nodeState.nodeType == node)
            {
                nodeState.TryGetPosition(out position);
                nodeState.TryGetRotation(out rotation);
                return true;
            }
        }

        return false;
    }
}