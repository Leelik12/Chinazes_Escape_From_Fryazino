using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using Photon.Pun;

public class SimpleHandPose : MonoBehaviourPun, IPunObservable
{
    [Header("XR Input")]
    public XRInputValueReader<float> gripInput;
    public XRInputValueReader<float> triggerInput;

    [Header("Ссылки")]
    public Animator avatarAnimator;

    [Header("Настройки руки")]
    public bool isLeftHand = true;
    [Tooltip("Максимальный угол сгиба пальцев (в градусах)")]
    public float maxFingerBend = 70f;
    [Tooltip("Максимальный угол сгиба большого пальца")]
    public float maxThumbBend = 50f;

    [Header("Данные (синхронизация)")]
    public float gripStrength;
    public float triggerStrength;

    // Каждая строка — набор суставов одного пальца
    private Transform[][] fingers;

    void Start()
    {
        if (!avatarAnimator)
        {
            Debug.LogError("Animator не назначен в " + name);
            enabled = false;
            return;
        }

        // Загружаем все суставы пальцев
        fingers = new Transform[5][];

        if (isLeftHand)
        {
            fingers[0] = GetFingerChain(HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal);
            fingers[1] = GetFingerChain(HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal);
            fingers[2] = GetFingerChain(HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal);
            fingers[3] = GetFingerChain(HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal);
            fingers[4] = GetFingerChain(HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal);
        }
        else
        {
            fingers[0] = GetFingerChain(HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal);
            fingers[1] = GetFingerChain(HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal);
            fingers[2] = GetFingerChain(HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal);
            fingers[3] = GetFingerChain(HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal);
            fingers[4] = GetFingerChain(HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal);
        }
    }

    Transform[] GetFingerChain(HumanBodyBones proximal, HumanBodyBones intermediate, HumanBodyBones distal)
    {
        return new Transform[]
        {
            avatarAnimator.GetBoneTransform(proximal),
            avatarAnimator.GetBoneTransform(intermediate),
            avatarAnimator.GetBoneTransform(distal)
        };
    }

    void Update()
    {
        if (photonView.IsMine)
        {
            gripStrength = gripInput.ReadValue();
            triggerStrength = triggerInput.ReadValue();
        }

        ApplyFingerPose();
    }

    void ApplyFingerPose()
    {
        if (fingers == null) return;

        for (int i = 0; i < fingers.Length; i++)
        {
            if (fingers[i] == null) continue;

            float bend = 0f;

            // Отдельная логика для большого пальца и указательного
            if (i == 0) // большой палец
                bend = maxThumbBend * gripStrength;
            else if (i == 1) // указательный
                bend = maxFingerBend * Mathf.Max(gripStrength, triggerStrength);
            else // остальные
                bend = maxFingerBend * gripStrength;

            // Сгибаем суставы
            for (int j = 0; j < fingers[i].Length; j++)
            {
                if (fingers[i][j] == null) continue;
                fingers[i][j].localRotation = Quaternion.Euler(bend, 0, 0);
            }
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(gripStrength);
            stream.SendNext(triggerStrength);
        }
        else
        {
            gripStrength = (float)stream.ReceiveNext();
            triggerStrength = (float)stream.ReceiveNext();
        }
    }
}
